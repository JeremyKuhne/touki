<#
.SYNOPSIS
    Capture a .NET perf trace (EventPipe or ETW) of an executable project running,
    then print the filtrace commands to analyze it. This helper drives the capture step:
    EventPipe via dotnet-trace, ETW via the filtrace collect verb.

.DESCRIPTION
    Wraps the "build the project, run its output under a profiler, then analyze the
    trace" loop for an ordinary executable project - a console app, worker, or web
    host. It is the whole-application counterpart to Capture-BenchmarkTrace.ps1,
    which profiles a BenchmarkDotNet micro-benchmark.

    It builds the project, resolves the actual run target from the build output, and
    launches THAT under the profiler - never `dotnet run`. `dotnet run` builds and
    then forks your app into a separate child process, so a single-process EventPipe
    session would trace the build/run host instead of your code. Launching the built
    apphost (or `dotnet <app>.dll`) directly keeps the trace on the app itself.

      - EventPipe (-Profiler EP, the default): cross-platform, no elevation, single
        process. Runs `dotnet-trace collect -- <app>` and writes a .nettrace. Pass
        -Metric alloc for a gc-verbose (allocation) capture instead of CPU sampling.
      - ETW (-Profiler ETW): Windows only, self-elevates (one UAC prompt),
        machine-wide, via `filtrace collect` (TraceEvent, no external recorder). Only an
        .etl carries wall-clock (threadtime), the native GC / JIT / memcpy split
        (classify --native-symbols), and multi-process scoping.

    The app runs to completion (launch-only); the profiler stops when it exits. The
    printed filtrace commands are pre-scoped: an EventPipe trace ranks the whole
    process; an .etl additionally uses --process, because an .etl is machine-wide.

    filtrace: https://github.com/JeremyKuhne/filtrace - install once with
    `dotnet tool install -g KlutzyNinja.Filtrace`, or drive the MCP trace_* tools.

.PARAMETER Project
    Path to the executable project - a .csproj or the directory holding one. Required.

.PARAMETER Profiler
    'EP' (EventPipe, default) or 'ETW' (Windows, self-elevating).

.PARAMETER Metric
    'cpu' (default) or 'alloc'. EventPipe only: 'alloc' captures the gc-verbose
    allocation profile instead of CPU sampling. An ETW capture always records CPU
    and wall-clock (threadtime) together.

.PARAMETER Tfm
    Target-framework moniker to build and run. Default net10.0.

.PARAMETER Configuration
    Build configuration. Default Release - a perf trace should profile optimized output.

.PARAMETER AppArgs
    Arguments passed to the application after the profiler launches it.

.PARAMETER Top
    Rows per ranking in the printed commands. Default 25.

.PARAMETER Output
    Trace output path. Defaults to ./perf-traces/<AssemblyName>.<nettrace|etl>.

.EXAMPLE
    ./Capture-ProjectTrace.ps1 -Project src/MyApp

.EXAMPLE
    ./Capture-ProjectTrace.ps1 -Project src/MyApp -Metric alloc -AppArgs '--input','big.json'

.EXAMPLE
    ./Capture-ProjectTrace.ps1 -Project src/MyApp -Profiler ETW
