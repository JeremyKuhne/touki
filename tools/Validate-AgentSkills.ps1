<#
.SYNOPSIS
    Validate the mixed skill catalog used by this repository.

.DESCRIPTION
    Runs the validator bundled with the vendored manage-skills core over all
    skills, then runs its strict portfolio mode over only the commons-vendored
    portable cores. Also validates local overlay pins, relationship targets and
    cycles, and the inventory/category labels in .agents/skills/README.md.

    Exact vendored payloads remain owned by their source repositories. This
    wrapper adds Touki's consumer-side checks without modifying the bundled
    validator.

.EXAMPLE
    pwsh tools/Validate-AgentSkills.ps1
#>

#Requires -Version 7.0
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$SkillsRoot = Join-Path $RepoRoot '.agents/skills'
$CatalogPath = Join-Path $SkillsRoot 'README.md'
$McpPath = Join-Path $RepoRoot '.vscode/mcp.json'
$BundledValidator = Join-Path $SkillsRoot 'manage-skills/scripts/Validate-Skills.ps1'
$CommonsRepo = 'https://github.com/JeremyKuhne/agent-skills'
$FiltraceRepo = 'https://github.com/JeremyKuhne/filtrace'
$Errors = [System.Collections.Generic.List[string]]::new()

function Add-Error([string] $Message) {
    $Errors.Add($Message) | Out-Null
}

function Get-Scalar([string] $Text, [string] $Name, [bool] $Indented = $false) {
    $indent = if ($Indented) { '\s+' } else { '' }
    $match = [regex]::Match($Text, "(?m)^${indent}$([regex]::Escape($Name)):\s*(.*?)\s*$")
    if ($match.Success) {
        return $match.Groups[1].Value.Trim([char[]]@("'", '"'))
    }
    return ''
}

function Test-MetadataIndentation([string] $Text, [string] $SkillName) {
    $lines = $Text -split "`r?`n"
    $metadataIndex = [Array]::IndexOf($lines, 'metadata:')
    if ($metadataIndex -lt 0) { return }

    $indents = [System.Collections.Generic.HashSet[int]]::new()
    for ($i = $metadataIndex + 1; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        if ($line -notmatch '^\s') { break }
        if ($line.TrimStart().StartsWith('#')) { continue }
        $indents.Add(([regex]::Match($line, '^\s+').Length)) | Out-Null
    }

    if ($indents.Count -gt 1) {
        Add-Error ".agents/skills/$SkillName/SKILL.md metadata fields must use one indentation depth."
    }
}

function Invoke-BundledValidator([string[]] $Paths, [switch] $Strict) {
    $arguments = @('-NoProfile', '-File', $BundledValidator) + $Paths
    if ($Strict) { $arguments += '-RequirePortfolioMetadata' }
    & pwsh @arguments
    if ($LASTEXITCODE -ne 0) {
        $mode = if ($Strict) { 'strict commons' } else { 'mixed catalog' }
        Add-Error "Bundled skill validation failed in $mode mode."
    }
}

if (-not (Test-Path -LiteralPath $BundledValidator -PathType Leaf)) {
    throw "Bundled skill validator not found: $BundledValidator"
}

$skillDirs = @(Get-ChildItem -LiteralPath $SkillsRoot -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'SKILL.md') } |
    Sort-Object Name)

Invoke-BundledValidator -Paths @($SkillsRoot)

