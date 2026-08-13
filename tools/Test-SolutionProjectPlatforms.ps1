<#
.SYNOPSIS
    Verify that every project advertises the platform mapped to it by a solution.

.DESCRIPTION
    Visual Studio skips a project when its solution platform mapping selects a
    platform that is absent from the project's evaluated Platforms property.
    Command-line solution builds do not expose this mismatch reliably and may
    build the project anyway.

    This script reads each project mapping from a .slnx file, asks MSBuild for
    the project's evaluated Platforms property, and fails on any mismatch.

.PARAMETER SolutionPath
    Path to the .slnx file to validate.

.EXAMPLE
    pwsh tools/Test-SolutionProjectPlatforms.ps1 -SolutionPath ./touki.slnx
#>
[CmdletBinding()]
param(
    [string]$SolutionPath = './touki.slnx'
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$solutionFile = Get-Item $SolutionPath
[xml]$solution = Get-Content -Raw $solutionFile.FullName
$solutionDirectory = $solutionFile.DirectoryName
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($project in $solution.SelectNodes('//Project')) {
    $projectPath = Join-Path $solutionDirectory ([string]$project.Path)
    $platformOutput = & dotnet msbuild $projectPath -getProperty:Platforms -nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Could not evaluate Platforms for '$projectPath'."
    }

    [string[]]$platforms = @(
        ([string]$platformOutput).Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
        | ForEach-Object { $_.Trim() }
    )

    foreach ($mapping in $project.Platform) {
        $mappedPlatform = [string]$mapping.Project
        if ([string]::IsNullOrWhiteSpace($mappedPlatform)) {
            continue
        }

        if ($platforms -notcontains $mappedPlatform) {
            $failures.Add(
                "'$($project.Path)' maps to '$mappedPlatform' but advertises Platforms='$($platforms -join ';')'.")
        }
        else {
            Write-Host "OK: $($project.Path) -> $mappedPlatform"
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Error "Solution project-platform mappings are invalid:`n$($failures -join "`n")"
}

Write-Host "All solution project-platform mappings are valid."
