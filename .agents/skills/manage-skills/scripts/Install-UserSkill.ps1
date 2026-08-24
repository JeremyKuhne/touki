#Requires -Version 7.0

<#
.SYNOPSIS
    Installs a complete Agent Skill directory at user scope.

.DESCRIPTION
    Validates and copies one skill into documented host-specific user roots.
    Multiple hosts are installed as one transaction. Private mode adds
    fail-closed source visibility, synchronization, and shared-root gates.

.PARAMETER SourceSkillPath
    Path to the canonical skill directory containing SKILL.md.

.PARAMETER TargetHost
    One or more user-scope host mappings. Use shared-agents for the neutral
    ~/.agents/skills root shared by several compatible clients.

.PARAMETER ProfileRoot
    User profile root under which documented host-specific skill roots are
    created. Defaults to the current PowerShell home directory.

.PARAMETER Private
    Require a local-only source or a GitHub repository verified as private, and
    reject synchronized, network, Git-worktree, or shared-root destinations.

.PARAMETER AllowPrivateMultiHostExposure
    Explicitly accept the expanded discovery surface when a private skill is
    copied to multiple roots or to the neutral ~/.agents/skills root.

.PARAMETER Force
    Replace existing installed copies after staging and hash verification.
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string] $SourceSkillPath,

    [ValidateSet(
        'github-copilot',
        'shared-agents',
        'claude-code',
        'codex',
        'gemini-cli',
        'cursor')]
    [string[]] $TargetHost = @('github-copilot'),

    [string] $ProfileRoot = $HOME,

    [switch] $Private,
    [switch] $AllowPrivateMultiHostExposure,
    [switch] $Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-PathWithin {
    param(
        [Parameter(Mandatory)] [string] $Candidate,
        [Parameter(Mandatory)] [string] $Root
    )

    if (-not [System.IO.Path]::IsPathFullyQualified($Candidate)) {
        throw "Candidate path must be fully qualified: '$Candidate'."
    }
    if (-not [System.IO.Path]::IsPathFullyQualified($Root)) {
        throw "Root path must be fully qualified: '$Root'."
    }

    $comparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }

    $candidatePath = [System.IO.Path]::GetFullPath($Candidate)
    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)

    return $candidatePath.Equals($rootPath, $comparison) -or
        $candidatePath.StartsWith(
            "$rootPath$([System.IO.Path]::DirectorySeparatorChar)",
            $comparison)
}

function Test-PathEqual {
    param(
        [Parameter(Mandatory)] [string] $Left,
        [Parameter(Mandatory)] [string] $Right
    )

    $comparison = if ($IsWindows) {
        [System.StringComparison]::OrdinalIgnoreCase
    }
    else {
        [System.StringComparison]::Ordinal
    }

    return [System.IO.Path]::GetFullPath($Left).Equals(
        [System.IO.Path]::GetFullPath($Right),
        $comparison)
}

