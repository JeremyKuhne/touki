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

    The net481 ETW (.etl) half of the corpus is captured separately by the
    sibling capture-etw.ps1, which needs an elevated session (ETW kernel tracing
    requires administrator rights) and, unlike this script, does not re-freeze
    the parity oracle.

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
$coreFixtures = Join-Path $fixturesRoot '../tests/TraceQ.Core.Tests/Fixtures'
$artifacts = Join-Path $benchProject 'BenchmarkDotNet.Artifacts'

Write-Host 'Capturing the EventPipe CPU profile (BenchmarkDotNet)...'
if (Test-Path $artifacts)
{
    Remove-Item -Recurse -Force $artifacts
}

Push-Location $benchProject
try
{
    dotnet run -c Release -f net10.0 -- --filter '*HotLoop*' | Out-Host
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
# Run it under InvariantCulture so the oracle's N1 formatting always emits the
# '1234.5' decimal point (and ',' group separators) the parser expects, rather
# than a culture-specific decimal comma that would break regeneration.
$originalCulture = [System.Threading.Thread]::CurrentThread.CurrentCulture
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture
try
{
    $oracleOutput = & $oracle -Path $fixtureSpeedscope -Top 15 6>&1 |
        ForEach-Object { $_.ToString() -split "`r?`n" }
}
finally
{
    [System.Threading.Thread]::CurrentThread.CurrentCulture = $originalCulture
}

$golden = [ordered]@{
    source    = 'tools/Get-TraceHotspots.ps1 (frozen; see traceq/fixtures/oracles)'
    selfTime  = Get-OracleRows -Lines $oracleOutput -SectionMarker 'TOP SELF-TIME'
    inclusive = Get-OracleRows -Lines $oracleOutput -SectionMarker 'TOP INCLUSIVE-TIME'
}

$goldenPath = Join-Path $parityFixtures 'hotloop.oracle.json'
$golden | ConvertTo-Json -Depth 5 | Set-Content -Path $goldenPath -Encoding utf8
Write-Host "Oracle golden -> $goldenPath (self=$($golden.selfTime.Count) rows, inclusive=$($golden.inclusive.Count) rows)"

# Capture the allocation smoke trace (GC-verbose) for the allocation provider.
# AllocLoop runs a single bounded invocation, so its GCAllocationTick-bearing
# .nettrace stays small enough (well under 1 MB) to commit as the smoke fixture.
# The larger, richer captures used for performance work are regenerated on demand
# and attached to a release rather than committed.
Write-Host 'Capturing the allocation smoke trace (GC-verbose)...'
Push-Location $benchProject
try
{
    dotnet run -c Release -f net10.0 -- --filter '*AllocLoop*' | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "Allocation capture failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}

$allocTrace = Get-ChildItem -Recurse $artifacts -Filter '*AllocLoop*.nettrace' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $allocTrace)
{
    throw "No allocation .nettrace was produced under $artifacts."
}

$fixtureAlloc = Join-Path $coreFixtures 'alloc.nettrace'
Copy-Item $allocTrace.FullName $fixtureAlloc -Force
Write-Host "Allocation fixture -> $fixtureAlloc ($([math]::Round($allocTrace.Length / 1KB)) KB)"

# Capture the JIT smoke trace for the JIT-stats provider. JitLoop calls each of
# its methods once, so a single Monitoring invocation captures the complete JIT
# picture (every method is jitted on first call) while keeping the trace tiny.
Write-Host 'Capturing the JIT smoke trace...'
Push-Location $benchProject
try
{
    dotnet run -c Release -f net10.0 -- --filter '*JitLoop*' | Out-Host
    if ($LASTEXITCODE -ne 0)
    {
        throw "JIT capture failed with exit code $LASTEXITCODE."
    }
}
finally
{
    Pop-Location
}

$jitTrace = Get-ChildItem -Recurse $artifacts -Filter '*JitLoop*.nettrace' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
if ($null -eq $jitTrace)
{
    throw "No JIT .nettrace was produced under $artifacts."
}

$fixtureJit = Join-Path $coreFixtures 'jit.nettrace'
Copy-Item $jitTrace.FullName $fixtureJit -Force
Write-Host "JIT fixture -> $fixtureJit ($([math]::Round($jitTrace.Length / 1KB)) KB)"

# The net481 ETW (.etl) half is captured separately by capture-etw.ps1: it needs
# an elevated session, and unlike this script it does not re-freeze the parity
# oracle, so the two halves regenerate on independent cadences.
Write-Host 'Done.'
