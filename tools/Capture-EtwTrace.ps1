<#
.SYNOPSIS
    Capture an ETW (.etl) CPU trace of a touki.perf benchmark on .NET Framework
    (net481) and print the exact, process-scoped filtrace commands to analyze it.

.DESCRIPTION
    EventPipe profiling (tools/Profile-Benchmark.ps1) is net10-only and managed-only.
    The Framework target needs ETW, which requires running elevated. This script
    wraps that whole loop:

      1. Self-elevates (a single UAC prompt) when not already Administrator. The
         elevated window shows the benchmark's LIVE progress - output is teed to a
         log, never redirected away with `*>`, so the window never looks hung.
      2. Verifies touki.perf references BenchmarkDotNet.Diagnostics.Windows (the
         package that provides `-p ETW`); without it the profiler silently no-ops.
      3. Runs `dotnet run -c Release -f net481 --project touki.perf -- --filter
         <Filter> -p ETW --keepFiles`, which writes an .etl into
         BenchmarkDotNet.Artifacts/.
      4. Locates the newest .etl and prints the next-step filtrace commands already
         scoped to the benchmark process with --process, plus the net481 build-output
         --symbols directory for line-level attribution.

    Why --process matters: an .etl is a machine-wide capture. filtrace auto-scopes to
    the busiest process, but a noisy background app (antivirus, a VPN client) can win
    that race, so the printed commands pin --process explicitly. The cpu/threadtime/
    rank/lines/callers/heatmap verbs all accept it.

.PARAMETER Filter
    BenchmarkDotNet --filter glob selecting the benchmark(s) to profile, e.g.
    '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingle'. Profile one method at a
    time for a clean trace.

.PARAMETER Process
    Process-name substring the printed filtrace commands scope to with --process.
    Defaults to 'touki.perf' (the benchmark host).

.PARAMETER Tfm
    Target framework. ETW is the Framework path; defaults to net481. For net10 use
    EventPipe via tools/Profile-Benchmark.ps1 instead.

.PARAMETER Top
    Rows per ranking in the printed commands. Default 25.

.PARAMETER SkipRun
    Skip the benchmark run and analyze the newest existing .etl for -Filter. Use when
    you already have a fresh capture and only want the next-step commands.

.EXAMPLE
    ./tools/Capture-EtwTrace.ps1 -Filter '*MsBuildEnumeratePerf3.GlobEnumeratorExtGlobSingle'

.NOTES
    Native runtime-symbol resolution happens at analysis time in filtrace via its
    `--native-symbols` option, not in this capture script - see
    https://github.com/JeremyKuhne/filtrace.
#>
param(
    [Parameter(Mandatory)][string]$Filter,
    [string]$Process = "touki.perf",
    [string]$Tfm = "net481",
    [int]$Top = 25,
    [switch]$SkipRun
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot "BenchmarkDotNet.Artifacts"
$log = Join-Path $artifacts "etw-capture.log"

function Test-Elevated {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

if ($Tfm -eq "net10.0") {
    Write-Warning "net10 supports EventPipe, which needs no elevation - consider tools/Profile-Benchmark.ps1. Continuing with ETW anyway."
}

# ETW kernel sessions require Administrator. When not elevated, relaunch this script
# in an elevated window that shows the capture's LIVE progress (the child tees its
# output to the log; nothing is redirected away), then wait for it to finish.
if (-not (Test-Elevated)) {
    Write-Host "ETW capture needs Administrator; relaunching elevated (a UAC prompt will appear)." -ForegroundColor Yellow
    Write-Host "The elevated window shows live capture progress - watch it there; this window waits." -ForegroundColor Yellow

    $argList = @('-NoProfile', '-File', $PSCommandPath, '-Filter', $Filter, '-Process', $Process, '-Tfm', $Tfm, '-Top', $Top)
    if ($SkipRun) { $argList += '-SkipRun' }

    $proc = Start-Process pwsh -Verb RunAs -PassThru -Wait -ArgumentList $argList
    if ($proc.ExitCode -ne 0) {
        Write-Error "Elevated capture failed (exit $($proc.ExitCode)). See $log."
        exit $proc.ExitCode
    }

    # Mirror the elevated run's summary here so the result is visible in this window too.
    if (Test-Path $log) {
        Write-Host "`n--- capture log tail (full log: $log) ---" -ForegroundColor Cyan
        Get-Content $log -Tail 25
    }
    exit 0
}

# --- Elevated from here. ---
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

if (-not $SkipRun) {
    # Without BenchmarkDotNet.Diagnostics.Windows the `-p ETW` profiler silently
    # resolves to UnresolvedDiagnoser and no .etl is written - fail fast with guidance.
    $perfProj = Join-Path $repoRoot "touki.perf/touki.perf.csproj"
    if (-not (Select-String -Path $perfProj -Pattern "BenchmarkDotNet.Diagnostics.Windows" -Quiet)) {
        Write-Error "touki.perf does not reference BenchmarkDotNet.Diagnostics.Windows; -p ETW will no-op. Add the package first."
        exit 1
    }

    Write-Host "Capturing ETW trace: $Filter ($Tfm)..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        # Tee, do not redirect: the elevated window shows BenchmarkDotNet's live
        # progress while the run is also logged for the parent window to surface.
        dotnet run -c Release -f $Tfm --project touki.perf -- --filter $Filter -p ETW --keepFiles 2>&1 |
            Tee-Object -FilePath $log
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Benchmark run failed (exit $LASTEXITCODE). See $log."
            exit $LASTEXITCODE
        }
    }
    finally {
        Pop-Location
    }
}

$etl = Get-ChildItem -Path $artifacts -Filter '*.etl' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime | Select-Object -Last 1
if ($null -eq $etl) {
    Write-Error "No .etl found in $artifacts. Did the capture run?"
    exit 1
}

# The net481 build-output directory BenchmarkDotNet kept (--keepFiles); its embedded
# PDBs resolve managed frames to source lines for the `lines`/`heatmap` verbs.
$symbols = "artifacts/x64/Release/touki.perf/$Tfm/touki.perf-DefaultJob-1/bin/Release/$Tfm"
# filtrace is the standalone analyzer (github.com/JeremyKuhne/filtrace), published to
# NuGet. Install once with `dotnet tool install -g KlutzyNinja.Filtrace`; the printed
# commands below then run as `filtrace <verb> ...`.
$filtrace = "filtrace"

Write-Host "`nCaptured: $($etl.FullName)" -ForegroundColor Green
Write-Host "`nNext-step filtrace commands (scoped to '$Process'):" -ForegroundColor Green
Write-Host "  # who is in the capture (pick a --process target):"
Write-Host "  $filtrace processes `"$($etl.FullName)`""
Write-Host "  # CPU self-time hotspots:"
Write-Host "  $filtrace cpu `"$($etl.FullName)`" --process $Process --top $Top"
Write-Host "  # wall-clock (running + blocked) - near-equal to cpu means CPU-bound:"
Write-Host "  $filtrace threadtime `"$($etl.FullName)`" --process $Process --top $Top"
Write-Host "  # line-level inside a hot method:"
Write-Host "  $filtrace lines `"$($etl.FullName)`" --method <MethodName> --process $Process --symbols $symbols"
