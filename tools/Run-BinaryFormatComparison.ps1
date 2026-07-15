<#
.SYNOPSIS
    Run the touki BinaryFormattedObject benchmarks against an exact clean build
    of JeremyKuhne/binaryformat.

.DESCRIPTION
    Creates a fresh detached checkout of binaryformat commit
    aaa1dd1bf7ee8ce626b82c3c55343dfee4a71743 outside the touki repository,
    verifies the checkout is clean, builds the net8.0 library in Release, and
    exposes the resulting assembly to touki.perf through the
    BinaryFormatUpstreamAssembly environment-backed MSBuild property.

    The environment property is required because BenchmarkDotNet creates child
    projects and build processes. A command-line /p: property on the outer
    dotnet run invocation does not flow into those child builds.

.PARAMETER TargetFramework
    Modern .NET target to benchmark. The upstream project targets net8.0 and
    cannot be referenced by the net481 benchmark host.

.PARAMETER Category
    Benchmark category to run: EndToEnd, ParseOnly, or All.

.PARAMETER Job
    BenchmarkDotNet job. Default uses the adaptive default job; Medium and Short
    use the corresponding fixed BenchmarkDotNet jobs.

.PARAMETER Filter
    BenchmarkDotNet filter glob. Defaults to the full BinaryFormattedObjectPerf
    class.

.EXAMPLE
    ./tools/Run-BinaryFormatComparison.ps1 -TargetFramework net10.0 -Category EndToEnd

.EXAMPLE
    ./tools/Run-BinaryFormatComparison.ps1 -TargetFramework net11.0 -Category ParseOnly -Job Medium
#>
param(
    [ValidateSet("net10.0", "net11.0")]
    [string]$TargetFramework = "net10.0",

    [ValidateSet("EndToEnd", "ParseOnly", "All")]
    [string]$Category = "EndToEnd",

    [ValidateSet("Default", "Medium", "Short")]
    [string]$Job = "Default",

    [string]$Filter = "*BinaryFormattedObjectPerf*"
)

$ErrorActionPreference = "Stop"
$commit = "aaa1dd1bf7ee8ce626b82c3c55343dfee4a71743"
$repository = "https://github.com/JeremyKuhne/binaryformat.git"
$repoRoot = Split-Path -Parent $PSScriptRoot
$checkout = Join-Path ([System.IO.Path]::GetTempPath()) "touki-binaryformat-$commit-$([Guid]::NewGuid().ToString('N'))"
$previousAssembly = $env:BinaryFormatUpstreamAssembly

try {
    Write-Host "Cloning binaryformat commit $commit..." -ForegroundColor Cyan
    git clone --quiet --no-checkout $repository $checkout
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to clone $repository (exit $LASTEXITCODE)."
    }

    git -C $checkout checkout --quiet --detach $commit
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to check out binaryformat commit $commit (exit $LASTEXITCODE)."
    }

    $actualCommitOutput = git -C $checkout rev-parse HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read the binaryformat checkout revision (exit $LASTEXITCODE)."
    }

    $actualCommit = $actualCommitOutput.Trim()
    $status = git -C $checkout status --porcelain=v1
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to read the binaryformat checkout status (exit $LASTEXITCODE)."
    }

    if ($actualCommit -ne $commit -or $status) {
        throw "The binaryformat checkout is not the expected clean commit $commit."
    }

    $project = Join-Path $checkout "src/binaryformat/binaryformat.csproj"
    Push-Location $repoRoot
    try {
        $sdkVersion = dotnet --version
        if ($LASTEXITCODE -ne 0) {
            throw "Unable to resolve the touki .NET SDK (exit $LASTEXITCODE)."
        }

        Write-Host "Using .NET SDK $sdkVersion from touki/global.json." -ForegroundColor DarkGray
        Write-Host "Building exact upstream source in Release..." -ForegroundColor Cyan
        dotnet build $project -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "The binaryformat Release build failed (exit $LASTEXITCODE)."
        }
    }
    finally {
        Pop-Location
    }

    $assembly = Join-Path $checkout "artifacts/x64/Release/binaryformat/net8.0/binaryformat.dll"
    if (-not (Test-Path -LiteralPath $assembly)) {
        throw "The expected upstream assembly was not produced at '$assembly'."
    }

    $assemblyHash = (Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash
    Write-Host "Upstream assembly SHA-256: $assemblyHash" -ForegroundColor DarkGray

    $env:BinaryFormatUpstreamAssembly = $assembly
    $benchmarkArguments = @("--filter", $Filter)
    if ($Category -ne "All") {
        $benchmarkArguments += @("--allCategories", $Category)
    }

    if ($Job -ne "Default") {
        $benchmarkArguments += @("--job", $Job.ToLowerInvariant())
    }

    Write-Host "Running $Category comparison on $TargetFramework..." -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        dotnet run -c Release -f $TargetFramework --project touki.perf -- @benchmarkArguments
        if ($LASTEXITCODE -ne 0) {
            throw "The benchmark run failed (exit $LASTEXITCODE)."
        }
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:BinaryFormatUpstreamAssembly = $previousAssembly
    if (Test-Path -LiteralPath $checkout) {
        Remove-Item -LiteralPath $checkout -Recurse -Force
    }
}