<#
.SYNOPSIS
    Open a speedscope flame graph in the browser with the profile already loaded
    AND a chosen view active (default: Left Heavy) - no manual load, no manual
    view toggle.

.DESCRIPTION
    The `npx speedscope <file>` CLI auto-loads a profile but always opens in the
    default Time Order view; it gives no way to pick the view it opens in. The
    speedscope app does support a `view` hash parameter, but only when it controls
    the URL - which the CLI does not expose.

    This script takes the same approach as the Perfetto opener: it serves the
    profile from a one-shot local HTTP listener with the CORS header speedscope.app
    requires, then opens

        https://www.speedscope.app/#profileURL=http://127.0.0.1:<port>/<file>&view=left-heavy&title=<name>

    speedscope fetches the profile over loopback and applies the requested view on
    load. Nothing is uploaded; the profile stays on the loopback interface, and a
    browser permits the https app to fetch http://127.0.0.1 because loopback is a
    trustworthy origin.

    The three views map to speedscope's modes:
      - time-ordered : the default chronological flame chart
      - left-heavy   : stacks merged and sorted by weight (the hotspot view) [default]
      - sandwich     : the per-function caller/callee table

    The input must be a speedscope-format profile (what `traceq export` writes by
    default, or `--format speedscope`). A Chrome-trace / Perfetto export is NOT a
    speedscope profile - open those with tools/Open-PerfettoTrace.ps1.

.PARAMETER Path
    Path to the speedscope profile (.speedscope.json) to serve.

.PARAMETER View
    The speedscope view to open in: time-ordered, left-heavy, or sandwich.
    Defaults to left-heavy.

.PARAMETER Origin
    The speedscope app origin. Defaults to https://www.speedscope.app.

.PARAMETER Port
    The loopback port to serve from. Defaults to 9002 (the Perfetto opener uses
    9001).

.PARAMETER Title
    The profile title shown in the speedscope tab. Defaults to the file name.

.PARAMETER TimeoutSeconds
    Hard cap on how long the listener stays up waiting for the first fetch.
    Defaults to 300.

.PARAMETER NoOpenBrowser
    Print the deep link instead of launching the browser (useful for testing).

.EXAMPLE
    ./tools/Open-SpeedscopeTrace.ps1 BenchmarkDotNet.Artifacts/flamegraphs/net10-nettrace.speedscope.json

.EXAMPLE
    ./tools/Open-SpeedscopeTrace.ps1 trace.speedscope.json -View sandwich

.NOTES
    The fully offline alternative is `npx -y speedscope <file>`, which embeds the
    profile in a self-contained temp HTML but cannot set the initial view.
    Companion: tools/Open-PerfettoTrace.ps1 for Chrome-trace / Perfetto exports.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Path,
    [ValidateSet("time-ordered", "left-heavy", "sandwich")]
    [string]$View = "left-heavy",
    [string]$Origin = "https://www.speedscope.app",
    [int]$Port = 9002,
    [string]$Title,
    [int]$TimeoutSeconds = 300,
    [switch]$NoOpenBrowser
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "Profile file not found: $Path"
    exit 1
}

$full = (Resolve-Path -LiteralPath $Path).Path
$fname = [System.IO.Path]::GetFileName($full)
if ([string]::IsNullOrWhiteSpace($Title)) {
    $Title = $fname
}

# Strip any path/query from the origin so the CORS allow-origin is just scheme://host.
$originUri = [System.Uri]$Origin
$allowOrigin = "{0}://{1}" -f $originUri.Scheme, $originUri.Authority

$profileUrl = "http://127.0.0.1:$Port/$([System.Uri]::EscapeDataString($fname))"
$deepLink = "$allowOrigin/#profileURL=$([System.Uri]::EscapeDataString($profileUrl))" +
    "&view=$View" +
    "&title=$([System.Uri]::EscapeDataString($Title))"

$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://127.0.0.1:$Port/")
try {
    $listener.Start()
}
catch [System.Net.HttpListenerException] {
    Write-Error ("Could not bind http://127.0.0.1:$Port/ - $($_.Exception.Message). " +
        "Port $Port may be in use by a previous run.")
    exit 1
}

Write-Host "Serving $fname on http://127.0.0.1:$Port/ (CORS origin $allowOrigin)" -ForegroundColor Cyan
if ($NoOpenBrowser) {
    Write-Host "Open in browser: $deepLink" -ForegroundColor Yellow
}
else {
    Write-Host "Opening speedscope ($View view) with the profile preloaded..." -ForegroundColor Green
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
                # Preflight (only sent if speedscope ever makes a non-simple request).
                $response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS"
                $response.Headers["Access-Control-Allow-Headers"] = "*"
                $response.StatusCode = 204
            }
            elseif (($request.HttpMethod -in "GET", "HEAD") -and $requestPath -eq "/$fname") {
                $bytes = [System.IO.File]::ReadAllBytes($full)
                $response.ContentType = "application/json"
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

        # Once speedscope has fetched the profile it lives in browser memory; keep the
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
    Write-Warning "speedscope never fetched the profile before the timeout. Was the browser blocked?"
    exit 2
}

Write-Host "Done - profile delivered to speedscope ($View view)." -ForegroundColor Cyan
