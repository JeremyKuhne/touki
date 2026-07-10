<#
.SYNOPSIS
    Validates Agent Skills (SKILL.md) against the agentskills.io specification. A
    dependency-free PowerShell check based on `agentskills/skills-ref` (frontmatter),
    plus the spec's length recommendation and Claude's no-XML-tags rule.

.DESCRIPTION
    For each skill directory, checks the SKILL.md against the Agent Skills
    spec (https://agentskills.io/specification):

      - required fields present: name and description. Any other field (the spec's
        optionals, client extensions, or custom keys) is allowed and not flagged.
      - name: required; <= 64 chars; NFKC-normalized; lowercase; letters, digits,
        and hyphens only; no leading/trailing or consecutive hyphen; matches the
        directory name.
      - description: required; non-empty; <= 1024 chars; no XML-style tags (the
        name and description are injected into the agent's skill-metadata block).
      - compatibility: if present, a string <= 500 chars.
      - yaml: inline scalar values carry no unquoted ':' (a mapping indicator that
        a strict parser, like the strictyaml skills-ref uses, would reject).
      - length: SKILL.md is at most 500 lines (the spec's progressive-disclosure
        recommendation, "Keep your main SKILL.md under 500 lines").

    The length and no-XML-tags checks go beyond skills-ref, which validates
    frontmatter only; the colon check restores parity with its strict YAML parser.

    With -RequirePortfolioMetadata, also enforces this commons' portable-core
    policy: metadata.portability/applicability/binding/risk/maturity/requires/
    related, the optional-overlay loader cue, and overlay.md frontmatter when an
    overlay is present.

    The frontmatter parser handles inline scalars, `>`/`|` block scalars, and one
    level of `metadata:` mapping, with `---` matched line by line. A known field
    given a block mapping/sequence (or an unquoted-colon scalar) is rejected;
    unknown fields may take any shape. For arbitrary YAML use the `skills-ref` tool.

    Exits 0 when every skill is valid, 1 otherwise.

.PARAMETER Path
    One or more paths. Each may be a skill directory (contains SKILL.md) or a
    parent directory whose immediate subdirectories are skills (e.g. skills/).
    Defaults to the current directory.

.PARAMETER Quiet
    Print only failures.

.PARAMETER RequirePortfolioMetadata
    Require and validate the commons portfolio metadata and overlay contract.

.EXAMPLE
    pwsh Validate-Skills.ps1 skills/
    Validate every skill directory under skills/.

.EXAMPLE
    pwsh Validate-Skills.ps1 skills/manage-skills
    Validate a single skill directory.

.EXAMPLE
    pwsh Validate-Skills.ps1 skills/ -RequirePortfolioMetadata
    Validate every commons core against the stricter portfolio policy.
#>

#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Position = 0)] [string[]] $Path,
    [switch] $Quiet,
    [switch] $RequirePortfolioMetadata
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$MaxName = 64
$MaxDescription = 1024
$MaxCompatibility = 500
$MaxSkillLines = 500
$PortfolioMetadataFields = @('portability', 'applicability', 'binding', 'risk', 'maturity', 'requires', 'related')
$AllowedPortability = @('portable', 'semi-portable', 'repo-specific')
$AllowedApplicability = @('universal', 'git-github', 'agent-customization', 'dotnet', 'dotnet-framework', 'dotnet-project-gated', 'tool-shipped', 'repo-local')
$AllowedBinding = @('none', 'optional-overlay', 'required-overlay')
$AllowedRisk = @('advisory', 'local-write', 'remote-write')
$AllowedMaturity = @('experimental', 'canary', 'stable')
$OverlayCue = 'If `overlay.md` exists beside this file, read it before acting'
# Spec scalar fields. If one of these is given a block mapping/sequence instead of
# a scalar it is rejected; unknown fields and `metadata` are not shape-checked.
$KnownScalarFields = @('name', 'description', 'license', 'compatibility', 'allowed-tools')

function Get-SkillMd ([string] $dir) {
    foreach ($n in 'SKILL.md', 'skill.md') {
        $p = Join-Path $dir $n
        if (Test-Path -LiteralPath $p -PathType Leaf) { return $p }
    }
    return $null
}

# Unwrap a YAML scalar's surrounding quotes. A single-quoted scalar un-doubles ''
# (the YAML single-quote escape); a double-quoted scalar is stripped as-is.
function Get-ScalarValue ([string] $value) {
    if ($value.Length -ge 2 -and $value.StartsWith("'") -and $value.EndsWith("'")) {
        return $value.Substring(1, $value.Length - 2).Replace("''", "'")
    }
    if ($value.Length -ge 2 -and $value.StartsWith('"') -and $value.EndsWith('"')) {
        return $value.Substring(1, $value.Length - 2)
    }
    return $value
}

# Reject an unquoted inline scalar whose value holds a ':' followed by whitespace
# (or a trailing ':') - a YAML mapping indicator that strict parsers (strictyaml,
# which skills-ref uses) reject. Quoted values and flow collections ({ } / [ ]) are
# exempt (their inner colons are valid YAML); true block scalars are handled before
# this is reached, so a leading '>'/'|' here is plain text.
function Test-InlineColon ([string] $key, [string] $value) {
    if ($value -match '^["''\[{]') { return }
    if ($value -match ':(\s|$)') {
        throw "Invalid YAML in frontmatter: value for '$key' has an unquoted ':' (a YAML mapping indicator); quote the value or use a block scalar (>-)."
    }
}

# Fold a YAML block scalar (the lines under a `key: >` or `key: |`) into a string:
# common indentation stripped, '|' kept literal (newline-joined), '>' folded with
# spaces.
function Join-BlockScalar ([System.Collections.Generic.List[string]] $blockLines, [bool] $literal) {
    $nonBlank = @($blockLines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($nonBlank.Count -eq 0) { return '' }
    $minIndent = ($nonBlank | ForEach-Object { [regex]::Match($_, '^\s*').Length } | Measure-Object -Minimum).Minimum
    $dedented = $blockLines | ForEach-Object {
        if ([string]::IsNullOrWhiteSpace($_)) { '' } else { $_.Substring([Math]::Min($minIndent, $_.Length)) }
    }
    if ($literal) { return ($dedented -join "`n").Trim() }
    $sb = [System.Text.StringBuilder]::new()
    $prevBlank = $true
    foreach ($l in $dedented) {
        if ($l -eq '') { [void]$sb.Append("`n"); $prevBlank = $true }
        else { if (-not $prevBlank) { [void]$sb.Append(' ') }; [void]$sb.Append($l); $prevBlank = $false }
    }
    return $sb.ToString().Trim()
}

# Parse SKILL.md frontmatter into a case-sensitive ordered map. The `---`
# delimiters are matched line by line. Handles inline scalars, `>`/`|` block
# scalars, and one level of `metadata:` block mapping. A known field given a block
# mapping/sequence is rejected; an unknown field may take any shape (unvalidated).
# Not a general YAML parser; for arbitrary YAML use `skills-ref`.
function Read-Frontmatter ([string] $content) {
    $allLines = $content -split "\r?\n"
    if ($allLines.Count -eq 0 -or $allLines[0].Trim() -ne '---') {
        throw 'SKILL.md must start with YAML frontmatter (---)'
    }
    $end = -1
    for ($k = 1; $k -lt $allLines.Count; $k++) {
        if ($allLines[$k].Trim() -eq '---') { $end = $k; break }
    }
    if ($end -lt 0) { throw 'SKILL.md frontmatter not properly closed with ---' }

    $map = New-Object System.Collections.Specialized.OrderedDictionary ([System.StringComparer]::Ordinal)
    $lines = @()
    if ($end -gt 1) { $lines = @($allLines[1..($end - 1)]) }
    $i = 0
    while ($i -lt $lines.Count) {
        $line = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($line)) { $i++; continue }
        if ($line.TrimStart().StartsWith('#')) { $i++; continue }
        if ($line -notmatch '^\S') { throw "Invalid YAML in frontmatter near: $line" }

        $idx = $line.IndexOf(':')
        if ($idx -lt 0) { throw "Invalid YAML in frontmatter near: $line" }
        $key = $line.Substring(0, $idx).Trim()
        $rest = $line.Substring($idx + 1).Trim()

        if ($rest -match '^[|>][+-]?\d*\s*$') {
            $literal = $rest.StartsWith('|')
            $i++
            $blockLines = [System.Collections.Generic.List[string]]::new()
            while ($i -lt $lines.Count -and ([string]::IsNullOrWhiteSpace($lines[$i]) -or $lines[$i] -match '^\s')) {
                $blockLines.Add($lines[$i]); $i++
            }
            $map[$key] = Join-BlockScalar $blockLines $literal
            continue
        }

        if ($rest -eq '') {
            # Empty value: classify the following block (mapping, sequence, or none).
            $j = $i + 1
            while ($j -lt $lines.Count -and [string]::IsNullOrWhiteSpace($lines[$j])) { $j++ }
            $hasChild = ($j -lt $lines.Count -and $lines[$j] -match '^\s')
            $isSequence = $hasChild -and ($lines[$j] -match '^\s+-(\s|$)')

            if ($hasChild -and $key -ceq 'metadata' -and -not $isSequence) {
                $sub = New-Object System.Collections.Specialized.OrderedDictionary ([System.StringComparer]::Ordinal)
                $i++
                while ($i -lt $lines.Count) {
                    if ([string]::IsNullOrWhiteSpace($lines[$i])) { $i++; continue }
                    if ($lines[$i] -notmatch '^\s') { break }
                    if ($lines[$i].TrimStart().StartsWith('#')) { $i++; continue }
                    $kv = $lines[$i].Trim()
                    $ci = $kv.IndexOf(':')
                    if ($ci -lt 0) { throw "Invalid YAML in frontmatter near: $kv" }
                    $sk = $kv.Substring(0, $ci).Trim()
                    $sv = $kv.Substring($ci + 1).Trim()
                    Test-InlineColon $sk $sv
                    $sub[$sk] = Get-ScalarValue $sv
                    $i++
                }
                $map[$key] = $sub
                continue
            }

            if ($hasChild -and $key -cin $KnownScalarFields) {
                $shape = if ($isSequence) { 'sequence' } else { 'mapping' }
                throw "Field '$key' must be a scalar value, not a block $shape."
            }

            # A null value, an unknown field's nested block, or a metadata sequence:
            # swallow any nested lines and store an empty value (shape unvalidated).
            if ($hasChild) {
                $i++
                while ($i -lt $lines.Count -and ([string]::IsNullOrWhiteSpace($lines[$i]) -or $lines[$i] -match '^\s')) { $i++ }
            }
            else {
                $i++
            }
            $map[$key] = ''
            continue
        }

        Test-InlineColon $key $rest
        $map[$key] = Get-ScalarValue $rest
        $i++
    }
    return $map
}

# Count physical lines in SKILL.md (matches editor line numbers / Get-Content).
function Measure-SkillLineCount ([string] $content) {
    if ([string]::IsNullOrEmpty($content)) { return 0 }
    $n = ($content -split "\r?\n").Count
    if ($content.EndsWith("`n")) { $n-- }
    return $n
}

function Test-SkillName ($name, [string] $dir) {
    if ($null -eq $name -or $name -isnot [string] -or [string]::IsNullOrWhiteSpace($name)) {
        "Field 'name' must be a non-empty string"
        return
    }
    $name = $name.Trim().Normalize([System.Text.NormalizationForm]::FormKC)

    if ($name.Length -gt $MaxName) {
        "Skill name '$name' exceeds $MaxName character limit ($($name.Length) chars)"
    }
    if ($name -cne $name.ToLowerInvariant()) {
        "Skill name '$name' must be lowercase"
    }
    if ($name.StartsWith('-') -or $name.EndsWith('-')) {
        'Skill name cannot start or end with a hyphen'
    }
    if ($name.Contains('--')) {
        'Skill name cannot contain consecutive hyphens'
    }
    $invalid = $false
    foreach ($c in $name.ToCharArray()) { if (-not ([char]::IsLetterOrDigit($c) -or $c -eq '-')) { $invalid = $true; break } }
    if ($invalid) {
        "Skill name '$name' contains invalid characters. Only letters, digits, and hyphens are allowed."
    }
    if ($dir) {
        $leaf = Split-Path -Leaf $dir
        if ($leaf.Normalize([System.Text.NormalizationForm]::FormKC) -cne $name) {
            "Directory name '$leaf' must match skill name '$name'"
        }
    }
}

function Test-SkillDescription ($description) {
    if ($null -eq $description -or $description -isnot [string] -or [string]::IsNullOrWhiteSpace($description)) {
        "Field 'description' must be a non-empty string"
        return
    }
    if ($description.Length -gt $MaxDescription) {
        "Description exceeds $MaxDescription character limit ($($description.Length) chars)"
    }
    if ($description -match '</?[A-Za-z][^<>]*>') {
        "Description must not contain XML-style tags (found '$($Matches[0])'). The name and description are injected into the agent's skill-metadata XML block; reword (e.g. 'of T') or use backticks."
    }
}

function Test-SkillCompatibility ($compatibility) {
    if ($compatibility -isnot [string]) {
        "Field 'compatibility' must be a string"
        return
    }
    if ($compatibility.Length -gt $MaxCompatibility) {
        "Compatibility exceeds $MaxCompatibility character limit ($($compatibility.Length) chars)"
    }
}

function Get-MetadataValue ($metadata, [string] $key) {
    if ($metadata -is [System.Collections.IDictionary] -and $metadata.Contains($key)) {
        return $metadata[$key]
    }
    return $null
}

function Test-MetadataEnum ($metadata, [string] $key, [string[]] $allowed) {
    $value = Get-MetadataValue $metadata $key
    if ($null -eq $value) { return }
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        "metadata.$key must be a non-empty string"
        return
    }
    $value = $value.Trim()
    if ($allowed -cnotcontains $value) {
        "metadata.$key '$value' is invalid; expected one of: $($allowed -join ', ')"
    }
}

function Test-RelationshipMetadata ($metadata, [string] $key) {
    $value = Get-MetadataValue $metadata $key
    if ($null -eq $value) { return }
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value)) {
        "metadata.$key must be 'none' or a comma-separated list of skill names"
        return
    }
    $value = $value.Trim()
    if ($value -ceq 'none') { return }

    $names = @($value -split ',' | ForEach-Object { $_.Trim() })
    if ($names.Count -eq 0 -or $names -ccontains '') {
        "metadata.$key must be 'none' or a comma-separated list of skill names"
        return
    }
    foreach ($relationshipName in $names) {
        if ($relationshipName -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$') {
            "metadata.$key contains invalid skill name '$relationshipName'"
        }
    }
    $duplicates = @($names | Group-Object | Where-Object Count -gt 1 | ForEach-Object Name)
    if ($duplicates.Count -gt 0) {
        "metadata.$key contains duplicate skill name(s): $($duplicates -join ', ')"
    }
}

