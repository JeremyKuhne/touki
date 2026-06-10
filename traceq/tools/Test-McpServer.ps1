#!/usr/bin/env pwsh
# Copyright (c) 2025 Jeremy W Kuhne
# SPDX-License-Identifier: MIT
# See LICENSE file in the project root for full license information

<#
.SYNOPSIS
  Validates the traceq MCP server's two wire-protocol contracts as build artifacts.

.DESCRIPTION
  Enforces the two checks born with the MCP facade (docs/traceq-implementation-plan.md,
  milestone M3):

    1. stdout purity - stdout carries only JSON-RPC. The server is run with a
       deliberately chatty log level (Trace) forced through configuration; every
       line it writes to stdout must still parse as JSON, proving the logging
       providers are pinned to stderr and cannot corrupt the protocol stream.
    2. schema budget - the tool list a client sends to the model must stay small.
       The serialized `tools` array from a real `tools/list` round-trip is measured
       and the estimated token cost must stay within the budget, so the curated
       surface cannot grow into an unscannable wall that crowds the model's context.

  Drives the server over stdio exactly as a client would: initialize, initialized,
  then tools/list. Run from the traceq subtree root (the directory holding
  traceq.slnx).

.PARAMETER Configuration
  The build configuration whose MCP binary to exercise. Defaults to Release.

.PARAMETER MaxSchemaTokens
  The tool-list token budget. Defaults to 4000. Tokens are estimated at four
  characters each; the check prints the measured characters and estimate so a
  regression is legible.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [int]$MaxSchemaTokens = 4000
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$mcpDll = Join-Path $root "src/TraceQ.Mcp/bin/$Configuration/net10.0/TraceQ.Mcp.dll"

if (-not (Test-Path $mcpDll)) {
    throw "MCP binary not found at '$mcpDll'. Build the solution first (dotnet build traceq.slnx -c $Configuration)."
}

$failures = [System.Collections.Generic.List[string]]::new()
function Add-Failure([string]$message) { $failures.Add($message) }

# Drive the server over stdio exactly as a client would.
$psi = [System.Diagnostics.ProcessStartInfo]::new()
$psi.FileName = 'dotnet'
$psi.ArgumentList.Add($mcpDll)
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.UseShellExecute = $false
# Force a deliberately chatty log level so the run exercises the logging path; the
# server must keep every one of these off stdout. The double underscore is the
# configuration-provider nesting separator (Logging:LogLevel:Default).
$psi.Environment['Logging__LogLevel__Default'] = 'Trace'

# A JSON-RPC 2.0 message carries jsonrpc == "2.0"; the purity contract is that
# stdout lines are well-formed JSON-RPC, not merely parseable JSON.
function Test-IsJsonRpc20($root) {
    try {
        $field = $root.GetProperty('jsonrpc')
        return ($field.ValueKind -eq [System.Text.Json.JsonValueKind]::String) -and ($field.GetString() -eq '2.0')
    }
    catch { return $false }
}

# The numeric JSON-RPC id of a message, or $null for a notification (no id) or a
# non-integer id. Used to identify the tools/list response by id == 2 exactly,
# rather than a substring match that would also accept id 20, 21, ...
function Get-JsonRpcId($root) {
    try {
        $field = $root.GetProperty('id')
        if ($field.ValueKind -eq [System.Text.Json.JsonValueKind]::Number) { return $field.GetInt32() }
    }
    catch { }
    return $null
}

Write-Host "Exercising the MCP server: $mcpDll"
$p = [System.Diagnostics.Process]::Start($psi)

# Drain stderr asynchronously from the moment the process starts. The check forces
# Trace logging, so the server writes a lot to stderr; if nothing reads it the OS
# pipe buffer fills, the server's next stderr write blocks, and the tools/list
# response never arrives. Draining concurrently keeps the protocol stream flowing.
$stderrTask = $p.StandardError.ReadToEndAsync()

$p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"ci","version":"1.0"}}}')
$p.StandardInput.WriteLine('{"jsonrpc":"2.0","method":"notifications/initialized"}')
$p.StandardInput.WriteLine('{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}')
$p.StandardInput.Flush()