$skillData = @{}
$commonsDirs = [System.Collections.Generic.List[string]]::new()
foreach ($dir in $skillDirs) {
    $skillPath = Join-Path $dir.FullName 'SKILL.md'
    $raw = Get-Content -Raw -LiteralPath $skillPath
    Test-MetadataIndentation -Text $raw -SkillName $dir.Name
    $repo = Get-Scalar $raw 'github-repo' $true
    $pin = Get-Scalar $raw 'github-pinned' $true
    $portability = Get-Scalar $raw 'portability' $true
    $applicability = Get-Scalar $raw 'applicability' $true
    $binding = Get-Scalar $raw 'binding' $true
    $risk = Get-Scalar $raw 'risk' $true
    $maturity = Get-Scalar $raw 'maturity' $true
    $requires = Get-Scalar $raw 'requires' $true
    $related = Get-Scalar $raw 'related' $true
    $overlayPath = Join-Path $dir.FullName 'overlay.md'
    $hasOverlay = Test-Path -LiteralPath $overlayPath -PathType Leaf

    if ($repo -eq $CommonsRepo) {
        $commonsDirs.Add($dir.FullName)
    }
    elseif ([string]::IsNullOrWhiteSpace($repo)) {
        foreach ($metadataField in @{
                portability = $portability
                applicability = $applicability
                binding = $binding
                risk = $risk
                maturity = $maturity
                requires = $requires
                related = $related
            }.GetEnumerator()) {
            if ([string]::IsNullOrWhiteSpace([string]$metadataField.Value)) {
                Add-Error ".agents/skills/$($dir.Name)/SKILL.md is missing metadata.$($metadataField.Key)."
            }
        }
    }

    if ($hasOverlay) {
        $overlay = Get-Content -Raw -LiteralPath $overlayPath
        if (-not $overlay.StartsWith("---`n") -and -not $overlay.StartsWith("---`r`n")) {
            Add-Error ".agents/skills/$($dir.Name)/overlay.md is missing YAML frontmatter."
        }
        else {
            $core = Get-Scalar $overlay 'core'
            $corePin = Get-Scalar $overlay 'core-pin'
            if ($core -ne $dir.Name) {
                Add-Error ".agents/skills/$($dir.Name)/overlay.md core '$core' must match '$($dir.Name)'."
            }
            if ([string]::IsNullOrWhiteSpace($corePin)) {
                Add-Error ".agents/skills/$($dir.Name)/overlay.md is missing core-pin."
            }
            elseif (-not [string]::IsNullOrWhiteSpace($pin) -and $corePin -ne $pin) {
                Add-Error ".agents/skills/$($dir.Name)/overlay.md core-pin '$corePin' must match '$pin'."
            }
        }
    }

    $skillData[$dir.Name] = [pscustomobject]@{
        Name = $dir.Name
        Repo = $repo
        Portability = $portability
        Requires = $requires
        Related = $related
        HasOverlay = $hasOverlay
    }
}