function Test-OverlayContract ($metadata, [string] $raw, [string] $dir) {
    $binding = Get-MetadataValue $metadata 'binding'
    if ($binding -isnot [string]) { return }
    $binding = $binding.Trim()
    if (@('none', 'optional-overlay', 'required-overlay') -cnotcontains $binding) { return }

    $overlayPath = Join-Path $dir 'overlay.md'
    $hasOverlay = Test-Path -LiteralPath $overlayPath -PathType Leaf

    if (@('optional-overlay', 'required-overlay') -ccontains $binding -and -not $raw.Contains($OverlayCue)) {
        "metadata.binding '$binding' requires this loader cue in SKILL.md: $OverlayCue"
    }
    if ($binding -ceq 'required-overlay' -and -not $hasOverlay) {
        "metadata.binding 'required-overlay' requires overlay.md beside SKILL.md"
        return
    }
    if ($binding -ceq 'none' -and $hasOverlay) {
        "overlay.md exists but metadata.binding is 'none'"
        return
    }
    if (-not $hasOverlay) { return }

    try {
        $overlayFrontmatter = Read-Frontmatter (Get-Content -LiteralPath $overlayPath -Raw)
    }
    catch {
        "overlay.md: $($_.Exception.Message)"
        return
    }

    foreach ($requiredOverlayField in @('core', 'core-pin')) {
        if (-not $overlayFrontmatter.Contains($requiredOverlayField) -or
            [string]::IsNullOrWhiteSpace([string]$overlayFrontmatter[$requiredOverlayField])) {
            "overlay.md is missing required frontmatter field: $requiredOverlayField"
        }
    }
    if ($overlayFrontmatter.Contains('core')) {
        $directoryName = Split-Path -Leaf $dir
        if ([string]$overlayFrontmatter['core'] -cne $directoryName) {
            "overlay.md core '$($overlayFrontmatter['core'])' must match skill directory '$directoryName'"
        }
    }
}

