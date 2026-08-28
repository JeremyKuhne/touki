<#
.SYNOPSIS
    Verify that KlutzyNinja.Touki brings in its analyzer package.

.DESCRIPTION
    Checks the packed analyzer assets and Touki dependency metadata, verifies
    that the repository's project reference is build-only, restores a temporary
    project from the packed KlutzyNinja.Touki package only, and then builds
    source that triggers TOUKI0001.

.PARAMETER PackageDirectory
    Directory containing matching KlutzyNinja.Touki and
    KlutzyNinja.Touki.Analyzers packages.

.EXAMPLE
    pwsh tools/Test-AnalyzerPackageDependency.ps1 -PackageDirectory ./artifacts/packages
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$PackageDirectory
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$packageDirectoryPath = (Resolve-Path $PackageDirectory).Path
$toukiPackages = @(
    Get-ChildItem -Path $packageDirectoryPath -Filter 'KlutzyNinja.Touki.*.nupkg'
        | Where-Object Name -Match '^KlutzyNinja\.Touki\.\d.*\.nupkg$'
)
$analyzerPackages = @(
    Get-ChildItem -Path $packageDirectoryPath -Filter 'KlutzyNinja.Touki.Analyzers.*.nupkg'
)

if ($toukiPackages.Count -ne 1) {
    throw "Expected one KlutzyNinja.Touki package, found $($toukiPackages.Count)."
}

if ($analyzerPackages.Count -ne 1) {
    throw "Expected one KlutzyNinja.Touki.Analyzers package, found $($analyzerPackages.Count)."
}

$analyzerPrefix = 'KlutzyNinja.Touki.Analyzers.'
$analyzerVersion = $analyzerPackages[0].BaseName.Substring($analyzerPrefix.Length)
$toukiPrefix = 'KlutzyNinja.Touki.'
$toukiVersion = $toukiPackages[0].BaseName.Substring($toukiPrefix.Length)

Add-Type -AssemblyName System.IO.Compression.FileSystem
$expectedAnalyzerAssets = @(
    'analyzers/dotnet/cs/touki.analyzers.codefixes.dll'
    'analyzers/dotnet/cs/touki.analyzers.dll'
)
$toukiArchive = [System.IO.Compression.ZipFile]::OpenRead($toukiPackages[0].FullName)
$analyzerArchive = [System.IO.Compression.ZipFile]::OpenRead($analyzerPackages[0].FullName)