# Single async read pump with a hard overall deadline; do not break on a per-read
# timeout because a cold server's first stdout line can take several seconds.
$stdout = [System.Collections.Generic.List[string]]::new()
$gotTools = $false
$deadline = [DateTime]::UtcNow.AddSeconds(30)
$pending = $p.StandardOutput.ReadLineAsync()
while ([DateTime]::UtcNow -lt $deadline) {
    if ($pending.Wait(500)) {
        $line = $pending.Result
        if ($null -eq $line) { break }
        $stdout.Add($line)
        # Detect the tools/list response by a real JSON-RPC id == 2, not a substring
        # (which would also match id 20, 21, ...). A non-JSON line is left for the
        # purity check below to flag.
        $trimmed = $line.Trim()
        if ($trimmed.Length -gt 0) {
            try {
                $probe = [System.Text.Json.JsonDocument]::Parse($trimmed)
                if ((Get-JsonRpcId $probe.RootElement) -eq 2) { $gotTools = $true; break }
            }
            catch { }
        }
        $pending = $p.StandardOutput.ReadLineAsync()
    }
}

$p.StandardInput.Close()
# Wait for the process - and its redirected pipes - to fully close before reading
# results; after a Kill the streams can still be mid-flight, which races the stderr
# drain. A plain WaitForExit() after Kill blocks until the pipes are closed.
if (-not $p.WaitForExit(5000)) {
    $p.Kill()
    $p.WaitForExit()
}

# The process has exited, so the stderr drain has completed; collect it for the
# failure output. Surfaced only on failure so a passing run stays quiet.
$stderrOutput = $stderrTask.GetAwaiter().GetResult()

if (-not $gotTools) {
    throw (
        "The server did not return a tools/list response within the deadline.`n" +
        "stdout:`n$($stdout -join "`n")`n" +
        "stderr:`n$stderrOutput")
}

# 1. stdout purity: every non-empty stdout line must be a JSON-RPC 2.0 message, not
# merely parseable JSON. The tools/list response is the one whose id == 2.
$toolsLine = $null
foreach ($line in $stdout) {
    $trimmed = $line.Trim()
    if ($trimmed.Length -eq 0) { continue }
    try {
        $doc = [System.Text.Json.JsonDocument]::Parse($trimmed)
    }
    catch {
        Add-Failure "Non-JSON line on stdout (would corrupt the JSON-RPC stream): $trimmed"
        continue
    }

    if (-not (Test-IsJsonRpc20 $doc.RootElement)) {
        Add-Failure "Line on stdout is not a JSON-RPC 2.0 message (missing jsonrpc=2.0): $trimmed"
        continue
    }

    if ((Get-JsonRpcId $doc.RootElement) -eq 2) { $toolsLine = $trimmed }
}

# 2. schema budget: measure the serialized tools array from the tools/list response.
$toolNames = @()
if ($null -ne $toolsLine) {
    $doc = [System.Text.Json.JsonDocument]::Parse($toolsLine)
    $tools = $doc.RootElement.GetProperty('result').GetProperty('tools')
    foreach ($t in $tools.EnumerateArray()) { $toolNames += $t.GetProperty('name').GetString() }

    $serialized = $tools.GetRawText()
    $chars = $serialized.Length
    $estimatedTokens = [math]::Ceiling($chars / 4)
    Write-Host "Tool list: $($toolNames.Count) tools ($($toolNames -join ', '))"
    Write-Host "Schema size: $chars chars, ~$estimatedTokens tokens (budget $MaxSchemaTokens)"
    if ($estimatedTokens -gt $MaxSchemaTokens) {
        Add-Failure "Tool-list schema is ~$estimatedTokens tokens (budget $MaxSchemaTokens). Tighten descriptions or trim the surface."
    }
}
else {
    Add-Failure 'Could not locate the tools/list response to measure the schema budget.'
}

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host "MCP server check FAILED with $($failures.Count) issue(s):" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    if (-not [string]::IsNullOrWhiteSpace($stderrOutput)) {
        Write-Host ''
        Write-Host 'Server stderr (tail):' -ForegroundColor Yellow
        $tail = if ($stderrOutput.Length -gt 2048) { $stderrOutput.Substring($stderrOutput.Length - 2048) } else { $stderrOutput }
        Write-Host $tail
    }
    exit 1
}

Write-Host ''
Write-Host 'MCP server check passed.' -ForegroundColor Green
exit 0
