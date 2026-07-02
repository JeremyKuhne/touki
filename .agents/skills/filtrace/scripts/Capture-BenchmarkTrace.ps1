<#
.SYNOPSIS
    Capture a .NET CPU trace (EventPipe or ETW) of a BenchmarkDotNet benchmark, then
    print the filtrace commands to analyze it. This helper drives the capture step via
    BenchmarkDotNet's EventPipe or ETW profiler.

.DESCRIPTION
    Wraps the "record a trace, then analyze it" loop for a BenchmarkDotNet perf
    project. Run it from the repository root (where BenchmarkDotNet.Artifacts should
    land):

      - EventPipe (-Profiler EP, the default): cross-platform, no elevation, single
        process. Runs `dotnet run -c Release -f <Tfm> --project <Project> --
        --filter <Filter> -p EP`, which writes a raw .nettrace and (today) also
        a derived .speedscope.json from the same capture. The raw .nettrace is
        preferred when both exist - it is the only one of the two that carries
        allocation events and per-frame source locations, which the printed
        `alloc` / `lines` commands need; a .speedscope.json is CPU-self-time
        only, and its `filtrace lines` output is always empty (per the filtrace
        skill, speedscope inputs carry no line data). Falls back to
        .speedscope.json - printing only the commands that work against it
        (`cpu`, `export`) - when no .nettrace was produced.
      - ETW (-Profiler ETW): Windows only, self-elevates (one UAC prompt), machine
        wide. Runs the same with `-p ETW --keepFiles`, which writes a .etl. Only an
        .etl carries wall-clock (threadtime), the native GC / JIT / memcpy split
        (classify --native-symbols), and multi-process scoping.

    Output is teed, never redirected away, so the elevated window shows live
    progress instead of looking hung. The printed filtrace commands are pre-scoped:
    an EventPipe trace with --benchmark (past the harness); an .etl additionally with
    --process, because an .etl is machine-wide.

    filtrace: https://github.com/JeremyKuhne/filtrace - install once with
    `dotnet tool install -g KlutzyNinja.Filtrace`, or drive the MCP trace_* tools.

.PARAMETER Project
    Path to the perf project - a .csproj or the directory holding one. Required.

.PARAMETER Filter
    BenchmarkDotNet --filter glob selecting the benchmark(s), e.g. '*GlobMatchBench*'.
    Profile one at a time for a clean trace. Required.

.PARAMETER Profiler
    'EP' (EventPipe, default) or 'ETW' (Windows, self-elevating).

.PARAMETER Tfm
    Target-framework moniker to run. Default net10.0.

.PARAMETER Process
    Process-name substring the printed ETW commands scope to with --process.
    Defaults to the project file's base name (the benchmark host).

.PARAMETER Top
    Rows per ranking in the printed commands. Default 25.

.EXAMPLE
    ./Capture-BenchmarkTrace.ps1 -Project src/App.Perf -Filter '*GlobMatchBench*'

.EXAMPLE
    ./Capture-BenchmarkTrace.ps1 -Project src/App.Perf -Filter '*GlobMatchBench*' -Profiler ETW
#>
param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Filter,
    [ValidateSet('EP', 'ETW')][string]$Profiler = 'EP',
    [string]$Tfm = 'net10.0',
    [string]$Process,
    [int]$Top = 25
)

$ErrorActionPreference = 'Stop'

# Resolve the project file (accept either a .csproj or a directory holding one).
$projItem = Get-Item -LiteralPath $Project
if ($projItem.PSIsContainer) {
    $projFile = Get-ChildItem -LiteralPath $Project -Filter *.csproj | Select-Object -First 1
    if ($null -eq $projFile) { Write-Error "No .csproj found under $Project." -ErrorAction Continue ; exit 1 }
}
else {
    $projFile = $projItem
}
if (-not $Process) { $Process = [System.IO.Path]::GetFileNameWithoutExtension($projFile.Name) }

$repoRoot = (Get-Location).Path
$artifacts = Join-Path $repoRoot 'BenchmarkDotNet.Artifacts'
$log = Join-Path $artifacts 'capture.log'

