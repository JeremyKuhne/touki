<#
.SYNOPSIS
    Render an inclusive-time flame graph (SVG) from a BenchmarkDotNet
    EventPipeProfiler speedscope trace.

.DESCRIPTION
    BenchmarkDotNet's EventPipeProfiler exports an "evented" speedscope file:
    leaf self-time collapses into a synthetic CPU_TIME node, so the raw trace
    is not directly usable as a flame graph. This script walks the open/close
    events, attributes the wall-clock delta between consecutive events to the
    full open-frame stack, and emits the inclusive-time call tree as a
    standalone icicle SVG (root at top), which is exactly what a flame graph
    visualizes.

    Validated on net10.0 traces only. The EventPipeProfiler does not produce a
    trace on net481 (it resolves to UnresolvedDiagnoser); use [EtwProfiler] for
    Framework profiling instead.

.PARAMETER Path
    Path to the .speedscope.json file written to BenchmarkDotNet.Artifacts.

.PARAMETER OutSvg
    Destination path for the generated SVG.

.PARAMETER RootFrame
    Optional. A substring matched against frame names. When set, time is only
    attributed to the subtree rooted at the first matching frame on each stack
    (and that frame becomes the 100% root). Use this to exclude BenchmarkDotNet
    warmup/pilot/JIT/overhead and scope the graph to just the measured workload,
    e.g. -RootFrame 'WorkloadActionUnroll'.

.EXAMPLE
    ./tools/speedscope-to-flamegraph.ps1 `
        -Path BenchmarkDotNet.Artifacts/MyBench.speedscope.json `
        -OutSvg scratch/mybench.svg -Title "MyBench"
#>
param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][string]$OutSvg,
    [string]$Title = "Flame graph",
    [string]$RootFrame = "",
    [int]$Width = 1200,
    [int]$RowHeight = 18,
    [double]$MinPercent = 0.3
)

$j = Get-Content $Path -Raw | ConvertFrom-Json
$frames = $j.shared.frames

# node = @{ name; value; children = @{name->node} }
$root = @{ name = "root"; value = 0.0; children = @{} }

function Short([string]$n) {
    # Trim the verbose CLR signatures to method identifiers for readability.
    if ($n -match '!([^(]+)') { $n = $matches[1] }
    $n = $n -replace 'value class ', '' -replace 'class ', ''
    if ($n.Length -gt 90) { $n = $n.Substring(0, 87) + '...' }
    return $n
}

foreach ($p in $j.profiles) {
    if (-not $p.events -or $p.events.Count -eq 0) { continue }
    $stack = [System.Collections.Generic.List[int]]::new()
    $lastAt = $null
    foreach ($e in $p.events) {
        $at = [double]$e.at
        if ($null -ne $lastAt -and $stack.Count -gt 0) {
            $delta = $at - $lastAt
            if ($delta -gt 0) {
                # When -RootFrame is set, find the first matching frame on the
                # stack and attribute only the subtree below it; otherwise use
                # the whole stack from the process root.
                $startIdx = 0
                $include = $true
                if ($RootFrame) {
                    $include = $false
                    for ($si = 0; $si -lt $stack.Count; $si++) {
                        if ($frames[$stack[$si]].name -match [regex]::Escape($RootFrame)) {
                            $startIdx = $si
                            $include = $true
                            break
                        }
                    }
                }
                if ($include) {
                    $node = $root
                    $node.value += $delta
                    for ($fi = $startIdx; $fi -lt $stack.Count; $fi++) {
                        $name = Short $frames[$stack[$fi]].name
                        if (-not $node.children.ContainsKey($name)) {
                            $node.children[$name] = @{ name = $name; value = 0.0; children = @{} }
                        }
                        $node = $node.children[$name]
                        $node.value += $delta
                    }
                }
            }
        }
        if ($e.type -eq 'O') { $stack.Add([int]$e.frame) }
        elseif ($e.type -eq 'C') {
            for ($k = $stack.Count - 1; $k -ge 0; $k--) {
                if ($stack[$k] -eq [int]$e.frame) { $stack.RemoveAt($k); break }
            }
        }
        $lastAt = $at
    }
}

$total = $root.value
if ($total -le 0) { Write-Error "No timed samples in $Path"; exit 1 }

$rects = [System.Collections.Generic.List[object]]::new()
$maxDepth = 0

function Emit($node, $depth, $x0, $w) {
    if ($w / $Width * 100.0 -lt $MinPercent) { return }
    if ($depth -gt $script:maxDepth) { $script:maxDepth = $depth }
    $pct = [math]::Round(100.0 * $node.value / $total, 1)
    $script:rects.Add([pscustomobject]@{ x = $x0; depth = $depth; w = $w; name = $node.name; pct = $pct; ms = [math]::Round($node.value, 1) })
    $childTotal = ($node.children.Values | Measure-Object -Property value -Sum).Sum
    if (-not $childTotal) { return }
    $cx = $x0
    foreach ($c in ($node.children.Values | Sort-Object value -Descending)) {
        $cw = $w * ($c.value / $node.value)
        Emit $c ($depth + 1) $cx $cw
        $cx += $cw
    }
}

foreach ($c in ($root.children.Values | Sort-Object value -Descending)) {
    Emit $c 0 0 ($Width * ($c.value / $total))
}

$height = ($maxDepth + 2) * $RowHeight + 40
$palette = @('#e67e22', '#d35400', '#e74c3c', '#c0392b', '#f39c12', '#d68910', '#ca6f1e', '#ba4a00')

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("<svg xmlns='http://www.w3.org/2000/svg' width='$Width' height='$height' font-family='Consolas,monospace' font-size='11'>")
[void]$sb.AppendLine("<rect width='$Width' height='$height' fill='#fafafa'/>")
[void]$sb.AppendLine("<text x='8' y='20' font-size='14' font-weight='bold'>$([System.Security.SecurityElement]::Escape($Title))</text>")
$i = 0
foreach ($r in $rects) {
    $y = 30 + $r.depth * $RowHeight
    $fill = $palette[$i % $palette.Count]; $i++
    $rw = [math]::Max($r.w, 0.5)
    $esc = [System.Security.SecurityElement]::Escape("$($r.name)  ($($r.pct)% incl, $($r.ms) ms)")
    [void]$sb.AppendLine("<g><title>$esc</title><rect x='$([math]::Round($r.x,2))' y='$y' width='$([math]::Round($rw,2))' height='$($RowHeight-1)' fill='$fill' stroke='#fff' stroke-width='0.5'/>")
    if ($rw -gt 55) {
        $label = [System.Security.SecurityElement]::Escape($r.name)
        [void]$sb.AppendLine("<text x='$([math]::Round($r.x+2,2))' y='$($y+12)' fill='#000'>$label</text>")
    }
    [void]$sb.AppendLine("</g>")
}
[void]$sb.AppendLine("</svg>")
Set-Content -Path $OutSvg -Value $sb.ToString() -Encoding UTF8
Write-Host "Wrote $OutSvg  (total $([math]::Round($total,1)) ms, $($rects.Count) frames, depth $maxDepth)"