function Test-NetworkPath {
    param([Parameter(Mandatory)] [string] $Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    if ($fullPath.StartsWith('\\', [System.StringComparison]::Ordinal)) {
        return $true
    }

    if ($IsWindows) {
        $root = [System.IO.Path]::GetPathRoot($fullPath)
        if (-not [string]::IsNullOrEmpty($root)) {
            try {
                return [System.IO.DriveInfo]::new($root).DriveType -eq
                    [System.IO.DriveType]::Network
            }
            catch {
                return $false
            }
        }
    }

    return $false
}

function Get-ReparsePointInPath {
    param([Parameter(Mandatory)] [string] $Path)

    $candidate = [System.IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrEmpty($candidate)) {
        if (Test-Path -LiteralPath $candidate) {
            $item = Get-Item -LiteralPath $candidate -Force
            if (($item.Attributes -band
                    [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                return $item.FullName
            }
        }

        $parent = Split-Path -Parent $candidate
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $candidate) {
            break
        }
        $candidate = $parent
    }

    return $null
}

function Get-ExistingAncestor {
    param([Parameter(Mandatory)] [string] $Path)

    $candidate = [System.IO.Path]::GetFullPath($Path)
    while (-not (Test-Path -LiteralPath $candidate)) {
        $parent = Split-Path -Parent $candidate
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $candidate) {
            throw "No existing ancestor was found for '$Path'."
        }

        $candidate = $parent
    }

    return $candidate
}

function Get-TreeManifest {
    param([Parameter(Mandatory)] [string] $Root)

    Get-ChildItem -LiteralPath $Root -Recurse -File -Force |
        ForEach-Object {
            [pscustomobject]@{
                Path = ([System.IO.Path]::GetRelativePath(
                        $Root,
                        $_.FullName)).Replace('\', '/')
                Hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }
        } |
        Sort-Object Path
}

function Remove-EmptyDirectoriesToAncestor {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Ancestor
    )

    $current = [System.IO.Path]::GetFullPath($Path)
    while (-not (Test-PathEqual $current $Ancestor)) {
        if (-not (Test-Path -LiteralPath $current -PathType Container)) {
            break
        }
        if (@(Get-ChildItem -LiteralPath $current -Force).Count -ne 0) {
            break
        }

        Remove-Item -LiteralPath $current -Force
        $parent = Split-Path -Parent $current
        if ([string]::IsNullOrEmpty($parent) -or $parent -eq $current) {
            break
        }
        $current = $parent
    }
}

function Get-UserSkillsRoot {
    param([Parameter(Mandatory)] [string] $Name)

    switch ($Name) {
        'github-copilot' { Join-Path $ProfileRoot '.copilot/skills' }
        'shared-agents' { Join-Path $ProfileRoot '.agents/skills' }
        'claude-code' { Join-Path $ProfileRoot '.claude/skills' }
        'codex' { Join-Path $ProfileRoot '.agents/skills' }
        'gemini-cli' { Join-Path $ProfileRoot '.gemini/skills' }
        'cursor' { Join-Path $ProfileRoot '.cursor/skills' }
        default { throw "Unsupported target host '$Name'." }
    }
}

function Assert-PrivateSource {
    param(
        [Parameter(Mandatory)] [string] $SkillRoot,
        [Parameter(Mandatory)] [string] $GitPath
    )

    $sourceRepository = & $GitPath -C $SkillRoot rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0) {
        return
    }

    $gh = Get-Command gh -ErrorAction SilentlyContinue
    if ($null -eq $gh) {
        throw 'GitHub CLI is required to verify a private Git repository.'
    }

    Push-Location $sourceRepository
    try {
        $visibilityOutput = @(
            & gh repo view --json visibility --jq '.visibility' 2>&1
        )
        $visibilityExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($visibilityExitCode -ne 0) {
        throw 'The source repository visibility could not be verified as private.'
    }

    $visibility = @($visibilityOutput | ForEach-Object {
            $_.ToString().Trim().ToUpperInvariant()
        } | Where-Object { $_ -in @('PRIVATE', 'PUBLIC', 'INTERNAL') } |
        Select-Object -Unique)
    if ($visibility.Count -ne 1) {
        throw 'The source repository returned an unrecognized visibility result.'
    }

    $visibilityValue = $visibility[0]
    if ($visibilityValue -cne 'PRIVATE') {
        throw "Refusing to install a private skill from a $visibilityValue repository."
    }
}

$sourceInputPath = $ExecutionContext.SessionState.Path.
    GetUnresolvedProviderPathFromPSPath($SourceSkillPath)
if (-not (Test-Path -LiteralPath $sourceInputPath -PathType Container)) {
    throw "The source skill directory does not exist: '$SourceSkillPath'."
}

$sourceReparsePoint = Get-ReparsePointInPath $sourceInputPath
if ($null -ne $sourceReparsePoint) {
    throw "The source path contains a reparse point: '$sourceReparsePoint'."
}

$skillRoot = (Resolve-Path -LiteralPath $sourceInputPath).Path
$ProfileRoot = [System.IO.Path]::GetFullPath(
    $ExecutionContext.SessionState.Path.
        GetUnresolvedProviderPathFromPSPath($ProfileRoot))

$skillName = Split-Path -Leaf $skillRoot
$validator = Join-Path $PSScriptRoot 'Validate-Skills.ps1'
$pwsh = Join-Path $PSHOME $(if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' })
$validationOutput = & $pwsh -NoProfile -File $validator $skillRoot -Quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "The source skill failed validation:`n$($validationOutput -join "`n")"
}

$reparsePoint = Get-ChildItem -LiteralPath $skillRoot -Recurse -Force |
    Where-Object {
        ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
    } |
    Select-Object -First 1
if ($null -ne $reparsePoint) {
    throw "The source contains a reparse point: '$($reparsePoint.FullName)'."
}

if (Test-NetworkPath $skillRoot) {
    throw 'The source skill cannot be installed from a network share.'
}

$git = Get-Command git -CommandType Application -ErrorAction SilentlyContinue |
    Select-Object -First 1
if ($null -eq $git) {
    throw 'Git is required to verify source and destination repository boundaries.'
}

if ($Private) {
    Assert-PrivateSource $skillRoot $git.Source
}

$targets = [System.Collections.Generic.List[object]]::new()
foreach ($name in ($TargetHost | Select-Object -Unique)) {
    $root = [System.IO.Path]::GetFullPath((Get-UserSkillsRoot $name))
    $existing = $targets | Where-Object { Test-PathEqual $_.Root $root } |
        Select-Object -First 1
    if ($null -ne $existing) {
        $existing.Hosts.Add($name)
        continue
    }

    $hosts = [System.Collections.Generic.List[string]]::new()
    $hosts.Add($name)
    $targets.Add([pscustomobject]@{
            Root = $root
            Hosts = $hosts
            Destination = Join-Path $root $skillName
            ExistingAncestor = $null
            RootPreExisting = $false
        })
}

$sharedRoot = Get-UserSkillsRoot 'shared-agents'
$usesSharedRoot = @($targets | Where-Object {
        Test-PathEqual $_.Root $sharedRoot
    }).Count -gt 0
if ($Private -and
    ($targets.Count -gt 1 -or $usesSharedRoot) -and
    -not $AllowPrivateMultiHostExposure) {
    throw 'A private skill requires -AllowPrivateMultiHostExposure for multiple roots or ~/.agents/skills.'
}

$syncRoots = @(
    $env:OneDrive,
    $env:OneDriveConsumer,
    $env:OneDriveCommercial,
    $env:Dropbox,
    $env:GoogleDrive
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

foreach ($target in $targets) {
    if (Test-NetworkPath $target.Root) {
        throw "The destination cannot be a network share: '$($target.Root)'."
    }

    $target.RootPreExisting = Test-Path -LiteralPath $target.Root -PathType Container
    $target.ExistingAncestor = Get-ExistingAncestor $target.Root
    if (-not (Test-Path -LiteralPath $target.ExistingAncestor -PathType Container)) {
        throw "The destination path is blocked by a file: '$($target.ExistingAncestor)'."
    }

    $destinationRepository = & $git.Source -C $target.ExistingAncestor `
        rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0) {
        throw "The destination is inside a Git worktree: '$destinationRepository'."
    }

    if ($Private) {
        $destinationReparsePoint = Get-ReparsePointInPath $target.ExistingAncestor
        if ($null -ne $destinationReparsePoint) {
            throw "The destination path contains a reparse point: '$destinationReparsePoint'."
        }
    }

    if ($Private) {
        foreach ($syncRoot in $syncRoots) {
            if (Test-PathWithin $target.Destination $syncRoot) {
                throw "The destination is under a synchronized folder: '$syncRoot'."
            }
        }
    }

    if (Test-PathWithin $target.Destination $skillRoot) {
        throw 'A destination cannot be inside the source skill directory.'
    }

    if ((Test-Path -LiteralPath $target.Destination) -and -not $Force) {
        throw "'$($target.Destination)' already exists. Pass -Force to replace it."
    }
}