if ($commonsDirs.Count -gt 0) {
    $strictRoot = Join-Path ([System.IO.Path]::GetTempPath()) "touki-agent-skills-$([guid]::NewGuid())"
    New-Item -ItemType Directory -Path $strictRoot | Out-Null
    try {
        foreach ($commonsDir in $commonsDirs) {
            Copy-Item -LiteralPath $commonsDir -Destination $strictRoot -Recurse
        }
        Invoke-BundledValidator -Paths @($strictRoot) -Strict
    }
    finally {
        Remove-Item -LiteralPath $strictRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

foreach ($skill in $skillData.Values) {
    foreach ($relationship in @('Requires', 'Related')) {
        $value = [string]$skill.$relationship
        if ([string]::IsNullOrWhiteSpace($value) -or $value -eq 'none') { continue }
        foreach ($target in ($value -split ',' | ForEach-Object { $_.Trim() })) {
            if (-not $skillData.ContainsKey($target)) {
                Add-Error "$($skill.Name) metadata.$($relationship.ToLowerInvariant()) references missing skill '$target'."
            }
        }
    }
}

$visiting = @{}
$visited = @{}
function Test-RequiresCycle([string] $Name, [string[]] $Stack) {
    if ($visited.ContainsKey($Name)) { return }
    if ($visiting.ContainsKey($Name)) {
        Add-Error "metadata.requires cycle: $(($Stack + $Name) -join ' -> ')"
        return
    }

    $visiting[$Name] = $true
    $value = [string]$skillData[$Name].Requires
    if (-not [string]::IsNullOrWhiteSpace($value) -and $value -ne 'none') {
        foreach ($target in ($value -split ',' | ForEach-Object { $_.Trim() })) {
            if ($skillData.ContainsKey($target)) {
                Test-RequiresCycle -Name $target -Stack ($Stack + $Name)
            }
        }
    }
    $visiting.Remove($Name)
    $visited[$Name] = $true
}

foreach ($name in $skillData.Keys) {
    Test-RequiresCycle -Name $name -Stack @()
}

$catalogEntries = @{}
foreach ($line in Get-Content -LiteralPath $CatalogPath) {
    if (-not $line.StartsWith('| [')) { continue }
    $cells = @($line -split '\|')
    if ($cells.Count -lt 5) { continue }
    $skillCell = $cells[1].Trim()
    $match = [regex]::Match($skillCell, '^\[([a-z0-9-]+)\]\(\./([a-z0-9-]+)/SKILL\.md\)$')
    if (-not $match.Success) { continue }
    $label = $match.Groups[1].Value
    $directory = $match.Groups[2].Value
    if ($catalogEntries.ContainsKey($directory)) {
        Add-Error "Catalog contains duplicate skill '$directory'."
        continue
    }
    if ($label -ne $directory) {
        Add-Error "Catalog label '$label' must match skill directory '$directory'."
    }
    $catalogEntries[$directory] = [pscustomobject]@{
        Portability = $cells[3].Trim()
        CrossReferences = $cells[4].Trim()
    }
}

foreach ($name in $skillData.Keys) {
    if (-not $catalogEntries.ContainsKey($name)) {
        Add-Error "Catalog is missing skill '$name'."
        continue
    }

    $skill = $skillData[$name]
    $expected = if ($skill.Repo -eq $CommonsRepo) {
        if ($skill.HasOverlay) { 'vendored (portable core) + overlay' } else { 'vendored (portable core)' }
    }
    elseif ($skill.Repo -eq $FiltraceRepo) {
        if ($skill.HasOverlay) { 'vendored (tool repo) + overlay' } else { 'vendored (tool repo)' }
    }
    else {
        $skill.Portability
    }

    if ($catalogEntries[$name].Portability -ne $expected) {
        Add-Error "Catalog portability for '$name' is '$($catalogEntries[$name].Portability)'; expected '$expected'."
    }

    foreach ($relationship in @('Requires', 'Related')) {
        $value = [string]$skill.$relationship
        if ([string]::IsNullOrWhiteSpace($value) -or $value -eq 'none') { continue }
        foreach ($target in ($value -split ',' | ForEach-Object { $_.Trim() })) {
            if (-not $catalogEntries[$name].CrossReferences.Contains("``$target``")) {
                Add-Error "Catalog cross-references for '$name' omit metadata.$($relationship.ToLowerInvariant()) target '$target'."
            }
        }
    }
}

foreach ($name in $catalogEntries.Keys) {
    if (-not $skillData.ContainsKey($name)) {
        Add-Error "Catalog references missing skill '$name'."
    }
}

if ($skillData.ContainsKey('filtrace') -and (Test-Path -LiteralPath $McpPath -PathType Leaf)) {
    $filtraceSkill = Get-Content -Raw -LiteralPath (Join-Path $SkillsRoot 'filtrace/SKILL.md')
    $filtracePin = (Get-Scalar $filtraceSkill 'github-pinned' $true).TrimStart('v')
    $mcp = Get-Content -Raw -LiteralPath $McpPath
    $packageMatch = [regex]::Match($mcp, 'KlutzyNinja\.Filtrace\.Mcp@([^"\s]+)')
    if (-not $packageMatch.Success) {
        Add-Error '.vscode/mcp.json must pin KlutzyNinja.Filtrace.Mcp explicitly.'
    }
    elseif ($packageMatch.Groups[1].Value -ne $filtracePin) {
        Add-Error "Filtrace MCP pin '$($packageMatch.Groups[1].Value)' must match skill pin '$filtracePin'."
    }
}

if ($Errors.Count -gt 0) {
    Write-Host 'Agent-skill validation failed:' -ForegroundColor Red
    foreach ($errorMessage in $Errors) { Write-Host "  - $errorMessage" }
    exit 1
}

Write-Host "Agent-skill validation passed ($($skillDirs.Count) skills; $($commonsDirs.Count) strict commons cores)." -ForegroundColor Green
exit 0