function Test-PortfolioMetadata ($metadata, [string] $raw, [string] $dir, [bool] $required) {
    if ($null -eq $metadata) {
        if ($required) { 'Missing required field in frontmatter: metadata' }
        return
    }
    if ($metadata -isnot [System.Collections.IDictionary]) {
        'Field metadata must be a mapping'
        return
    }

    if ($required) {
        foreach ($requiredMetadataField in $PortfolioMetadataFields) {
            if (-not $metadata.Contains($requiredMetadataField)) {
                "Missing required portfolio field: metadata.$requiredMetadataField"
            }
        }
    }

    Test-MetadataEnum $metadata 'portability' $AllowedPortability
    Test-MetadataEnum $metadata 'applicability' $AllowedApplicability
    Test-MetadataEnum $metadata 'binding' $AllowedBinding
    Test-MetadataEnum $metadata 'risk' $AllowedRisk
    Test-MetadataEnum $metadata 'maturity' $AllowedMaturity
    Test-RelationshipMetadata $metadata 'requires'
    Test-RelationshipMetadata $metadata 'related'

    $portability = Get-MetadataValue $metadata 'portability'
    if ($portability -is [string]) { $portability = $portability.Trim() }
    if ($required -and $portability -cne 'portable') {
        "Commons cores must set metadata.portability to 'portable'"
    }

    Test-OverlayContract $metadata $raw $dir
}

