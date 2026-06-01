<#
.SYNOPSIS
    Run a touki.perf benchmark under the EventPipe CPU profiler and emit accurate
    hotspot rankings (and optionally a flame-graph SVG) in one command.

.DESCRIPTION
    Wraps the full "profile a benchmark, then read where the time went" loop:

      1. Runs `dotnet run -c Release -f <tfm> --project touki.perf -- --filter
         <Filter> -p EP`, which exports a .speedscope.json into
         BenchmarkDotNet.Artifacts/.
      2. Locates the newest .speedscope.json matching the filter.
      3. Pipes it through tools/Get-TraceHotspots.ps1 to print self-time and
         inclusive-time rankings with the JIT-helper sampling artifacts
         (BulkMoveWithWriteBarrier, PollGC, the synthetic CPU_TIME marker, ...)
         folded back into the real methods.
      4. Optionally renders an inclusive flame-graph SVG via
         tools/speedscope-to-flamegraph.ps1.

    EventPipe profiling is net10.0-only; the script refuses net481 (use
    [EtwProfiler] for Framework profiling). See
    docs/performance-investigation.md section 3 for the full rationale.

.PARAMETER Filter
    BenchmarkDotNet --filter glob selecting the benchmark(s) to profile, e.g.
    '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingleWithRoot'. Profile one
    method at a time for a clean trace.

.PARAMETER RootFrame
    Substring identifying the frame to scope the rankings to (passed through to
    Get-TraceHotspots). Strongly recommended - without it the rankings include
    BenchmarkDotNet warmup and idle threadpool threads. Do NOT use the benchmark
    method name (it also matches BenchmarkDotNet's 'Activity Benchmark(...)'
    wrapper); use a frame inside the workload (e.g. an enumerator MoveNext).

.PARAMETER OutSvg
    Optional path. When set, also renders an inclusive flame-graph SVG scoped to
    -RootFrame.

.PARAMETER Tfm
    Target framework. Only net10.0 supports EventPipe; defaults to net10.0.

.PARAMETER Top
    Rows per ranking. Default 25.

.PARAMETER SkipRun
    Skip the benchmark run and analyze the newest existing trace for -Filter.
    Use when you already have a fresh trace and only want to re-aggregate.

.EXAMPLE
    ./tools/Profile-Benchmark.ps1 `
        -Filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingleWithRoot' `
        -RootFrame 'RecordedDirectoryEnumerator.MoveNext' `
        -OutSvg scratch/extglob-singlewithroot.svg
#>
param(
    [Parameter(Mandatory)][string]$Filter,
    [string]$RootFrame = "",
    [string]$OutSvg = "",
    [string]$Tfm = "net10.0",
    [int]$Top = 25,
    [switch]$SkipRun
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot "BenchmarkDotNet.Artifacts"

if ($Tfm -ne "net10.0") {
    Write-Error "EventPipe profiling is net10.0-only ('$Tfm' resolves to UnresolvedDiagnoser on Framework). Use [EtwProfiler] for net481."
    exit 1
}

if (-not $SkipRun) {
    Write-Host "Running benchmark under EventPipe profiler: $Filter ($Tfm)..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        dotnet run -c Release -f $Tfm --project touki.perf -- --filter $Filter -p EP
        if ($LASTEXITCODE -ne 0) { Write-Error "Benchmark run failed (exit $LASTEXITCODE)."; exit $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }
}

# The exported file name embeds the benchmark method name; match on the filter's
# last path segment, falling back to the newest trace overall.
$needle = ($Filter -replace '[\*]', '').Split('.')[-1]
$trace = Get-ChildItem -Path $artifacts -Filter "*.speedscope.json" -ErrorAction SilentlyContinue |
    Where-Object { $needle -eq "" -or $_.Name -like "*$needle*" } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $trace) {
    $trace = Get-ChildItem -Path $artifacts -Filter "*.speedscope.json" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
}

if (-not $trace) {
    Write-Error "No .speedscope.json found in $artifacts. Did the run export a trace?"
    exit 1
}

Write-Host "`nAnalyzing trace: $($trace.Name)" -ForegroundColor Cyan
& (Join-Path $PSScriptRoot "Get-TraceHotspots.ps1") -Path $trace.FullName -RootFrame $RootFrame -Top $Top

if ($OutSvg) {
    Write-Host ""
    & (Join-Path $PSScriptRoot "speedscope-to-flamegraph.ps1") `
        -Path $trace.FullName -OutSvg $OutSvg -Title $needle -RootFrame $RootFrame
}
