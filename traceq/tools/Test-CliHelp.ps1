#!/usr/bin/env pwsh
# Copyright (c) 2025 Jeremy W Kuhne
# SPDX-License-Identifier: MIT
# See LICENSE file in the project root for full license information

<#
.SYNOPSIS
  Lints the traceq CLI help surface as a build artifact.

.DESCRIPTION
  Enforces the M2 help contract (docs/traceq-implementation-plan.md, milestone M2):

    1. Every [Command] verb in the CLI is listed in the top-level help.
    2. Each verb's `--help` succeeds, shows a Usage line, and stays within the
       per-verb line budget (so help never grows into an unscannable wall).
    3. The README documents every verb with a runnable example and carries the
       canonical workflow - examples live in the README because ConsoleAppFramework
       generates the per-verb `--help` from XML docs and has no examples section.

  Run from the traceq subtree root (the directory holding traceq.slnx).

.PARAMETER Configuration
  The build configuration whose CLI binary to lint. Defaults to Release.

.PARAMETER MaxVerbHelpLines
  The per-verb `--help` line budget. Defaults to 60.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [int]$MaxVerbHelpLines = 60
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$commandsFile = Join-Path $root 'src/TraceQ/Cli/TraceCommands.cs'
$readmeFile = Join-Path $root 'README.md'
$cliDll = Join-Path $root "src/TraceQ/bin/$Configuration/net10.0/traceq.dll"

$failures = [System.Collections.Generic.List[string]]::new()
function Add-Failure([string]$message) { $failures.Add($message) }

if (-not (Test-Path $cliDll)) {
    throw "CLI binary not found at '$cliDll'. Build the solution first (dotnet build traceq.slnx -c $Configuration)."
}

# The verb set is the source of truth: every [Command("name")] in TraceCommands.
# @(...) forces an array so a single-verb surface does not collapse to a string
# (which would make foreach iterate characters).
$verbs = @(Select-String -Path $commandsFile -Pattern '\[Command\("([^"]+)"\)\]' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if ($verbs.Count -eq 0) { throw "No [Command(...)] verbs found in $commandsFile." }
Write-Host "Linting help for $($verbs.Count) verbs: $($verbs -join ', ')"

# 1. Top-level help lists every verb. If the CLI itself fails to run, fail with a
# focused message rather than letting every verb check cascade into noise.
$topHelp = (& dotnet $cliDll 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) {
    throw "Top-level help ('dotnet traceq.dll') exited with code $LASTEXITCODE.`n$topHelp"
}
foreach ($verb in $verbs) {
    if ($topHelp -notmatch "(?m)^\s+$([regex]::Escape($verb))\s") {
        Add-Failure "Top-level help does not list the '$verb' verb."
    }
}

# 2. Per-verb help: succeeds, has a Usage line, stays within the line budget.
foreach ($verb in $verbs) {
    $verbHelp = (& dotnet $cliDll $verb --help 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        Add-Failure "'$verb --help' exited with code $LASTEXITCODE."
    }
    if ($verbHelp -notmatch '(?m)^Usage:') {
        Add-Failure "'$verb --help' has no Usage: line."
    }
    # Out-String appends a trailing newline; trim it so the count reflects the
    # actually rendered lines rather than overcounting by one.
    $lineCount = ($verbHelp.TrimEnd("`r", "`n") -split "`n").Count
    if ($lineCount -gt $MaxVerbHelpLines) {
        Add-Failure "'$verb --help' is $lineCount lines (budget $MaxVerbHelpLines)."
    }
}

# 3. README documents every verb with a runnable example and carries the workflow.
$readme = Get-Content $readmeFile -Raw
if ($readme -notmatch '(?im)workflow') {
    Add-Failure "README has no 'Workflow' section."
}
foreach ($verb in $verbs) {
    # A documented example is a `traceq <verb> ...` invocation somewhere in the README.
    if ($readme -notmatch "traceq $([regex]::Escape($verb))(\s|``)") {
        Add-Failure "README has no 'traceq $verb' example."
    }
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "Help lint FAILED with $($failures.Count) issue(s):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host ''
Write-Host 'Help lint passed.' -ForegroundColor Green
exit 0
