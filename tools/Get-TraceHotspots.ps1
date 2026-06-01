<#
.SYNOPSIS
    Aggregate a BenchmarkDotNet EventPipeProfiler speedscope trace into accurate
    self-time and inclusive-time rankings, folding JIT-helper sampling artifacts
    back into the real methods that incurred them.

.DESCRIPTION
    BenchmarkDotNet's EventPipeProfiler exports an "evented" speedscope file.
    Two facts make a naive "top self-time" reading wrong:

      1. The leaf self-time of every sample collapses into a synthetic CPU_TIME
         (or UNMANAGED_CODE_TIME) marker node, so an unfolded self-time
         aggregation reports ~100% CPU_TIME and 0 ms for every managed method.

      2. EventPipe's stack walk is managed-only. When a sample's instruction
         pointer lands inside a JIT helper - a write barrier, a memmove, or the
         GC-poll thunk RyuJIT emits at loop back-edges - the walker resolves the
         leaf to the helper thunk (System.Buffer.BulkMoveWithWriteBarrier,
         Thread.PollGCWorker, ...) instead of the method whose hot loop is
         actually running. Those frames then appear, misleadingly, to dominate.

    This script folds both classes of frame (the synthetic markers and the JIT
    helper thunks) into the nearest non-folded ancestor on each sample's stack,
    so self-time is credited to the real method. The inclusive ranking simply
    skips folded frames. The result matches what PerfView produces with
    /FoldPats, with no GUI and no extra tooling - it reads the .speedscope.json
    that BenchmarkDotNet already wrote.

    Validated on net10.0 traces only. The EventPipeProfiler does not produce a
    trace on net481 (it resolves to UnresolvedDiagnoser); use [EtwProfiler] for
    Framework profiling instead.

.PARAMETER Path
    Path to the .speedscope.json file written to BenchmarkDotNet.Artifacts.

.PARAMETER RootFrame
    Optional substring matched against frame names. When set, time is attributed
    only to the subtree rooted at the first matching frame on each stack, and
    that frame becomes the 100% root. Use it to exclude BenchmarkDotNet
    warmup/pilot/JIT/idle-threadpool overhead and scope to the measured work.

    CRITICAL: BenchmarkDotNet wraps the workload in an
    'Activity Benchmark(...benchmarkName=Foo...)' frame whose NAME CONTAINS the
    benchmark method name. Using the method name as -RootFrame therefore also
    matches that wrapper and pulls in idle threadpool threads. Scope to a frame
    inside the actual workload instead (e.g. the enumerator MoveNext, or the
    first method unique to the system under test).

.PARAMETER Fold
    Regex patterns. A leaf frame whose (shortened) name matches any pattern is
    folded into its caller. Defaults cover the synthetic sample markers and the
    common JIT-helper thunks. Pass your own list to fold additional frames.

.PARAMETER CallersOf
    Optional. Instead of the self/inclusive ranking, report which frames are the
    immediate callers of the frame matching this substring, with the time each
    caller contributes. Use it to confirm what a JIT-helper artifact is really
    attributable to (e.g. -CallersOf BulkMoveWithWriteBarrier).

.PARAMETER Top
    Number of rows to print in each ranking. Default 25.

.EXAMPLE
    ./tools/Get-TraceHotspots.ps1 `
        -Path BenchmarkDotNet.Artifacts/MyBench.speedscope.json `
        -RootFrame 'RecordedDirectoryEnumerator.MoveNext'