#>
param(
    [Parameter(Mandatory)][string]$Project,
    [ValidateSet('EP', 'ETW')][string]$Profiler = 'EP',
    [ValidateSet('cpu', 'alloc')][string]$Metric = 'cpu',
    [string]$Tfm = 'net10.0',
    [string]$Configuration = 'Release',
    [string[]]$AppArgs = @(),
    [int]$Top = 25,
    [string]$Output
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

# Reading an .etl (and recording one via a kernel ETW session) is Windows-only.
# Compare against $false so Windows PowerShell 5.1 (where $IsWindows is undefined) is
# not mistaken for a non-Windows OS.
if ($Profiler -eq 'ETW' -and $IsWindows -eq $false) {
    Write-Error 'ETW capture is Windows-only. Use -Profiler EP on this OS.' -ErrorAction Continue
    exit 1
}

function Test-Elevated {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

# filtrace records the ETW session itself (no external recorder); it installs as a global
# tool under ~/.dotnet/tools, which a freshly elevated shell may not have on PATH yet.
# Check it in the normal console (before any elevation) so the install hint is not buried
# in a UAC window that then closes.
$toolsDir = Join-Path $HOME '.dotnet/tools'
if ((Test-Path $toolsDir) -and ($env:PATH -notlike "*$toolsDir*")) {
    $env:PATH = "$toolsDir$([System.IO.Path]::PathSeparator)$env:PATH"
}
if ($Profiler -eq 'ETW' -and -not (Get-Command filtrace -ErrorAction SilentlyContinue)) {
    Write-Error 'filtrace not found. Install it (dotnet tool install -g KlutzyNinja.Filtrace), then re-run.' -ErrorAction Continue
    exit 1
}

# ETW kernel sessions require Administrator. When not elevated, relaunch this script
# in an elevated window; -WorkingDirectory keeps the default output path (and any
# relative -Output) resolving against the caller's directory, not system32.
if ($Profiler -eq 'ETW' -and -not (Test-Elevated)) {
    Write-Host 'ETW capture needs Administrator; relaunching elevated (a UAC prompt will appear).' -ForegroundColor Yellow
    # Quote path/value args so a project path, output path, or app argument containing
    # spaces survives Start-Process joining the array into a single command line.
    $argList = @('-NoProfile', '-File', "`"$PSCommandPath`"", '-Project', "`"$($projFile.FullName)`"",
        '-Profiler', 'ETW', '-Metric', $Metric, '-Tfm', $Tfm, '-Configuration', "`"$Configuration`"", '-Top', $Top)
    if ($Output) { $argList += @('-Output', "`"$Output`"") }
    if ($AppArgs.Count -gt 0) { $argList += @('-AppArgs') + ($AppArgs | ForEach-Object { "`"$_`"" }) }
    $proc = Start-Process pwsh -Verb RunAs -PassThru -Wait -WorkingDirectory (Get-Location).Path -ArgumentList $argList
    if ($proc.ExitCode -ne 0) { Write-Error "Elevated capture failed (exit $($proc.ExitCode))." -ErrorAction Continue ; exit $proc.ExitCode }
    exit 0
}

Write-Host "Building $($projFile.Name) ($Configuration, $Tfm)..." -ForegroundColor Cyan
dotnet build $projFile.FullName -c $Configuration -f $Tfm | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Error "Build failed (exit $LASTEXITCODE)." -ErrorAction Continue ; exit $LASTEXITCODE }

# Resolve the built assembly, its name, and the output kind in one evaluation. With
# more than one -getProperty the SDK returns JSON, so parse the Properties object.
$propsJson = dotnet msbuild $projFile.FullName -getProperty:TargetPath -getProperty:AssemblyName `
    -getProperty:OutputType "-p:Configuration=$Configuration" "-p:TargetFramework=$Tfm" 2>$null | Out-String
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($propsJson)) {
    Write-Error "Could not read build properties from $($projFile.Name) (dotnet msbuild -getProperty failed). Ensure the project restores and builds for $Configuration/$Tfm." -ErrorAction Continue
    exit 1
}
$props = ($propsJson | ConvertFrom-Json).Properties
$targetPath = $props.TargetPath
$assemblyName = $props.AssemblyName
$outputType = $props.OutputType

if ([string]::IsNullOrWhiteSpace($targetPath)) {
    Write-Error "Could not resolve the build output (TargetPath) for $($projFile.Name)." -ErrorAction Continue
    exit 1
}
if ($outputType -notin @('Exe', 'WinExe')) {
    Write-Error "$($projFile.Name) is a '$outputType' project, not an executable. Point at an app project (OutputType Exe)." -ErrorAction Continue
    exit 1
}

# Prefer the apphost so the process carries the app's own name (an .etl is
# machine-wide, and `--process $assemblyName` is far easier to scope than `dotnet`).
# Fall back to `dotnet <app>.dll` when no apphost was produced (UseAppHost=false).
$outputDir = Split-Path -Parent $targetPath
# Treat an undefined $IsWindows (Windows PowerShell 5.1) as Windows, matching the ETW
# guard above, so the .exe apphost is still found there.
$exeSuffix = ''
if ($IsWindows -ne $false) { $exeSuffix = '.exe' }
$appHost = Join-Path $outputDir ($assemblyName + $exeSuffix)
if (Test-Path -LiteralPath $appHost) {
    $runExe = $appHost
    $runPrefixArgs = @()
    $processName = $assemblyName
}
else {
    $runExe = 'dotnet'
    $runPrefixArgs = @($targetPath)
    $processName = 'dotnet'
}

# The build output directory holds the portable PDBs that resolve source lines.
$symbols = $outputDir

# Default the trace path under ./perf-traces (created on demand).
if (-not $Output) {
    $captureDir = Join-Path (Get-Location).Path 'perf-traces'
    New-Item -ItemType Directory -Force -Path $captureDir | Out-Null
    $ext = 'nettrace'
    if ($Profiler -eq 'ETW') { $ext = 'etl' }
    $Output = Join-Path $captureDir "$assemblyName.$ext"
}

if ($Profiler -eq 'EP') {
    # dotnet-trace is a separate global tool; make sure it is installed and on PATH.
    $toolsDir = Join-Path $HOME '.dotnet/tools'
    if ((Test-Path $toolsDir) -and ($env:PATH -notlike "*$toolsDir*")) {
        $env:PATH = "$toolsDir$([System.IO.Path]::PathSeparator)$env:PATH"
    }
    if (-not (Get-Command dotnet-trace -ErrorAction SilentlyContinue)) {
        Write-Host 'dotnet-trace not found; installing the global tool...' -ForegroundColor Yellow
        dotnet tool install --global dotnet-trace | Out-Host
        if ($LASTEXITCODE -ne 0) { Write-Error 'Failed to install dotnet-trace. Install it manually: dotnet tool install -g dotnet-trace.' -ErrorAction Continue ; exit 1 }
        if ($env:PATH -notlike "*$toolsDir*") { $env:PATH = "$toolsDir$([System.IO.Path]::PathSeparator)$env:PATH" }
    }

    $traceProfile = 'cpu-sampling'
    if ($Metric -eq 'alloc') { $traceProfile = 'gc-verbose' }

    Write-Host "Capturing EventPipe ($Metric) trace of $processName..." -ForegroundColor Cyan
    # Launch the built app directly (never `dotnet run`) so this single-process
    # EventPipe session records the app, not a separate build/run host process.
    $collectArgs = @('collect', '--output', $Output, '--profile', $traceProfile, '--', $runExe)
    $collectArgs += $runPrefixArgs
    $collectArgs += $AppArgs
    dotnet-trace @collectArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { Write-Error "dotnet-trace failed (exit $LASTEXITCODE)." -ErrorAction Continue ; exit $LASTEXITCODE }
}
else {
    Write-Host "Capturing ETW (CPU + threadtime) trace of $processName via filtrace collect..." -ForegroundColor Cyan
    # filtrace records the ETW session itself with TraceEvent - no PerfView or wpr. It
    # launches the built app, captures CPU + context-switch (threadtime) stacks with managed
    # method names, and writes the machine-wide .etl the analysis verbs read.
    # filtrace collect takes a single command-line string; quote any element that has
    # whitespace (escaping embedded quotes) so argument boundaries survive the join, the
    # way the EventPipe path preserves them by passing an array.
    $launchArgs = (@($runPrefixArgs) + $AppArgs | ForEach-Object {
            if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
        }) -join ' '
    $collectArgs = @('collect', '--launch', $runExe, '--output', $Output, '--metric', 'threadtime')
    if ($launchArgs) { $collectArgs += @('--launch-args', $launchArgs) }
    filtrace @collectArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { Write-Error "filtrace collect failed (exit $LASTEXITCODE)." -ErrorAction Continue ; exit $LASTEXITCODE }
}

Write-Host "`nCaptured: $Output" -ForegroundColor Green
Write-Host "`nNext-step filtrace commands:" -ForegroundColor Green
if ($Profiler -eq 'ETW') {
    # An .etl is machine-wide: scope every query to the captured process.
    Write-Host "  filtrace processes `"$Output`""
    Write-Host "  filtrace cpu `"$Output`" --process $processName --top $Top"
    Write-Host "  filtrace threadtime `"$Output`" --process $processName --top $Top"
    Write-Host "  filtrace lines `"$Output`" --process $processName --symbols `"$symbols`""
    Write-Host "  filtrace classify `"$Output`" --process $processName --native-symbols"
}
else {
    # A single-process EventPipe trace ranks the whole app; there is no harness.
    if ($Metric -eq 'alloc') {
        Write-Host "  filtrace alloc `"$Output`" --top $Top"
    }
    else {
        Write-Host "  filtrace cpu `"$Output`" --top $Top"
    }
    Write-Host "  filtrace lines `"$Output`" --symbols `"$symbols`""
    Write-Host "  # scope past runtime startup with --root <Type>.<Method> once you see the ranking"
}