function Test-SkillDir ([string] $dir) {
    $errors = [System.Collections.Generic.List[string]]::new()

    $resolved = Resolve-Path -LiteralPath $dir -ErrorAction SilentlyContinue
    if (-not $resolved) { return @("Path does not exist: $dir") }
    $dir = $resolved.Path
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { return @("Not a directory: $dir") }

    $md = Get-SkillMd $dir
    if (-not $md) { return @('Missing required file: SKILL.md') }

    $raw = Get-Content -LiteralPath $md -Raw
    try {
        $fm = Read-Frontmatter $raw
    }
    catch {
        return @($_.Exception.Message)
    }

    if (-not $fm.Contains('name')) { $errors.Add('Missing required field in frontmatter: name') }
    else { Test-SkillName $fm['name'] $dir | ForEach-Object { $errors.Add($_) } }

    if (-not $fm.Contains('description')) { $errors.Add('Missing required field in frontmatter: description') }
    else { Test-SkillDescription $fm['description'] | ForEach-Object { $errors.Add($_) } }

    if ($fm.Contains('compatibility')) { Test-SkillCompatibility $fm['compatibility'] | ForEach-Object { $errors.Add($_) } }

    $metadata = if ($fm.Contains('metadata')) { $fm['metadata'] } else { $null }
    Test-PortfolioMetadata $metadata $raw $dir $RequirePortfolioMetadata.IsPresent |
        ForEach-Object { $errors.Add($_) }

    $lineCount = Measure-SkillLineCount $raw
    if ($lineCount -gt $MaxSkillLines) {
        $errors.Add("SKILL.md is $lineCount lines; keep it under the recommended $MaxSkillLines-line limit (move detail into references/ or sibling files).")
    }

    return $errors.ToArray()
}

