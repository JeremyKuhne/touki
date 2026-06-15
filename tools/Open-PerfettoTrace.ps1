<#
.SYNOPSIS
    Open a Chrome-trace / Perfetto trace file in the Perfetto UI with the trace
    already loaded - no manual "Open trace file" step.

.DESCRIPTION
    The Perfetto UI (https://ui.perfetto.dev) cannot fetch a trace from a file://
    path, so a self-contained local HTML (the trick the speedscope CLI uses) does
    not work for it. Instead Perfetto supports a deep link that fetches the trace
    over HTTP:

        https://ui.perfetto.dev/#!/?url=http://127.0.0.1:9001/<file>

    This script reproduces Perfetto's own tools/open_trace_in_ui helper in
    PowerShell: it serves the trace from a one-shot local HTTP listener on
    127.0.0.1:9001 with the CORS header the UI requires, opens the deep link in the
    default browser, serves the file when the UI fetches it, then shuts the listener
    down. Nothing is uploaded anywhere - the trace stays on the loopback interface.

    Port 9001 is not arbitrary: it is the HTTP+RPC port the Perfetto UI's content
    security policy whitelists, so the fetch is only allowed from that port without
    enabling the UI's "Relax CSP" flag.

    The input must be a Chrome Trace Event Format file (what `filtrace export --format
    chromium` writes) or a native Perfetto proto trace. A speedscope export is NOT a
    Perfetto format - open those with the speedscope CLI instead.

.PARAMETER Path
    Path to the trace file to serve (Chrome-trace JSON or Perfetto proto).

.PARAMETER Origin
    The Perfetto UI origin. Defaults to https://ui.perfetto.dev; use
    http://localhost:10000 for a local Perfetto devserver.

.PARAMETER Port
    The loopback port to serve from. Defaults to 9001 (the only port the Perfetto
    UI CSP allows without the Relax-CSP flag).

.PARAMETER TimeoutSeconds
    Hard cap on how long the listener stays up waiting for the first fetch. After
    the trace is served once the listener stays up only briefly (for tab reloads)
    and then exits. Defaults to 300.

.PARAMETER PinTrack
    Regex of track name(s) to pin to the top of the timeline once the trace loads,
    via the stable dev.perfetto.PinTracksByRegex startup command. Defaults to
    'filtrace' - the single aggregate track filtrace's chromium export emits (named by
    the export's --name). Pass '' to skip pinning.

.PARAMETER NoExpand
    Skip the default dev.perfetto.ExpandTracksByRegex '.*' startup command. By
    default every track group is expanded on load so the flame graph is visible
    immediately instead of collapsed under a process row.

.PARAMETER StartupCommands
    One or more raw Perfetto startup-command JSON objects (e.g.
    '{"id":"dev.perfetto.RunQueryAndShowTab","args":["SELECT ..."]}') to run after
    the trace loads, overriding the default expand/pin commands. See
    https://perfetto.dev/docs/visualization/commands-automation-reference for the
    stable command surface.

.PARAMETER NoOpenBrowser
    Print the deep link instead of launching the browser (useful for testing).

.EXAMPLE
    ./tools/Open-PerfettoTrace.ps1 BenchmarkDotNet.Artifacts/flamegraphs/net10-etl-scoped.perfetto.json

.NOTES
    Companion to the speedscope path: tools/Open-SpeedscopeTrace.ps1 opens a
    speedscope flame graph hands-free with a chosen view (e.g. Left Heavy).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [string]$Origin = "https://ui.perfetto.dev",
    [int]$Port = 9001,
    [int]$TimeoutSeconds = 300,
    [string]$PinTrack = "filtrace",
    [switch]$NoExpand,
    [string[]]$StartupCommands,
    [switch]$NoOpenBrowser
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Trace file not found: $Path"
    exit 1
}

$full = (Resolve-Path -LiteralPath $Path).Path
$fname = [System.IO.Path]::GetFileName($full)
$encoded = [System.Uri]::EscapeDataString($fname)

# Strip any path/query from the origin so the CORS allow-origin is just scheme://host.
$originUri = [System.Uri]$Origin
$allowOrigin = "{0}://{1}" -f $originUri.Scheme, $originUri.Authority
$deepLink = "$allowOrigin/#!/?url=http://127.0.0.1:$Port/$encoded&referrer=open_trace_in_ui"

