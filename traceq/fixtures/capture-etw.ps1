<#
.SYNOPSIS
    Captures the net481 ETW (.etl) half of the traceq fixture corpus.

.DESCRIPTION
    Runs the HotLoopBench `EtwLoop` benchmark under BenchmarkDotNet's ETW
    profiler (net481, with the context-switch kernel keywords the ThreadTime
    view needs), copies the resulting `.etl` into the core test fixtures, and
    pre-converts it to `.etlx` for the cross-machine hand-off (O1) spike.

    This is split out from make-fixtures.ps1 on purpose: ETW kernel tracing
    needs an elevated session, whereas the EventPipe half does not, and the
    EventPipe half also re-freezes the parity oracle (a step that should run
    only when the CPU benchmark itself changes). Capturing the ETW half is
    therefore an independent, elevation-only operation.

.NOTES
    Run from an administrator terminal on a Windows machine with the .NET 10
    SDK and the net481 targeting pack. The committed `.etl` / `.etlx` are the
    in-repo smoke fixtures; larger captures are regenerated on demand.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$fixturesRoot = $PSScriptRoot
$benchProject = Join-Path $fixturesRoot 'HotLoopBench'
$coreFixtures = Join-Path $fixturesRoot '../tests/TraceQ.Core.Tests/Fixtures'
$artifacts = Join-Path $benchProject 'BenchmarkDotNet.Artifacts'

# ETW kernel tracing requires administrator rights; fail fast with a clear
# message rather than letting BenchmarkDotNet's validator reject the run.
$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator))
{
    throw 'Not elevated: ETW capture needs an administrator terminal. Re-run this script elevated.'
}

Write-Host 'Capturing the net481 ETW profile (elevated)...'
Push-Location $benchProject
try
{
    # The host runs on net10; the EtwCaptureConfig pins the profiled job to net481.
    dotnet run -c Release -f net10.0 -- --filter '*EtwLoop*' | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "ETW capture failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}

$etlTrace = Get-ChildItem -Recurse $artifacts -Filter '*EtwLoop*.etl' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $etlTrace)
{
    throw "No ETW .etl was produced under $artifacts."
}

$fixtureEtl = Join-Path $coreFixtures 'etw.etl'
Copy-Item $etlTrace.FullName $fixtureEtl -Force
Write-Host "ETW fixture -> $fixtureEtl ($([math]::Round($etlTrace.Length / 1KB)) KB)"

# Pre-convert the .etl to .etlx on Windows; the O1 spike commits this .etlx and
# reads it off Windows to settle whether the cross-machine hand-off holds.
$fixtureEtlx = Join-Path $coreFixtures 'etw.etlx'
Push-Location $benchProject
try
{
    dotnet run -c Release -f net10.0 -- convert $fixtureEtl $fixtureEtlx | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "ETL -> ETLX conversion failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}
Write-Host "ETLX fixture -> $fixtureEtlx ($([math]::Round((Get-Item $fixtureEtlx).Length / 1KB)) KB)"

Write-Host 'Done.'
