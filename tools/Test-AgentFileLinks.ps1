<#
.SYNOPSIS
    Verify that relative Markdown links in the agent customization files point
    at files present in the current working tree.

.DESCRIPTION
    Mirrors what the offline lychee link check enforces in
    .github/workflows/agent-files.yml. The CI step is a
    "links must resolve in the merged tree" gate; this script lets you run the
    same gate locally before pushing so a stale branch doesn't ship broken
    links into a PR.

    By default, scans every Markdown file in lychee's CI scope:

      AGENTS.md
      .github/copilot-instructions.md
      .github/instructions/**/*.md
      .github/prompts/**/*.md
      .github/agents/**/*.md
      .agents/**/*.md
      docs/agent-customization.md
    docs/binary-formatted-object-performance.md
    docs/filtrace-local-demo.md
    docs/performance-investigation.md
    docs/performance-investigation-without-mcp.md
    docs/performance-investigation-agent-tooling-retrospective.md

    For each Markdown link of the form `](target)` that is not an external URL,
    `mailto:`, or pure in-page anchor, the script resolves the target relative
    to the containing file's directory (after stripping any `#fragment`) and
    reports any that don't exist on disk.

.PARAMETER ChangedOnly
    Restrict the scan to files changed on the current branch versus the
    auto-detected base ref. The base is `upstream/main` when a remote literally
    named `upstream` exists (the fork-PR workflow) and `origin/main` otherwise
    (the work-on-canonical-clone workflow). Use this when you only want to
    audit your own diff.

.PARAMETER Base
    Override the base ref used by -ChangedOnly. Implies -ChangedOnly.

.EXAMPLE
    pwsh tools/Test-AgentFileLinks.ps1
    Scans every agent file in CI's lychee scope.

.EXAMPLE
    pwsh tools/Test-AgentFileLinks.ps1 -ChangedOnly
    Scans only the agent-scope files changed on this branch vs upstream/main
    (or origin/main when no upstream remote exists).

.EXAMPLE
    pwsh tools/Test-AgentFileLinks.ps1 -Base origin/feature-branch
    Compare against an explicit ref.
#>
[CmdletBinding()]
param(
    [switch]$ChangedOnly,
    [string]$Base
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

function Get-RepoRelativePath([string]$Path) {
    $full = (Resolve-Path -LiteralPath $Path).Path
    $rootWithSep = $RepoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if ($full.StartsWith($rootWithSep, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($rootWithSep.Length).Replace('\', '/')
    }
    return $full.Replace('\', '/')
}

# Files in CI's lychee scope. Keep in sync with .github/workflows/agent-files.yml.
# Each entry is either a single file or @{ Dir = <relative dir>; Filter = <glob> }
# for recursive scans.
$ScopeEntries = @(
    'AGENTS.md',
    '.github/copilot-instructions.md',
    'docs/agent-customization.md',
    'docs/binary-formatted-object-performance.md',
    'docs/filtrace-local-demo.md',
    'docs/performance-investigation.md',
    'docs/performance-investigation-without-mcp.md',
    'docs/performance-investigation-agent-tooling-retrospective.md',
    @{ Dir = '.github/instructions'; Filter = '*.md' },
    @{ Dir = '.github/prompts'; Filter = '*.md' },
    @{ Dir = '.github/agents'; Filter = '*.md' },
    @{ Dir = '.agents'; Filter = '*.md' }
)

function Resolve-BaseRef() {
    if ($Base) { return $Base }
    $remotes = git -C $RepoRoot remote 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Not a git repository (or git is not on PATH)."
    }
    if ($remotes -contains 'upstream') { return 'upstream/main' }
    if ($remotes -contains 'origin') { return 'origin/main' }
    throw "Cannot resolve a base ref: neither 'upstream' nor 'origin' remote is configured. Pass -Base explicitly."
}

function Get-ScopeFiles() {
    $results = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $ScopeEntries) {
        if ($entry -is [string]) {
            $full = Join-Path $RepoRoot $entry
            if (Test-Path -LiteralPath $full) {
                $results.Add((Resolve-Path -LiteralPath $full).Path) | Out-Null
            }
        }
        else {
            $dir = Join-Path $RepoRoot $entry.Dir
            if (-not (Test-Path -LiteralPath $dir)) { continue }
            $found = Get-ChildItem -LiteralPath $dir -Recurse -File -Filter $entry.Filter -ErrorAction SilentlyContinue
            foreach ($f in $found) { $results.Add($f.FullName) | Out-Null }
        }
    }
    return $results | Sort-Object -Unique
}

function Get-ChangedScopeFiles([string]$BaseRef) {
    $diffOutput = git -C $RepoRoot diff --name-only $BaseRef -- 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git diff against '$BaseRef' failed: $diffOutput"
    }
    $allScope = Get-ScopeFiles
    $allScopeSet = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]$allScope,
        [System.StringComparer]::OrdinalIgnoreCase)

    $changed = [System.Collections.Generic.List[string]]::new()
    foreach ($rel in $diffOutput) {
        if (-not $rel) { continue }
        $abs = (Join-Path $RepoRoot $rel)
        if (-not (Test-Path -LiteralPath $abs)) { continue }
        $absResolved = (Resolve-Path -LiteralPath $abs).Path
        if ($allScopeSet.Contains($absResolved)) {
            $changed.Add($absResolved) | Out-Null
        }
    }
    return $changed | Sort-Object -Unique
}