# Build the startup commands that configure the view once the trace loads. By
# default expand every track group (so the flame is visible, not collapsed under a
# process row) and pin the aggregate track to the top. Raw -StartupCommands, when
# given, replace the defaults entirely.
$commandObjects = @()
if ($PSBoundParameters.ContainsKey('StartupCommands') -and $StartupCommands.Count -gt 0) {
    foreach ($raw in $StartupCommands) {
        $commandObjects += ($raw | ConvertFrom-Json)
    }
}
else {
    if (-not $NoExpand) {
        $commandObjects += [pscustomobject]@{ id = 'dev.perfetto.ExpandTracksByRegex'; args = @('.*') }
    }

    if (-not [string]::IsNullOrWhiteSpace($PinTrack)) {
        $commandObjects += [pscustomobject]@{ id = 'dev.perfetto.PinTracksByRegex'; args = @($PinTrack) }
    }
}

if ($commandObjects.Count -gt 0) {
    # ConvertTo-Json unwraps a single-element array, so force array brackets when needed.
    $commandsJson = ConvertTo-Json -InputObject $commandObjects -Depth 6 -Compress
    if ($commandObjects.Count -eq 1) {
        $commandsJson = "[$commandsJson]"
    }

    $deepLink += "&startupCommands=" + [System.Uri]::EscapeDataString($commandsJson)
}

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
try {
    $listener.Start()
}
catch [System.Net.HttpListenerException] {
    Write-Error ("Could not bind http://127.0.0.1:$Port/ - $($_.Exception.Message). " +
        "Port $Port may be in use by a trace_processor server or a previous run.")
    exit 1
}

Write-Host "Serving $fname on http://127.0.0.1:$Port/ (CORS origin $allowOrigin)" -ForegroundColor Cyan
if ($NoOpenBrowser) {
    Write-Host "Open in browser: $deepLink" -ForegroundColor Yellow
}
else {
    Write-Host "Opening Perfetto UI with the trace preloaded..." -ForegroundColor Green
    Start-Process $deepLink | Out-Null
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
$served = 0
$task = $null
try {
    while ((Get-Date) -lt $deadline) {
        if ($null -eq $task) {
            $task = $listener.GetContextAsync()
        }

        # Poll the pending accept so the deadline is still checked while idle; the
        # same task is reused across iterations so no accept operation leaks.
        if (-not $task.Wait(1000)) {
            continue
        }

        $context = $task.Result
        $task = $null
        $request = $context.Request
        $response = $context.Response
        try {
            $response.Headers["Access-Control-Allow-Origin"] = $allowOrigin
            $response.Headers["Cache-Control"] = "no-cache"

            $requestPath = [System.Uri]::UnescapeDataString($request.Url.AbsolutePath)
            if ($request.HttpMethod -eq "OPTIONS") {
                # Preflight (only sent if the UI ever makes a non-simple request).
                $response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS"
                $response.Headers["Access-Control-Allow-Headers"] = "*"
                $response.StatusCode = 204
            }
            elseif (($request.HttpMethod -in "GET", "HEAD") -and $requestPath -eq "/$fname") {
                $bytes = [System.IO.File]::ReadAllBytes($full)

                # A Chrome-trace export is JSON; a native Perfetto capture (.pftrace,
                # .perfetto-trace, .pb) is a binary proto. Label the payload accordingly
                # so the Content-Type matches what is actually served.
                $isJson = [System.IO.Path]::GetExtension($fname).ToLowerInvariant() -eq ".json"
                $response.ContentType = if ($isJson) { "application/json" } else { "application/octet-stream" }
                $response.ContentLength64 = $bytes.Length
                if ($request.HttpMethod -eq "GET") {
                    $response.OutputStream.Write($bytes, 0, $bytes.Length)
                }

                $served++
                Write-Host ("Served {0} ({1:N0} bytes)" -f $fname, $bytes.Length) -ForegroundColor Green
            }
            else {
                $response.StatusCode = 404
            }
        }
        finally {
            $response.Close()
        }

        # Once the UI has fetched the trace it lives in browser memory; keep the
        # listener up only a short grace window so a tab reload still works, then exit.
        if ($served -eq 1) {
            $grace = (Get-Date).AddSeconds(60)
            if ($grace -lt $deadline) {
                $deadline = $grace
            }
        }
    }
}
finally {
    $listener.Stop()
    $listener.Close()
}

if ($served -eq 0) {
    Write-Warning "The Perfetto UI never fetched the trace before the timeout. Was the browser blocked?"
    exit 2
}

Write-Host "Done - trace delivered to the Perfetto UI." -ForegroundColor Cyan