.EXAMPLE
    ./tools/Get-TraceHotspots.ps1 -Path trace.speedscope.json `
        -CallersOf BulkMoveWithWriteBarrier -RootFrame 'MoveNext'
#>
param(
    [Parameter(Mandatory)][string]$Path,
    [string]$RootFrame = "",
    [string[]]$Fold = @(
        "CPU_TIME",
        "UNMANAGED_CODE_TIME",
        "BulkMoveWithWriteBarrier",
        "PollGC",
        "Memmove",
        "WriteBarrier",
        "JIT_"
    ),
    [string]$CallersOf = "",
    [int]$Top = 25
)

$j = Get-Content $Path -Raw | ConvertFrom-Json
$frames = $j.shared.frames

function Short([string]$n) {
    if ($n -match '!([^(]+)') { $n = $matches[1] }
    $n = $n -replace 'value class ', '' -replace 'class ', ''
    return $n
}

function IsFolded([string]$name) {
    foreach ($p in $Fold) {
        if ($name -match $p) { return $true }
    }
    return $false
}

$selfTime = @{}
$inclTime = @{}
$callerTime = @{}
$callersTargetTotal = 0.0
$total = 0.0

foreach ($p in $j.profiles) {
    if (-not $p.events -or $p.events.Count -eq 0) { continue }
    $stack = [System.Collections.Generic.List[int]]::new()
    $lastAt = $null
    foreach ($e in $p.events) {
        $at = [double]$e.at
        if ($null -ne $lastAt -and $stack.Count -gt 0) {
            $delta = $at - $lastAt
            if ($delta -gt 0) {
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
                    $total += $delta

                    if ($CallersOf) {
                        # Topmost occurrence of the target frame; credit its caller.
                        for ($si = $stack.Count - 1; $si -ge $startIdx; $si--) {
                            $nm = Short $frames[$stack[$si]].name
                            if ($nm -match [regex]::Escape($CallersOf)) {
                                $callersTargetTotal += $delta
                                $caller = if ($si -gt $startIdx) { Short $frames[$stack[$si - 1]].name } else { "<root>" }
                                if (-not $callerTime.ContainsKey($caller)) { $callerTime[$caller] = 0.0 }
                                $callerTime[$caller] += $delta
                                break
                            }
                        }
                    }
                    else {
                        # Self-time: walk up past folded frames to the real leaf.
                        $leafIdx = $stack.Count - 1
                        while ($leafIdx -gt $startIdx -and (IsFolded (Short $frames[$stack[$leafIdx]].name))) {
                            $leafIdx--
                        }
                        $leaf = Short $frames[$stack[$leafIdx]].name
                        if (-not $selfTime.ContainsKey($leaf)) { $selfTime[$leaf] = 0.0 }
                        $selfTime[$leaf] += $delta

                        # Inclusive: credit each distinct non-folded frame once.
                        $seen = @{}
                        for ($fi = $startIdx; $fi -lt $stack.Count; $fi++) {
                            $name = Short $frames[$stack[$fi]].name
                            if (IsFolded $name) { continue }
                            if (-not $seen.ContainsKey($name)) {
                                $seen[$name] = $true
                                if (-not $inclTime.ContainsKey($name)) { $inclTime[$name] = 0.0 }
                                $inclTime[$name] += $delta
                            }
                        }
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

if ($total -le 0) { Write-Error "No timed samples in $Path (check -RootFrame)"; exit 1 }

Write-Host "`nTotal scoped time: $([math]::Round($total,1)) ms"
if ($RootFrame) { Write-Host "Scoped to subtree: $RootFrame" }

if ($CallersOf) {
    $tpct = [math]::Round(100.0 * $callersTargetTotal / $total, 1)
    Write-Host "`n===== CALLERS OF '$CallersOf' (inclusive $([math]::Round($callersTargetTotal,1)) ms, $tpct% of scope) ====="
    $callerTime.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $Top | ForEach-Object {
        $pct = if ($callersTargetTotal -gt 0) { [math]::Round(100.0 * $_.Value / $callersTargetTotal, 1) } else { 0 }
        "{0,9:N1} ms  {1,5:N1}% of target  {2}" -f $_.Value, $pct, $_.Key
    }
    return
}

Write-Host "Folded patterns: $($Fold -join ', ')"
Write-Host "`n===== TOP SELF-TIME (helpers folded into caller) ====="
$selfTime.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $Top | ForEach-Object {
    $pct = [math]::Round(100.0 * $_.Value / $total, 1)
    "{0,9:N1} ms  {1,5:N1}%  {2}" -f $_.Value, $pct, $_.Key
}

Write-Host "`n===== TOP INCLUSIVE-TIME ====="
$inclTime.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $Top | ForEach-Object {
    $pct = [math]::Round(100.0 * $_.Value / $total, 1)
    "{0,9:N1} ms  {1,5:N1}%  {2}" -f $_.Value, $pct, $_.Key
}