function Test-FileLinks([string]$Path) {
    $broken = [System.Collections.Generic.List[string]]::new()
    $dir = Split-Path -Parent $Path
    $linkRegex = [regex]'\]\(([^)]+)\)'
    $lineNumber = 0
    $inFence = $false
    foreach ($line in Get-Content -LiteralPath $Path) {
        $lineNumber++
        # Toggle fenced code block on lines that open with ``` (or ~~~), with
        # an optional language tag. Anything inside is treated as code, not
        # markdown, mirroring lychee's markdown-aware link extraction.
        if ($line -match '^\s*(```|~~~)') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence) { continue }
        # Strip inline code spans (`...`) so links inside them aren't matched.
        $stripped = [regex]::Replace($line, '`[^`]*`', '')
        foreach ($linkMatch in $linkRegex.Matches($stripped)) {
            $target = $linkMatch.Groups[1].Value.Trim()
            if (-not $target) { continue }
            # External URLs, mailto, in-page anchors, absolute repo paths: skip.
            if ($target -match '^(https?:|mailto:|tel:|ftp:|#|/)') { continue }
            # Strip any fragment.
            $candidatePath = ($target -split '#', 2)[0]
            if (-not $candidatePath) { continue }
            $resolved = Join-Path $dir $candidatePath
            if (-not (Test-Path -LiteralPath $resolved)) {
                $rel = Get-RepoRelativePath -Path $Path
                $broken.Add("${rel}:${lineNumber}: ${target}") | Out-Null
            }
        }
    }
    return $broken
}

if ($Base) { $ChangedOnly = $true }

if ($ChangedOnly) {
    $baseRef = Resolve-BaseRef
    Write-Host "Scanning files changed vs ${baseRef}..."
    $files = Get-ChangedScopeFiles -BaseRef $baseRef
} else {
    Write-Host "Scanning all agent customization files in CI's lychee scope..."
    $files = Get-ScopeFiles
}

if (-not $files -or $files.Count -eq 0) {
    Write-Host "No files in scope to scan."
    exit 0
}

$allBroken = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    foreach ($entry in (Test-FileLinks -Path $file)) {
        $allBroken.Add($entry) | Out-Null
    }
}

if ($allBroken.Count -gt 0) {
    Write-Host ""
    Write-Host "Broken relative links:" -ForegroundColor Red
    foreach ($entry in $allBroken) {
        Write-Host "  $entry" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "$($allBroken.Count) broken link(s) in $($files.Count) scanned file(s)." -ForegroundColor Red
    exit 1
}

Write-Host "All relative links resolve. ($($files.Count) file(s) scanned.)" -ForegroundColor Green
exit 0