function Test-Elevated {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# Recording an .etl is Windows-only, and Test-Elevated below calls a Windows-only API, so
# fail fast with a clear message rather than a PlatformNotSupportedException. Compare
# against $false so Windows PowerShell 5.1 (undefined $IsWindows) is not mistaken for a
# non-Windows OS.
if ($Profiler -eq 'ETW' -and $IsWindows -eq $false) {
    Write-Error 'ETW capture is Windows-only. Use -Profiler EP on this OS.' -ErrorAction Continue
    exit 1
}

# ETW kernel sessions require Administrator. When not elevated, relaunch this script
# in an elevated window that shows the capture's live progress, then wait for it.
# -WorkingDirectory anchors the child at the repo root so BenchmarkDotNet.Artifacts (and
# the capture log the parent tails) resolve there, not in the elevated shell's system32.
if ($Profiler -eq 'ETW' -and -not (Test-Elevated)) {
    Write-Host 'ETW capture needs Administrator; relaunching elevated (a UAC prompt will appear).' -ForegroundColor Yellow
    # Quote path/value args so a project path, filter, or process name containing spaces
    # survives Start-Process joining the array into a single command line.
    $argList = @('-NoProfile', '-File', "`"$PSCommandPath`"", '-Project', "`"$($projFile.FullName)`"",
        '-Filter', "`"$Filter`"", '-Profiler', 'ETW', '-Tfm', $Tfm, '-Process', "`"$Process`"", '-Top', $Top)
    $proc = Start-Process pwsh -Verb RunAs -PassThru -Wait -WorkingDirectory $repoRoot -ArgumentList $argList
    if ($proc.ExitCode -ne 0) { Write-Error "Elevated capture failed (exit $($proc.ExitCode)). See $log." -ErrorAction Continue ; exit $proc.ExitCode }
    if (Test-Path $log) { Write-Host "`n--- capture log tail (full log: $log) ---" -ForegroundColor Cyan ; Get-Content $log -Tail 20 }
    exit 0
}

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# Without BenchmarkDotNet.Diagnostics.Windows the `-p ETW` profiler silently resolves
# to UnresolvedDiagnoser and no .etl is written - fail fast with guidance.
if ($Profiler -eq 'ETW' -and -not (Select-String -Path $projFile.FullName -Pattern 'BenchmarkDotNet.Diagnostics.Windows' -Quiet)) {
    Write-Error "$($projFile.Name) does not reference BenchmarkDotNet.Diagnostics.Windows; -p ETW will no-op. Add the package first." -ErrorAction Continue
    exit 1
}

# Both branches are multi-element arrays, so they stay arrays (a single-element
# if-expression would unwrap to a scalar under Set-StrictMode).
$profArg = if ($Profiler -eq 'ETW') { @('-p', 'ETW', '--keepFiles') } else { @('-p', 'EP') }

Write-Host "Capturing $Profiler trace: $Filter ($Tfm)..." -ForegroundColor Cyan
# Tee, do not redirect: an elevated window shows BenchmarkDotNet's live progress
# while the run is also logged for the parent window to surface.
dotnet run -c Release -f $Tfm --project $projFile.FullName -- --filter $Filter @profArg 2>&1 |
    Tee-Object -FilePath $log
if ($LASTEXITCODE -ne 0) { Write-Error "Benchmark run failed (exit $LASTEXITCODE). See $log." -ErrorAction Continue ; exit $LASTEXITCODE }

# Locate the newest trace of the right kind (BenchmarkDotNet may nest it under a
# results/ subfolder, so recurse). For EventPipe, prefer the raw .nettrace over any
# derived .speedscope.json from the same capture - the .nettrace also carries
# allocation events and per-frame source locations that the alloc/lines commands
# below need and a speedscope conversion does not; fall back to .speedscope.json
# only when no .nettrace was produced.
if ($Profiler -eq 'ETW') {
    $trace = Get-ChildItem -Path $artifacts -Filter '*.etl' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if ($null -eq $trace) { Write-Error "No *.etl found in $artifacts. Did the capture run?" -ErrorAction Continue ; exit 1 }
}
else {
    $trace = Get-ChildItem -Path $artifacts -Filter '*.nettrace' -Recurse -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime | Select-Object -Last 1
    if ($null -eq $trace) {
        $trace = Get-ChildItem -Path $artifacts -Filter '*.speedscope.json' -Recurse -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime | Select-Object -Last 1
    }
    if ($null -eq $trace) { Write-Error "No *.nettrace or *.speedscope.json found in $artifacts. Did the capture run?" -ErrorAction Continue ; exit 1 }
}

# The build output BenchmarkDotNet kept (EventPipe) or --keepFiles preserved (ETW);
# its embedded PDBs resolve managed frames to source lines for lines/heatmap.
$symbols = Join-Path (Split-Path -Parent $projFile.FullName) "bin/Release/$Tfm"

Write-Host "`nCaptured: $($trace.FullName)" -ForegroundColor Green
Write-Host "`nNext-step filtrace commands:" -ForegroundColor Green
if ($Profiler -eq 'ETW') {
    # An .etl is machine-wide: scope every query to the benchmark process AND to
    # the measured workload with --benchmark - both, every time, not just when a
    # ranking looks noisy. --process narrows the OS process; --benchmark is what
    # excludes the harness/warmup subtree so a ranking or export's proportions
    # actually reflect the measured [Benchmark] code.
    Write-Host "  filtrace processes `"$($trace.FullName)`""
    Write-Host "  filtrace cpu `"$($trace.FullName)`" --process $Process --benchmark --top $Top"
    Write-Host "  filtrace threadtime `"$($trace.FullName)`" --process $Process --benchmark --top $Top"
    Write-Host "  filtrace lines `"$($trace.FullName)`" --process $Process --benchmark --symbols `"$symbols`""
    Write-Host "  filtrace classify `"$($trace.FullName)`" --process $Process --benchmark --native-symbols"
    Write-Host "  filtrace export `"$($trace.FullName)`" --process $Process --benchmark --native-symbols --symbols `"$symbols`" -o flame.speedscope.json"
}
elseif ($trace.Name -like '*.nettrace') {
    # A raw .nettrace carries CPU, allocations, and per-frame source locations -
    # every verb below works against it. Scope past the BenchmarkDotNet harness
    # with --benchmark - every verb here, export included, not just the ones that
    # print a ranking.
    Write-Host "  filtrace cpu `"$($trace.FullName)`" --benchmark --top $Top"
    Write-Host "  filtrace alloc `"$($trace.FullName)`" --benchmark --top $Top"
    Write-Host "  filtrace lines `"$($trace.FullName)`" --benchmark --symbols `"$symbols`""
    Write-Host "  filtrace export `"$($trace.FullName)`" --benchmark --symbols `"$symbols`" -o flame.speedscope.json"
}
else {
    # A derived .speedscope.json carries CPU self-time only: no allocation events
    # (alloc needs a .nettrace) and no per-frame source locations (speedscope inputs
    # never carry line data), so only print the commands that actually work against it.
    Write-Host "  filtrace cpu `"$($trace.FullName)`" --benchmark --top $Top"
    Write-Host "  filtrace export `"$($trace.FullName)`" --benchmark -o flame.speedscope.json"
}