# ---------------------------------------------------------------------------

if (-not $Path -or $Path.Count -eq 0) {
    $Path = @('.')
}

$targets = [System.Collections.Generic.List[string]]::new()
foreach ($p in $Path) {
    if (Get-SkillMd $p) {
        $targets.Add((Resolve-Path -LiteralPath $p).Path)
    }
    elseif (Test-Path -LiteralPath $p -PathType Container) {
        Get-ChildItem -LiteralPath $p -Directory |
            Where-Object { Get-SkillMd $_.FullName } |
            ForEach-Object { $targets.Add($_.FullName) }
    }
    else {
        $targets.Add($p)
    }
}

$ordered = @($targets | Sort-Object -Unique)
if ($ordered.Count -eq 0) {
    Write-Host 'No skills found.' -ForegroundColor Yellow
    exit 1
}

$failed = 0
foreach ($t in $ordered) {
    $rel = [System.IO.Path]::GetRelativePath((Get-Location).Path, $t)
    $errs = @(Test-SkillDir $t)
    if ($errs.Count -eq 0) {
        if (-not $Quiet) { Write-Host "  OK    $rel" -ForegroundColor Green }
    }
    else {
        $failed++
        Write-Host "  FAIL  $rel" -ForegroundColor Red
        foreach ($e in $errs) { Write-Host "          - $e" -ForegroundColor Red }
    }
}

Write-Host ''
if ($failed -gt 0) {
    Write-Host "$failed of $($ordered.Count) skill(s) failed validation." -ForegroundColor Red
    exit 1
}
Write-Host "All $($ordered.Count) skill(s) valid." -ForegroundColor Green
exit 0
