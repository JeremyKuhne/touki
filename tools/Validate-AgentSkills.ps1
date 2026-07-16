<#
.SYNOPSIS
    Validate the mixed skill catalog used by this repository.

.DESCRIPTION
    Runs the validator bundled with the vendored manage-skills core over all
    skills, then runs its strict portfolio mode over only the commons-vendored
    portable cores. Also validates local overlay pins and tool-package bindings,
    relationship targets and cycles, and the inventory/category labels in
    .agents/skills/README.md.

    Exact vendored payloads remain owned by their source repositories. This
    wrapper adds Touki's overlay and tool-package binding checks without
    modifying the bundled validator.

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

function Get-GitObjectHash([string] $Type, [byte[]] $Content) {
    $header = [System.Text.Encoding]::ASCII.GetBytes("$Type $($Content.Length)`0")
    $payload = [byte[]]::new($header.Length + $Content.Length)
    [Array]::Copy($header, 0, $payload, 0, $header.Length)
    [Array]::Copy($Content, 0, $payload, $header.Length, $Content.Length)
    $hash = [System.Security.Cryptography.SHA1]::HashData($payload)
    return [Convert]::ToHexString($hash).ToLowerInvariant()
}

function Get-GitBlobHash([string] $Path) {
    $relativePath = [System.IO.Path]::GetRelativePath($RepoRoot, $Path).Replace('\', '/')
    $hash = (& git -C $RepoRoot hash-object -- $relativePath).Trim()
    if ($LASTEXITCODE -ne 0 -or $hash -notmatch '^[0-9a-f]{40}$') {
        throw "Could not compute the canonical Git blob hash for '$Path'."
    }
    return $hash
}

function Get-GitFileMode([string] $Path) {
    if (-not $IsWindows) {
        $unixMode = [System.IO.File]::GetUnixFileMode($Path)
        $executeBits = [System.IO.UnixFileMode]::UserExecute -bor
            [System.IO.UnixFileMode]::GroupExecute -bor
            [System.IO.UnixFileMode]::OtherExecute
        if (($unixMode -band $executeBits) -ne 0) {
            return '100755'
        }

        return '100644'
    }

    $relativePath = [System.IO.Path]::GetRelativePath($RepoRoot, $Path).Replace('\', '/')
    $stageEntry = @(& git -C $RepoRoot ls-files --stage -- $relativePath)
    if ($LASTEXITCODE -ne 0) {
        throw "Could not read the canonical Git mode for '$Path'."
    }
    if ($stageEntry.Count -eq 0) { return '100644' }
    if ($stageEntry.Count -ne 1 -or $stageEntry[0] -notmatch '^(100644|100755) ') {
        throw "Unsupported Git mode for tool payload file '$Path': $($stageEntry -join '; ')"
    }
    return $Matches[1]
}

function Get-GitTreeHash([string] $Directory, [switch] $ToolPayloadRoot) {
    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($child in Get-ChildItem -LiteralPath $Directory -Force) {
        if ($ToolPayloadRoot -and $child.Name -eq 'overlay.md') { continue }
        if ($child.Attributes.HasFlag([System.IO.FileAttributes]::ReparsePoint)) {
            throw "Tool payload tree hashing does not support reparse points: $($child.FullName)"
        }

        if ($child.PSIsContainer) {
            $mode = '40000'
            $hash = Get-GitTreeHash -Directory $child.FullName
            $sortName = "$($child.Name)/"
        }
        else {
            $mode = Get-GitFileMode -Path $child.FullName
            $hash = Get-GitBlobHash -Path $child.FullName
            $sortName = $child.Name
        }

        $entries.Add([pscustomobject]@{
                Mode = $mode
                Name = $child.Name
                SortName = $sortName
                Hash = $hash
            })
    }

    $sorted = $entries.ToArray()
    [Array]::Sort(
        $sorted,
        [System.Comparison[object]]{
            param($left, $right)
            return [StringComparer]::Ordinal.Compare($left.SortName, $right.SortName)
        })

    $stream = [System.IO.MemoryStream]::new()
    try {
        foreach ($entry in $sorted) {
            $prefix = [System.Text.Encoding]::UTF8.GetBytes("$($entry.Mode) $($entry.Name)")
            $stream.Write($prefix, 0, $prefix.Length)
            $stream.WriteByte(0)
            $hashBytes = [Convert]::FromHexString($entry.Hash)
            $stream.Write($hashBytes, 0, $hashBytes.Length)
        }
        return Get-GitObjectHash -Type tree -Content $stream.ToArray()
    }
    finally {
        $stream.Dispose()
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
        if ($binding -notin @('optional-overlay', 'required-overlay')) {
            Add-Error ".agents/skills/$($dir.Name)/overlay.md requires an overlay-capable metadata.binding."
        }
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
        Applicability = $applicability
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
    $expected = if ($skill.Applicability -eq 'tool-shipped') {
        if ($skill.HasOverlay) { 'vendored (tool repo) + overlay' } else { 'vendored (tool repo)' }
    }
    elseif ($skill.Repo -eq $CommonsRepo) {
        if ($skill.HasOverlay) { 'vendored (portable core) + overlay' } else { 'vendored (portable core)' }
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
    $mcp = Get-Content -Raw -LiteralPath $McpPath
    $packageMatch = [regex]::Match($mcp, 'KlutzyNinja\.Filtrace\.Mcp@([^"\s]+)')
    if (-not $packageMatch.Success) {
        Add-Error '.vscode/mcp.json must pin KlutzyNinja.Filtrace.Mcp explicitly.'
    }
    else {
        $filtraceOverlayPath = Join-Path $SkillsRoot 'filtrace/overlay.md'
        if (-not (Test-Path -LiteralPath $filtraceOverlayPath -PathType Leaf)) {
            Add-Error '.agents/skills/filtrace/overlay.md must bind the filtrace package.'
        }
        else {
            $filtraceOverlay = Get-Content -Raw -LiteralPath $filtraceOverlayPath
            $packageVersion = $packageMatch.Groups[1].Value
            $corePin = Get-Scalar $filtraceOverlay 'core-pin'
            $coreRepo = Get-Scalar $filtraceOverlay 'core-repo'
            $coreTreeSha = Get-Scalar $filtraceOverlay 'core-tree-sha'
            $runtimePin = Get-Scalar $filtraceOverlay 'runtime-pin'
            $releasePinMatch = [regex]::Match($corePin, '^v(\d+\.\d+\.\d+)$')
            if ($corePin -notmatch '^[0-9a-f]{40}$' -and -not $releasePinMatch.Success) {
                Add-Error 'Filtrace overlay core-pin must be an exact source commit or stable release tag.'
            }
            elseif ($releasePinMatch.Success -and $releasePinMatch.Groups[1].Value -ne $runtimePin) {
                Add-Error "Filtrace overlay release pin '$corePin' must match runtime-pin '$runtimePin'."
            }
            if ($coreRepo -ne 'https://github.com/JeremyKuhne/filtrace') {
                Add-Error "Filtrace overlay core-repo '$coreRepo' is not the canonical repository."
            }
            if ($coreTreeSha -notmatch '^[0-9a-f]{40}$') {
                Add-Error 'Filtrace overlay core-tree-sha must be a 40-character lowercase SHA.'
            }
            else {
                $actualTreeSha = Get-GitTreeHash -Directory (Join-Path $SkillsRoot 'filtrace') -ToolPayloadRoot
                if ($actualTreeSha -ne $coreTreeSha) {
                    Add-Error "Filtrace payload tree '$actualTreeSha' must match overlay core-tree-sha '$coreTreeSha'."
                }
            }
            if ($runtimePin -ne $packageVersion) {
                Add-Error "Filtrace overlay runtime-pin '$runtimePin' must match MCP package pin '$packageVersion'."
            }
            if (-not $filtraceOverlay.Contains("KlutzyNinja.Filtrace.Mcp@$packageVersion")) {
                Add-Error "Filtrace overlay must bind the MCP package pin '$packageVersion'."
            }
            if (-not $filtraceOverlay.Contains("--version $packageVersion")) {
                Add-Error "Filtrace overlay must bind the CLI package pin '$packageVersion'."
            }
        }
    }
}

if ($Errors.Count -gt 0) {
    Write-Host 'Agent-skill validation failed:' -ForegroundColor Red
    foreach ($errorMessage in $Errors) { Write-Host "  - $errorMessage" }
    exit 1
}

Write-Host "Agent-skill validation passed ($($skillDirs.Count) skills; $($commonsDirs.Count) strict commons cores)." -ForegroundColor Green
exit 0
