<#
.SYNOPSIS
    Regenerates the traceq EventPipe fixture corpus and its frozen parity oracle.

.DESCRIPTION
    Captures a net10 EventPipe CPU profile of the HotLoopBench benchmark, copies
    the speedscope export into the parity-test fixtures, and freezes the legacy
    oracle's (Get-TraceHotspots.ps1) self- and inclusive-time rankings as a JSON
    golden the parity tests compare against. Run this on a Windows machine with
    the .NET 10 SDK when the benchmark, TraceEvent, or BenchmarkDotNet version
    moves; it is not part of the build/test loop.

    The net481 ETW (.etl) half of the corpus is deferred (it needs an elevated
    session) and is added here when captured.

.NOTES
    The committed speedscope is the in-repo smoke fixture. The full .nettrace is
    left under BenchmarkDotNet.Artifacts (gitignored) and is regenerated on
    demand; it is too large for the repo and is destined for a release asset.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$fixturesRoot = $PSScriptRoot
$benchProject = Join-Path $fixturesRoot 'HotLoopBench'
$oracle = Join-Path $fixturesRoot 'oracles/Get-TraceHotspots.ps1'
$parityFixtures = Join-Path $fixturesRoot '../tests/TraceQ.Parity.Tests/Fixtures'
$artifacts = Join-Path $benchProject 'BenchmarkDotNet.Artifacts'

Write-Host 'Capturing the EventPipe CPU profile (BenchmarkDotNet)...'
if (Test-Path $artifacts)
{
    Remove-Item -Recurse -Force $artifacts
}

Push-Location $benchProject
try
{
    dotnet run -c Release -- --filter '*HotLoop*' | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "Benchmark capture failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}

$speedscope = Get-ChildItem -Recurse $artifacts -Filter '*.speedscope.json' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $speedscope)
{
    throw "No speedscope file was produced under $artifacts."
}

$fixtureSpeedscope = Join-Path $parityFixtures 'hotloop.speedscope.json'
Copy-Item $speedscope.FullName $fixtureSpeedscope -Force
Write-Host "Fixture speedscope -> $fixtureSpeedscope ($([math]::Round($speedscope.Length / 1KB)) KB)"

# Parse one ranking section of the oracle's text output into ordered rows.
function Get-OracleRows
{
    param([string[]]$Lines, [string]$SectionMarker)

    $rows = [System.Collections.Generic.List[object]]::new()
    $inSection = $false
    foreach ($raw in $Lines)
    {
        $line = $raw.Trim()
        if ($line -match '^=====')
        {
            $inSection = $line -match [regex]::Escape($SectionMarker)
            continue
        }

        if ($inSection -and $line -match '^([\d,]+\.\d+)\s+ms\s+([\d.]+)%\s+(.+?)$')
        {
            $rows.Add([ordered]@{
                frame          = $Matches[3]
                milliseconds   = [double]($Matches[1] -replace ',', '')
                percentOfScope = [double]$Matches[2]
            })
        }
    }

    return $rows
}

Write-Host 'Freezing the legacy oracle rankings...'
# The oracle writes its section headers with Write-Host (the Information stream)
# and its rows to the success stream; merge stream 6 so the parser sees both, and
# split embedded newlines (Write-Host prefixes some headers with a newline).
$oracleOutput = & $oracle -Path $fixtureSpeedscope -Top 15 6>&1 |
    ForEach-Object { $_.ToString() -split "`r?`n" }

$golden = [ordered]@{
    source    = 'tools/Get-TraceHotspots.ps1 (frozen; see traceq/fixtures/oracles)'
    selfTime  = Get-OracleRows -Lines $oracleOutput -SectionMarker 'TOP SELF-TIME'
    inclusive = Get-OracleRows -Lines $oracleOutput -SectionMarker 'TOP INCLUSIVE-TIME'
}

$goldenPath = Join-Path $parityFixtures 'hotloop.oracle.json'
$golden | ConvertTo-Json -Depth 5 | Set-Content -Path $goldenPath -Encoding utf8
Write-Host "Oracle golden -> $goldenPath (self=$($golden.selfTime.Count) rows, inclusive=$($golden.inclusive.Count) rows)"
Write-Host 'Done.'