$approvedTargets = @($targets | Where-Object {
        $PSCmdlet.ShouldProcess(
            $_.Destination,
            "Install '$skillName' for $($_.Hosts -join ', ')")
    })
if ($approvedTargets.Count -eq 0) {
    return
}

$sourceManifest = @(Get-TreeManifest $skillRoot)
$states = [System.Collections.Generic.List[object]]::new()
$committed = $false

try {
    foreach ($target in $approvedTargets) {
        $stagingPath = Join-Path $target.Root (
            ".$skillName.install-$([guid]::NewGuid().ToString('N'))")
        $state = [pscustomobject]@{
                Target = $target
                StagingPath = $stagingPath
                BackupPath = $null
                Installed = $false
            }
        $states.Add($state)

        New-Item -ItemType Directory -Path $target.Root -Force | Out-Null
        Copy-Item -LiteralPath $skillRoot -Destination $stagingPath -Recurse -Force

        $stagingManifest = @(Get-TreeManifest $stagingPath)
        $differences = Compare-Object $sourceManifest $stagingManifest -Property Path, Hash
        if ($differences) {
            throw "The staged copy for '$($target.Root)' does not match the source."
        }
    }

    foreach ($state in $states) {
        if (Test-Path -LiteralPath $state.Target.Destination) {
            $state.BackupPath = "$($state.Target.Destination).backup-$([guid]::NewGuid().ToString('N'))"
            Move-Item -LiteralPath $state.Target.Destination -Destination $state.BackupPath
        }

        Move-Item -LiteralPath $state.StagingPath -Destination $state.Target.Destination
        $state.Installed = $true
    }
    $committed = $true
}
catch {
    $rollbackStates = @($states)
    [array]::Reverse($rollbackStates)
    foreach ($state in $rollbackStates) {
        if ($state.Installed -and
            (Test-Path -LiteralPath $state.Target.Destination)) {
            Remove-Item -LiteralPath $state.Target.Destination -Recurse -Force
        }

        if ($null -ne $state.BackupPath -and
            (Test-Path -LiteralPath $state.BackupPath)) {
            Move-Item -LiteralPath $state.BackupPath -Destination $state.Target.Destination
        }

        if (Test-Path -LiteralPath $state.StagingPath) {
            Remove-Item -LiteralPath $state.StagingPath -Recurse -Force
        }

        if (-not $state.Target.RootPreExisting) {
            Remove-EmptyDirectoriesToAncestor `
                $state.Target.Root `
                $state.Target.ExistingAncestor
        }
    }

    throw
}
finally {
    foreach ($state in $states) {
        if (Test-Path -LiteralPath $state.StagingPath) {
            Remove-Item -LiteralPath $state.StagingPath -Recurse -Force
        }
    }
}

if ($committed) {
    foreach ($state in $states) {
        if ($null -ne $state.BackupPath) {
            Remove-Item -LiteralPath $state.BackupPath -Recurse -Force
        }
    }
}

foreach ($state in $states) {
    [pscustomobject]@{
        Skill = $skillName
        Hosts = $state.Target.Hosts -join ', '
        Scope = 'user'
        Mode = 'copy'
        Private = [bool]$Private
        Destination = $state.Target.Destination
        Status = 'Installed'
    }
}
