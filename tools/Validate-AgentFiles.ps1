<#
.SYNOPSIS
    Validate AI-agent customization files in the repo.

.DESCRIPTION
    Checks performed:

    1. AGENTS.md is byte-equivalent to .github/copilot-instructions.md (after the single
       DO-NOT-EDIT comment line at the top of the mirror).
    2. *.instructions.md files have a non-empty `applyTo` frontmatter value.
    3. SKILL.md files have `name` (^[a-z0-9-]{1,64}$, matching parent directory) and
       `description`.
    4. *.agent.md files have `description`; if `tools` is present it must be a YAML list.
    5. No trailing whitespace or whitespace-only lines in any agent file.

    Frontmatter is parsed with a small hand-written parser that handles the flat scalars
    and flat lists used by this repo's schema. If the schema grows beyond that, swap in
    the `powershell-yaml` module.

.PARAMETER Fix
    Regenerate .github/copilot-instructions.md from AGENTS.md.

.EXAMPLE
    pwsh tools/Validate-AgentFiles.ps1
    pwsh tools/Validate-AgentFiles.ps1 -Fix
#>
[CmdletBinding()]
param(
    [switch]$Fix
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$AgentsMd = Join-Path $RepoRoot 'AGENTS.md'
$CopilotMirror = Join-Path $RepoRoot '.github/copilot-instructions.md'
$MirrorHeaderText = '<!-- DO NOT EDIT. Generated mirror of /AGENTS.md. Edit AGENTS.md and run: pwsh tools/Validate-AgentFiles.ps1 -Fix -->'

$ScanDirs = @(
    (Join-Path $RepoRoot '.github/instructions'),
    (Join-Path $RepoRoot '.github/prompts'),
    (Join-Path $RepoRoot '.github/agents'),
    (Join-Path $RepoRoot '.agents')
)
$ExtraWhitespaceFiles = @(
    $AgentsMd,
    $CopilotMirror,
    (Join-Path $RepoRoot 'docs/agent-customization.md')
)

$SkillNamePattern = '^[a-z0-9-]{1,64}$'
$Errors = [System.Collections.Generic.List[string]]::new()

function Add-Error([string]$Message) {
    $Errors.Add($Message) | Out-Null
}

function Get-RelativePath([string]$Path) {
    return [System.IO.Path]::GetRelativePath($RepoRoot, $Path).Replace('\', '/')
}

function Get-ExpectedMirror() {
    # Use the line-ending convention of AGENTS.md on disk so the result matches
    # whatever Git's autocrlf produced on this checkout. This keeps the mirror
    # byte-identical to AGENTS.md (modulo the prepended header line).
    $agents = Get-Content -Raw -LiteralPath $AgentsMd
    $newline = if ($agents -match "`r`n") { "`r`n" } else { "`n" }
    return $MirrorHeaderText + $newline + $agents
}

# Parse a small subset of YAML frontmatter: flat scalars (`key: value`) and flat
# inline lists (`key: [a, b]` or `key: ['a', 'b']`). Returns a hashtable, or $null
# if no frontmatter block is present.
function Get-Frontmatter([string]$Path) {
    $text = Get-Content -Raw -LiteralPath $Path
    if (-not $text.StartsWith("---`n") -and -not $text.StartsWith("---`r`n")) {
        return $null
    }
    $lines = $text -split "`r?`n"
    # First line is '---'. Find the closing '---'.
    $end = -1
    for ($i = 1; $i -lt $lines.Length; $i++) {
        if ($lines[$i] -eq '---') { $end = $i; break }
    }
    if ($end -lt 0) { return $null }

    $result = @{}
    for ($i = 1; $i -lt $end; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*$' -or $line -match '^\s*#') { continue }
        if ($line -notmatch '^([A-Za-z0-9_-]+)\s*:\s*(.*)$') { continue }
        $key = $Matches[1]
        $rawValue = $Matches[2].Trim()

        if ($rawValue -eq '') {
            $result[$key] = ''
            continue
        }
        # Inline list: [a, b, c] or ['a', "b"]
        if ($rawValue -match '^\[(.*)\]\s*$') {
            $inner = $Matches[1].Trim()
            if ($inner -eq '') {
                $result[$key] = @()
            }
            else {
                $items = $inner -split ',' | ForEach-Object {
                    $item = $_.Trim()
                    if ($item -match "^'(.*)'$" -or $item -match '^"(.*)"$') { $Matches[1] } else { $item }
                }
                $result[$key] = @($items)
            }
            continue
        }
        # Quoted scalar
        if ($rawValue -match "^'(.*)'\s*$" -or $rawValue -match '^"(.*)"\s*$') {
            $result[$key] = $Matches[1]
            continue
        }
        # Bare scalar
        $result[$key] = $rawValue
    }
    return $result
}

function Test-Mirror() {
    if (-not (Test-Path -LiteralPath $CopilotMirror)) {
        Add-Error "$(Get-RelativePath $CopilotMirror) is missing."
        return
    }
    $expected = Get-ExpectedMirror
    $actual = Get-Content -Raw -LiteralPath $CopilotMirror
    if ($actual -ne $expected) {
        Add-Error "$(Get-RelativePath $CopilotMirror) is out of sync with AGENTS.md. Run: pwsh tools/Validate-AgentFiles.ps1 -Fix"
    }
}

function Test-Instructions([string]$Path) {
    $fm = Get-Frontmatter $Path
    $rel = Get-RelativePath $Path
    if ($null -eq $fm -or -not $fm.ContainsKey('applyTo') -or [string]::IsNullOrWhiteSpace([string]$fm['applyTo'])) {
        Add-Error "${rel}: missing or empty ``applyTo`` frontmatter."
    }
}

function Test-Skill([string]$Path) {
    $fm = Get-Frontmatter $Path
    $rel = Get-RelativePath $Path
    $parent = Split-Path -Leaf (Split-Path -Parent $Path)
    if ($null -eq $fm) {
        Add-Error "${rel}: missing frontmatter."
        return
    }
    $name = if ($fm.ContainsKey('name')) { [string]$fm['name'] } else { '' }
    $desc = if ($fm.ContainsKey('description')) { [string]$fm['description'] } else { '' }
    if ($name -notmatch $SkillNamePattern) {
        Add-Error "${rel}: ``name`` must match $SkillNamePattern (got '$name')."
    }
    elseif ($name -ne $parent) {
        Add-Error "${rel}: ``name`` ('$name') must equal parent directory ('$parent')."
    }
    if ([string]::IsNullOrWhiteSpace($desc)) {
        Add-Error "${rel}: missing or empty ``description``."
    }
}

function Test-Agent([string]$Path) {
    $fm = Get-Frontmatter $Path
    $rel = Get-RelativePath $Path
    if ($null -eq $fm) {
        Add-Error "${rel}: missing frontmatter."
        return
    }
    $desc = if ($fm.ContainsKey('description')) { [string]$fm['description'] } else { '' }
    if ([string]::IsNullOrWhiteSpace($desc)) {
        Add-Error "${rel}: missing or empty ``description``."
    }
    if ($fm.ContainsKey('tools') -and -not ($fm['tools'] -is [array])) {
        Add-Error "${rel}: ``tools`` must be a list."
    }
}

function Test-Whitespace([string]$Path) {
    $rel = Get-RelativePath $Path
    $lines = (Get-Content -Raw -LiteralPath $Path) -split "`r?`n"
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        $lineNum = $i + 1
        if ($line -ne $line.TrimEnd()) {
            Add-Error "${rel}:${lineNum}: trailing whitespace."
        }
        if ($line.Length -gt 0 -and [string]::IsNullOrWhiteSpace($line)) {
            Add-Error "${rel}:${lineNum}: whitespace-only line."
        }
    }
}

# --fix mode
if ($Fix) {
    [System.IO.File]::WriteAllText($CopilotMirror, (Get-ExpectedMirror))
    Write-Host "Wrote $(Get-RelativePath $CopilotMirror)"
}

# Discover files
$instructionFiles = @()
$skillFiles = @()
$agentFiles = @()
$whitespaceFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($dir in $ScanDirs) {
    if (-not (Test-Path -LiteralPath $dir)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $dir -Recurse -File) {
        $name = $file.Name
        if ($name -like '*.instructions.md') {
            $instructionFiles += $file.FullName
            [void]$whitespaceFiles.Add($file.FullName)
        }
        elseif ($name -eq 'SKILL.md') {
            $skillFiles += $file.FullName
            [void]$whitespaceFiles.Add($file.FullName)
        }
        elseif ($name -like '*.agent.md') {
            $agentFiles += $file.FullName
            [void]$whitespaceFiles.Add($file.FullName)
        }
        elseif ($name -like '*.md') {
            [void]$whitespaceFiles.Add($file.FullName)
        }
    }
}
foreach ($p in $ExtraWhitespaceFiles) {
    if (Test-Path -LiteralPath $p) { [void]$whitespaceFiles.Add((Resolve-Path -LiteralPath $p).Path) }
}

# Run checks
Test-Mirror
foreach ($p in $instructionFiles) { Test-Instructions $p }
foreach ($p in $skillFiles) { Test-Skill $p }
foreach ($p in $agentFiles) { Test-Agent $p }
foreach ($p in $whitespaceFiles) { Test-Whitespace $p }

if ($Errors.Count -gt 0) {
    Write-Host "Agent-file validation failed:" -ForegroundColor Red
    foreach ($e in $Errors) { Write-Host "  - $e" }
    exit 1
}
Write-Host "Agent-file validation passed." -ForegroundColor Green
exit 0