try {
    $actualAnalyzerAssets = @(
        $analyzerArchive.Entries
            | Where-Object { $_.FullName.EndsWith('.dll', [System.StringComparison]::OrdinalIgnoreCase) }
            | ForEach-Object FullName
            | Sort-Object
    )
    $assetDifferences = @(Compare-Object $expectedAnalyzerAssets $actualAnalyzerAssets)
    if ($assetDifferences.Count -ne 0) {
        throw "Unexpected analyzer assembly payload:`n$($assetDifferences | Out-String)"
    }

    $versionParts = $analyzerVersion.Split('-', 2)[0].Split('.')
    $expectedAssemblyVersion = "$($versionParts[0]).0.0.0"
    $expectedFileVersion = "$($versionParts[0]).$($versionParts[1]).$($versionParts[2]).0"
    $assemblyExtractDirectory = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ([System.IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory -Path $assemblyExtractDirectory | Out-Null
    try {
        foreach ($asset in $expectedAnalyzerAssets) {
            $entry = $analyzerArchive.GetEntry($asset)
            $destination = Join-Path $assemblyExtractDirectory ([System.IO.Path]::GetFileName($asset))
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination)

            $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($destination).Version.ToString()
            $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($destination)
            $productVersionMatches = $versionInfo.ProductVersion -eq $analyzerVersion `
                -or $versionInfo.ProductVersion.StartsWith(
                    "$analyzerVersion+",
                    [System.StringComparison]::Ordinal)
            $hasWrongAssemblyVersion = $assemblyVersion -ne $expectedAssemblyVersion
            $hasWrongFileVersion = $versionInfo.FileVersion -ne $expectedFileVersion

            if ($hasWrongAssemblyVersion -or $hasWrongFileVersion -or !$productVersionMatches) {
                throw "'$asset' does not carry analyzer version $analyzerVersion."
            }
        }
    }
    finally {
        Remove-Item -Recurse -Force $assemblyExtractDirectory -ErrorAction SilentlyContinue
    }

    $analyzerLibraryAssets = @(
        $analyzerArchive.Entries
            | Where-Object FullName -Match '^(lib|ref|runtimes)/'
    )
    if ($analyzerLibraryAssets.Count -ne 0) {
        throw 'KlutzyNinja.Touki.Analyzers contains compile-time or runtime library assets.'
    }

    $thirdPartyNotice = $analyzerArchive.GetEntry('THIRD-PARTY-NOTICES.TXT')
    if ($null -eq $thirdPartyNotice) {
        throw 'KlutzyNinja.Touki.Analyzers is missing THIRD-PARTY-NOTICES.TXT.'
    }

    $thirdPartyNoticeReader = [System.IO.StreamReader]::new($thirdPartyNotice.Open())
    try {
        $thirdPartyNoticeText = $thirdPartyNoticeReader.ReadToEnd()
    }
    finally {
        $thirdPartyNoticeReader.Dispose()
    }

    if ($thirdPartyNoticeText -notmatch 'License notice for \.NET Compiler Platform \("Roslyn"\)') {
        throw 'KlutzyNinja.Touki.Analyzers is missing the Roslyn license notice.'
    }

    $embeddedAnalyzerAssets = @(
        $toukiArchive.Entries
            | Where-Object FullName -Match '^analyzers/'
    )
    if ($embeddedAnalyzerAssets.Count -ne 0) {
        throw 'KlutzyNinja.Touki still embeds analyzer assets.'
    }

    $toukiNuspecEntry = $toukiArchive.GetEntry('KlutzyNinja.Touki.nuspec')
    $analyzerNuspecEntry = $analyzerArchive.GetEntry('KlutzyNinja.Touki.Analyzers.nuspec')
    if ($null -eq $toukiNuspecEntry -or $null -eq $analyzerNuspecEntry) {
        throw 'A package is missing its nuspec.'
    }

    $toukiNuspecReader = [System.IO.StreamReader]::new($toukiNuspecEntry.Open())
    $analyzerNuspecReader = [System.IO.StreamReader]::new($analyzerNuspecEntry.Open())
    try {
        [xml]$toukiNuspec = $toukiNuspecReader.ReadToEnd()
        [xml]$analyzerNuspec = $analyzerNuspecReader.ReadToEnd()
    }
    finally {
        $toukiNuspecReader.Dispose()
        $analyzerNuspecReader.Dispose()
    }

    $toukiNamespace = [System.Xml.XmlNamespaceManager]::new($toukiNuspec.NameTable)
    $toukiNamespace.AddNamespace('n', $toukiNuspec.DocumentElement.NamespaceURI)
    $dependencyGroups = @($toukiNuspec.SelectNodes('/n:package/n:metadata/n:dependencies/n:group', $toukiNamespace))
    if ($dependencyGroups.Count -eq 0) {
        throw 'The Touki package has no target framework dependency groups.'
    }

    foreach ($dependencyGroup in $dependencyGroups) {
        $analyzerDependencies = @(
            $dependencyGroup.SelectNodes(
                'n:dependency[@id="KlutzyNinja.Touki.Analyzers"]',
                $toukiNamespace)
        )
        if ($analyzerDependencies.Count -ne 1) {
            $targetFramework = $dependencyGroup.GetAttribute('targetFramework')
            throw "Expected one analyzer dependency for '$targetFramework', found $($analyzerDependencies.Count)."
        }

        $dependency = $analyzerDependencies[0]
        $hasWrongVersion = $dependency.GetAttribute('version') -ne $analyzerVersion
        $excludedAssets = @(
            $dependency.GetAttribute('exclude').Split(
                ',',
                [System.StringSplitOptions]::RemoveEmptyEntries)
        )
        $requiredExcludedAssets = @('Runtime', 'Compile', 'Build', 'Native', 'BuildTransitive')
        $missingExclusions = @($requiredExcludedAssets | Where-Object { $excludedAssets -notcontains $_ })
        $hasWrongAssetPolicy = $missingExclusions.Count -ne 0 -or $excludedAssets -contains 'Analyzers'
        if ($hasWrongVersion -or $hasWrongAssetPolicy) {
            throw 'Touki has an incorrect analyzer package version or asset inclusion policy.'
        }
    }

    $analyzerNamespace = [System.Xml.XmlNamespaceManager]::new($analyzerNuspec.NameTable)
    $analyzerNamespace.AddNamespace('n', $analyzerNuspec.DocumentElement.NamespaceURI)
    $analyzerPackageDependencies = @(
        $analyzerNuspec.SelectNodes('/n:package/n:metadata/n:dependencies//n:dependency', $analyzerNamespace)
    )
    if ($analyzerPackageDependencies.Count -ne 0) {
        throw 'KlutzyNinja.Touki.Analyzers must not expose build-time dependencies to consumers.'
    }
}
finally {
    $toukiArchive.Dispose()
    $analyzerArchive.Dispose()
}

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$toukiProjectPath = Join-Path $repositoryRoot 'touki/touki.csproj'
$referenceOutput = & dotnet msbuild $toukiProjectPath `
    -target:ResolveReferences `
    -getItem:ReferenceCopyLocalPaths `
    -getItem:Analyzer `
    -p:Configuration=Release `
    -p:Platform=AnyCPU `
    -p:Platforms=AnyCPU `
    -p:TargetFramework=net10.0 2>&1 | Out-String
$referenceExitCode = $LASTEXITCODE

if ($referenceExitCode -ne 0) {
    Write-Host $referenceOutput
    throw "Resolving Touki project references failed with exit code $referenceExitCode."
}

$resolvedReferences = $referenceOutput | ConvertFrom-Json
$copyLocalToukiAnalyzers = @(
    $resolvedReferences.Items.ReferenceCopyLocalPaths
        | Where-Object Identity -Match '[\\/]touki\.analyzers\.dll$'
)
$loadedToukiAnalyzers = @(
    $resolvedReferences.Items.Analyzer
        | Where-Object Identity -Match '[\\/]touki\.analyzers\.dll$'
)

if ($copyLocalToukiAnalyzers.Count -ne 0) {
    throw 'The Touki project reference copies touki.analyzers.dll into runtime output.'
}

if ($loadedToukiAnalyzers.Count -ne 1) {
    throw "Expected one build-only Touki analyzer reference, found $($loadedToukiAnalyzers.Count)."
}

$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null

try {
    $projectReferenceDirectory = Join-Path $temporaryDirectory 'project-reference'
    $packageReferenceDirectory = Join-Path $temporaryDirectory 'package-reference'
    New-Item -ItemType Directory -Path $projectReferenceDirectory | Out-Null
    New-Item -ItemType Directory -Path $packageReferenceDirectory | Out-Null

    $utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)
    $escapedToukiProjectPath = [System.Security.SecurityElement]::Escape($toukiProjectPath)
    $escapedPackageDirectoryPath = [System.Security.SecurityElement]::Escape($packageDirectoryPath)

    $projectReferenceProjectPath = Join-Path $projectReferenceDirectory 'ProjectReferenceProbe.csproj'
    $projectReferenceSourcePath = Join-Path $projectReferenceDirectory 'Program.cs'
    $projectReferenceProject = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$escapedToukiProjectPath"
                      AdditionalProperties="Platform=AnyCPU;Platforms=AnyCPU" />
  </ItemGroup>
</Project>
"@
    [System.IO.File]::WriteAllText($projectReferenceProjectPath, $projectReferenceProject, $utf8WithoutBom)
    [System.IO.File]::WriteAllText(
        $projectReferenceSourcePath,
        'System.Console.WriteLine(typeof(Touki.Value));',
        $utf8WithoutBom)

    $projectReferenceBuildOutput = & dotnet build $projectReferenceProjectPath `
        --configuration Release 2>&1 | Out-String
    $projectReferenceBuildExitCode = $LASTEXITCODE
    if ($projectReferenceBuildExitCode -ne 0) {
        Write-Host $projectReferenceBuildOutput
        throw "Project-reference consumer build failed with exit code $projectReferenceBuildExitCode."
    }

    $projectReferenceOutputDirectory = Join-Path $projectReferenceDirectory 'bin/Release/net10.0'
    $projectReferenceAnalyzerFiles = @(
        Get-ChildItem $projectReferenceOutputDirectory -File
            | Where-Object Name -Match '^touki\.analyzers\.'
    )
    if ($projectReferenceAnalyzerFiles.Count -ne 0) {
        throw 'A project-reference consumer copied Touki analyzer files into runtime output.'
    }

    $projectReferenceDepsPath = Join-Path $projectReferenceOutputDirectory 'ProjectReferenceProbe.deps.json'
    $projectReferenceDeps = [System.IO.File]::ReadAllText($projectReferenceDepsPath)
    if ($projectReferenceDeps -match 'touki\.analyzers|KlutzyNinja\.Touki\.Analyzers') {
        throw 'A project-reference consumer recorded the Touki analyzer as a runtime dependency.'
    }

    $projectReferenceAssetsPath = Join-Path $projectReferenceDirectory 'obj/project.assets.json'
    $projectReferenceAssets = Get-Content $projectReferenceAssetsPath -Raw | ConvertFrom-Json -Depth 100
    $projectReferenceAnalyzerCount = 0
    foreach ($target in $projectReferenceAssets.targets.PSObject.Properties) {
        foreach ($library in $target.Value.PSObject.Properties) {
            if ($library.Name -notmatch '^KlutzyNinja\.Touki\.Analyzers/') {
                continue
            }

            $projectReferenceAnalyzerCount++
            $compileAssets = $library.Value.PSObject.Properties['compile']
            $runtimeAssets = $library.Value.PSObject.Properties['runtime']
            $compileAssetNames = @(
                if ($null -ne $compileAssets) {
                    $compileAssets.Value.PSObject.Properties.Name
                }
            )
            $runtimeAssetNames = @(
                if ($null -ne $runtimeAssets) {
                    $runtimeAssets.Value.PSObject.Properties.Name
                }
            )
            $realCompileAssets = @($compileAssetNames | Where-Object { $_ -notmatch '/_\._$' })
            $realRuntimeAssets = @($runtimeAssetNames | Where-Object { $_ -notmatch '/_\._$' })
            if ($realCompileAssets.Count -ne 0 -or $realRuntimeAssets.Count -ne 0) {
                throw 'A project-reference consumer resolved real compile or runtime assets from the Touki analyzer.'
            }
        }
    }

    if ($projectReferenceAnalyzerCount -ne 1) {
        throw "Expected one downstream analyzer project entry, found $projectReferenceAnalyzerCount."
    }

    $projectPath = Join-Path $packageReferenceDirectory 'AnalyzerPackageProbe.csproj'
    $sourcePath = Join-Path $packageReferenceDirectory 'Probe.cs'
    $nugetConfigPath = Join-Path $packageReferenceDirectory 'NuGet.Config'
    $restorePackagesPath = Join-Path $packageReferenceDirectory 'packages'

    $project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <WarningsAsErrors>TOUKI0001</WarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="KlutzyNinja.Touki" Version="$toukiVersion" />
  </ItemGroup>
</Project>
"@
    $nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <packageSources>
        <clear />
        <add key="local" value="$escapedPackageDirectoryPath" />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
    <packageSourceMapping>
        <packageSource key="local">
            <package pattern="KlutzyNinja.Touki" />
            <package pattern="KlutzyNinja.Touki.Analyzers" />
        </packageSource>
        <packageSource key="nuget.org">
            <package pattern="*" />
        </packageSource>
    </packageSourceMapping>
</configuration>
"@
    $source = @'
namespace AnalyzerPackageProbe;

public static class Probe
{
    public static bool IsNull(object value) => value == null;
}
'@

    [System.IO.File]::WriteAllText($projectPath, $project, $utf8WithoutBom)
    [System.IO.File]::WriteAllText($nugetConfigPath, $nugetConfig, $utf8WithoutBom)
    [System.IO.File]::WriteAllText($sourcePath, $source, $utf8WithoutBom)

    $restoreOutput = & dotnet restore $projectPath `
        --packages $restorePackagesPath `
        --configfile $nugetConfigPath `
        --no-cache 2>&1 | Out-String
    $restoreExitCode = $LASTEXITCODE

    if ($restoreExitCode -ne 0) {
        Write-Host $restoreOutput
        throw "Consumer restore failed with exit code $restoreExitCode."
    }

    $assetsPath = Join-Path $packageReferenceDirectory 'obj/project.assets.json'
    $assets = Get-Content $assetsPath -Raw | ConvertFrom-Json -Depth 100
    $analyzerLibraryKey = "KlutzyNinja.Touki.Analyzers/$analyzerVersion"
    $analyzerLibrary = $assets.libraries.PSObject.Properties[$analyzerLibraryKey]
    if ($null -eq $analyzerLibrary -or $analyzerLibrary.Value.type -ne 'package') {
        throw 'Consumer restore did not resolve KlutzyNinja.Touki.Analyzers as a package dependency.'
    }

    $analyzerMetadataPath = Join-Path `
        $restorePackagesPath `
        "klutzyninja.touki.analyzers/$($analyzerVersion.ToLowerInvariant())/.nupkg.metadata"
    $analyzerMetadata = Get-Content $analyzerMetadataPath -Raw | ConvertFrom-Json
    $pathSeparators = [char[]]@(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $restoredSource = [System.IO.Path]::GetFullPath([string]$analyzerMetadata.source).TrimEnd($pathSeparators)
    $expectedSource = $packageDirectoryPath.TrimEnd($pathSeparators)
    $pathComparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }

    if (![string]::Equals($restoredSource, $expectedSource, $pathComparison)) {
        throw "Consumer restored the analyzer package from '$restoredSource', not the local feed."
    }

    $actualRestoredAnalyzerAssets = @(
        $analyzerLibrary.Value.files
            | Where-Object { $_ -match '^analyzers/.+\.dll$' }
            | Sort-Object -Unique
    )
    $restoredAssetDifferences = @(Compare-Object $expectedAnalyzerAssets $actualRestoredAnalyzerAssets)
    if ($restoredAssetDifferences.Count -ne 0) {
        throw "Consumer restore produced unexpected analyzer assets:`n$($restoredAssetDifferences | Out-String)"
    }

    $buildOutput = & dotnet build $projectPath --no-restore 2>&1 | Out-String
    $buildExitCode = $LASTEXITCODE

    if ($buildExitCode -eq 0 -or $buildOutput -notmatch '\bTOUKI0001\b') {
        Write-Host $buildOutput
        throw 'Referencing KlutzyNinja.Touki did not produce the expected TOUKI0001 diagnostic.'
    }

    Write-Host "KlutzyNinja.Touki $toukiVersion transitively activated TOUKI0001."
}
finally {
    Remove-Item -Recurse -Force $temporaryDirectory -ErrorAction SilentlyContinue
}