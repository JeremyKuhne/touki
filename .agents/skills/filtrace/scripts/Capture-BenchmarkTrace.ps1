<#
.SYNOPSIS
    Capture a .NET CPU trace (EventPipe or ETW) of a BenchmarkDotNet benchmark, then
    print the filtrace commands to analyze it. This helper drives the capture step via
    BenchmarkDotNet's EventPipe or ETW profiler.

.DESCRIPTION
    Wraps the "record a trace, then analyze it" loop for a BenchmarkDotNet perf
    project. Run it from the repository root. Each invocation passes a run-specific
    `--artifacts` directory and `--keepFiles`, enumerates every profiler output in
    that run, and emits a durable manifest with parameterized benchmark identity,
    trace pairs, runtime/source identity, and exact symbols when verified:

      - EventPipe (-Profiler EP, the default): cross-platform, no elevation, single
        process. BenchmarkDotNet normally writes a raw .nettrace and a paired derived
        .speedscope.json. The manifest retains both; analysis commands use the raw
        trace when present, or limit a speedscope-only case to CPU/export commands.
      - ETW (-Profiler ETW): Windows only, self-elevates (one UAC prompt), machine
        wide. Uses `-p ETW --keepFiles`, which writes a .etl. Only an
        .etl carries wall-clock (threadtime), the native GC / JIT / memcpy split
        (classify --native-symbols), and multi-process scoping.

    Full BenchmarkDotNet output is written only to capture.log. The final Text or Json
    handoff contains the manifest path, warnings, and commands only for analyses whose
    captureStatus is known-enabled. Enabled-zero remains actionable; disabled and
    unknown analyses are explained instead of receiving commands. Quiet Text mode
    emits warnings only.

    Every invocation writes BenchmarkDotNet output and capture.log under a unique
    BenchmarkDotNet.Artifacts/filtrace-runs/<RunId> directory, then emits manifest.json
    with every parameterized capture case. A same-project/same-TFM handle lock rejects
    overlap before any build starts. Logged child OutDir paths are verified with
    filtrace info; source commands are printed only when an exact PDB maps sampled
    frames. No globally newest artifact is selected.

    filtrace: https://github.com/JeremyKuhne/filtrace - install once with
    `dotnet tool install -g KlutzyNinja.Filtrace`, or drive the MCP trace_* tools.

.PARAMETER Project
    Path to the perf project - a .csproj or the directory holding one. Required.

.PARAMETER Filter
    BenchmarkDotNet --filter glob selecting the benchmark(s), e.g. '*GlobMatchBench*'.
    Profile one at a time for a clean trace. Required.

.PARAMETER Profiler
    'EP' (EventPipe, default) or 'ETW' (Windows, self-elevating).

.PARAMETER Tfm
    Target-framework moniker to run. Default net10.0.

.PARAMETER Process
    Process-name substring the printed ETW commands scope to with --process.
    Defaults to the project file's base name (the benchmark host).

.PARAMETER Top
    Rows per ranking in the printed commands. Default 25.

.PARAMETER OperationCount
    Optional positive operation count represented by each captured case. Specify
    together with OperationUnit to enable per-operation manifest comparison.

.PARAMETER OperationUnit
    Optional operation unit (for example items or requests). Specify together with
    OperationCount. Both fields are omitted when neither parameter is supplied.

.PARAMETER ElevatedTimeoutSeconds
    How long the non-elevated parent waits for the self-elevated ETW capture to finish
    before it stops blocking and reports the capture.log path. Default 1200 (20 minutes). Only
    the ETW self-elevation path uses it - it is the backstop that keeps a never-signaled
    elevated child from hanging the parent indefinitely.

.PARAMETER RunId
    Optional stable identifier for this capture run. Defaults to a UTC timestamp plus
    a random suffix. The run's BenchmarkDotNet artifacts and capture log are written
    under BenchmarkDotNet.Artifacts/filtrace-runs/<RunId>.

.PARAMETER DotnetPath
    Path or command name for the dotnet host. Defaults to dotnet from PATH.

.PARAMETER FiltracePath
    Path or command name for filtrace. When it resolves, the helper verifies version
    0.6.0 or newer and info JSON schema 8 before capture, then uses it to verify which
    logged BenchmarkDotNet child output has an exact PDB match for each trace. When it
    does not resolve, recorder-established analysis statuses remain the fallback.

.PARAMETER Format
    Final result format: Text (default) or Json. BenchmarkDotNet output always stays
    in capture.log.

.PARAMETER Quiet
    Suppress informational progress in Text mode. Warnings and errors still surface.

.PARAMETER ElevatedChild
    Internal switch reserved for the self-elevated ETW child process. Do not pass it
    directly; non-ETW or non-elevated use is rejected.

.EXAMPLE
    ./Capture-BenchmarkTrace.ps1 -Project src/App.Perf -Filter '*GlobMatchBench*'

.EXAMPLE
    ./Capture-BenchmarkTrace.ps1 -Project src/App.Perf -Filter '*GlobMatchBench*' -Profiler ETW
#>
param(
    [Parameter(Mandatory)][string]$Project,
    [Parameter(Mandatory)][string]$Filter,
    [ValidateSet('EP', 'ETW')][string]$Profiler = 'EP',
    [string]$Tfm = 'net10.0',
    [string]$Process,
    [int]$Top = 25,
    [ValidateScript({ $_ -gt 0 -and -not [double]::IsNaN($_) -and -not [double]::IsInfinity($_) })]
    [double]$OperationCount,
    [ValidateLength(1, 64)][string]$OperationUnit,
    [ValidateRange(1, 2147483647)][int]$ElevatedTimeoutSeconds = 1200,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$')][string]$RunId,
    [string]$DotnetPath = 'dotnet',
    [string]$FiltracePath = 'filtrace',
    [ValidateSet('Text', 'Json')][string]$Format = 'Text',
    [switch]$Quiet,
    [switch]$ElevatedChild
)

$ErrorActionPreference = 'Stop'
$showProgress = $Format -eq 'Text' -and -not $Quiet

function Write-CaptureMetadata([string]$TracePath, [System.Collections.IDictionary]$Analyses) {
    $metadata = [ordered]@{
        schemaVersion = 1
        analyses = $Analyses
    } | ConvertTo-Json -Depth 3 -Compress
    $encoding = New-Object System.Text.UTF8Encoding($false)
    try {
        [System.IO.File]::WriteAllText("$TracePath.filtrace.json", $metadata, $encoding)
    }
    catch {
        Write-Warning "Capture succeeded, but metadata could not be written: $($_.Exception.Message). Provider enablement will be unknown during analysis."
    }
}

function Write-RunManifest([string]$Path, [System.Collections.IDictionary]$Manifest) {
    $maxManifestBytes = 16MB
    try {
        $json = $Manifest | ConvertTo-Json -Depth 8 -Compress
    }
    catch {
        throw "Capture manifest could not be serialized at '$Path': $($_.Exception.Message)"
    }
    $encoding = New-Object System.Text.UTF8Encoding($false)
    $manifestBytes = $encoding.GetByteCount($json)
    if ($manifestBytes -ge $maxManifestBytes) {
        throw "Capture manifest is $manifestBytes UTF-8 bytes; the durable manifest safety limit is 16 MiB. Narrow the benchmark filter or split the capture into fewer cases."
    }

    [System.IO.File]::WriteAllText($Path, $json, $encoding)
}

function Get-RuntimeSummaries([string]$LogPath) {
    $finalSummaries = New-Object 'System.Collections.Generic.List[string]'
    $caseSummaries = New-Object 'System.Collections.Generic.List[string]'
    foreach ($logLine in Get-Content -LiteralPath $LogPath) {
        # Strip Get-Content's provider ETS properties; the 5.1 JSON serializer
        # otherwise recurses through PSProvider and can exhaust memory.
        $line = [string]::new($logLine.ToCharArray()).Trim()
        if ($line -match '^Runtime\s+=\s*(.+)$') {
            $finalSummaries.Add("Runtime = $($Matches[1].Trim())")
        }
        elseif ($line -match '^//\s*Runtime\s*=\s*(.+)$') {
            $caseSummaries.Add("Runtime = $($Matches[1].Trim())")
        }
    }

    # Final report rows carry configuration details such as GC mode. Replace only
    # the per-case identity they enrich; preserve unmatched per-case rows when a
    # failed or partial multi-runtime run omitted its final report row.
    $summaries = New-Object 'System.Collections.Generic.List[string]'
    foreach ($finalSummary in $finalSummaries | Sort-Object -Unique) {
        $summaries.Add($finalSummary)
    }
    foreach ($caseSummary in $caseSummaries | Sort-Object -Unique) {
        $coveredByFinalSummary = $false
        foreach ($finalSummary in $finalSummaries) {
            if ($finalSummary -eq $caseSummary -or
                $finalSummary.StartsWith("$caseSummary; ", [StringComparison]::Ordinal)) {
                $coveredByFinalSummary = $true
                break
            }
        }
        if (-not $coveredByFinalSummary) {
            $summaries.Add($caseSummary)
        }
    }

    return @($summaries | Sort-Object -Unique)
}

function Get-CaptureCases([string]$ArtifactsDirectory, [string]$CaptureProfiler) {
    $maxCases = 256
    $casesByStem = @{}
    foreach ($file in Get-ChildItem -LiteralPath $ArtifactsDirectory -Recurse -File -ErrorAction SilentlyContinue) {
        $kind = $null
        $stem = $null
        if ($file.Name -like '*.speedscope.json') {
            $kind = 'speedscope'
            $stem = $file.Name -replace '\.speedscope\.json$', ''
        }
        elseif ($CaptureProfiler -eq 'ETW' -and $file.Extension -eq '.etl') {
            $kind = 'trace'
            $stem = $file.BaseName
        }
        elseif ($CaptureProfiler -eq 'EP' -and $file.Extension -eq '.nettrace') {
            $kind = 'trace'
            $stem = $file.BaseName
        }
        else {
            continue
        }

        if (-not $casesByStem.ContainsKey($stem)) {
            if ($casesByStem.Count -ge $maxCases) {
                throw "Capture produced more than $maxCases cases; narrow the benchmark filter."
            }

            $casesByStem[$stem] = [ordered]@{
                id = $stem
                benchmarkId = $null
                benchmark = $null
                parameters = $null
                benchmarkDisplay = $null
                runtime = $null
                capturedUtc = $file.LastWriteTimeUtc.ToString('O')
                trace = $null
                speedscope = $null
                symbolsDirectory = $null
                operationCount = $null
                operationUnit = $null
                symbolCandidates = @()
                analyses = [ordered]@{}
                commands = @()
                warnings = @()
            }
        }

        $casesByStem[$stem][$kind] = $file.FullName
        if ($kind -eq 'trace') {
            $casesByStem[$stem].capturedUtc = $file.LastWriteTimeUtc.ToString('O')
        }
    }

    return @($casesByStem.Values | Sort-Object { $_.capturedUtc }, { $_.id })
}

function Get-SymbolCandidates([string]$CaptureLog, [string]$OuterSymbolsDirectory) {
    $maxCandidates = 32
    $candidates = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $outerCandidate = Get-LocalDirectoryCandidate $OuterSymbolsDirectory
    if ($outerCandidate) {
        [void]$candidates.Add($outerCandidate)
    }

    :captureLog foreach ($line in Get-Content -LiteralPath $CaptureLog) {
        foreach ($match in [regex]::Matches($line, '/p:OutDir="([^"]+)"')) {
            $directory = Get-LocalDirectoryCandidate $match.Groups[1].Value
            if ($directory) {
                [void]$candidates.Add($directory)
                if ($candidates.Count -ge $maxCandidates) { break captureLog }
            }
        }
    }

    return @($candidates | Sort-Object)
}

function Get-LocalDirectoryCandidate([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or
        $Path.StartsWith('\\', [StringComparison]::Ordinal) -or
        $Path.StartsWith('//', [StringComparison]::Ordinal)) {
        return $null
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
        if ($fullPath.StartsWith('\\', [StringComparison]::Ordinal) -or
            $fullPath.StartsWith('//', [StringComparison]::Ordinal) -or
            -not (Test-Path -LiteralPath $fullPath -PathType Container)) {
            return $null
        }

        return $fullPath
    }
    catch {
        return $null
    }
}

function Set-BenchmarkIdentities(
    [System.Collections.IDictionary[]]$CaptureCases,
    [string]$CaptureLog,
    [string]$FiltraceCommand,
    [bool]$CanInspectTraces) {
    $benchmarksInExecutionOrder = New-Object 'System.Collections.Generic.List[System.Collections.IDictionary]'
    $currentDisplay = $null
    $pendingBenchmark = $null
    foreach ($line in Get-Content -LiteralPath $CaptureLog) {
        if ($line -match '^// Benchmark: (.+)$') {
            $currentDisplay = $Matches[1]
            $pendingBenchmark = $null
            continue
        }

        # Comment-prefixed runtime rows belong to the active Execute block. The
        # unprefixed Runtime rows are the final report table and stay manifest-wide.
        if ($null -ne $pendingBenchmark -and $line -match '^//\s*Runtime\s*=') {
            $pendingBenchmark.runtime = [string]::new($line.ToCharArray())
            $pendingBenchmark = $null
            continue
        }

        if ($null -ne $currentDisplay -and
            $line -match '--benchmarkName\s+(.+?)(?=\s+--[A-Za-z])') {
            $benchmarkNameArgument = $Matches[1]
            if ($line -notmatch '--benchmarkId\s+(\d+)') { continue }
            $benchmarkId = [int]$Matches[1]
            $fullBenchmarkName = ConvertFrom-BenchmarkNameArgumentFull $benchmarkNameArgument
            $benchmark = [ordered]@{
                benchmarkId = $benchmarkId
                benchmark = Get-BenchmarkName $fullBenchmarkName
                fullBenchmarkName = $fullBenchmarkName
                parameters = Get-BenchmarkParameters $currentDisplay
                benchmarkDisplay = $currentDisplay
                runtime = $null
                assigned = $false
            }
            $benchmarksInExecutionOrder.Add($benchmark)
            $pendingBenchmark = $benchmark
        }
    }

    if ($CanInspectTraces) {
        foreach ($captureCase in $CaptureCases) {
            if (-not $captureCase.trace) { continue }
            $traceBenchmarkName = Get-TraceBenchmarkName $captureCase.trace $FiltraceCommand
            if ([string]::IsNullOrWhiteSpace($traceBenchmarkName)) { continue }
            $matches = @(
                $benchmarksInExecutionOrder |
                    Where-Object {
                        -not $_.assigned -and
                        $_.fullBenchmarkName -ceq $traceBenchmarkName
                    }
            )
            if ($matches.Count -ne 1) { continue }
            Set-CaptureCaseIdentity $captureCase $matches[0]
            $matches[0].assigned = $true
        }
    }

    $benchmarkNames = @(
        $benchmarksInExecutionOrder.benchmark |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -Unique
    )
    foreach ($benchmarkName in $benchmarkNames) {
        $benchmarks = @(
            $benchmarksInExecutionOrder |
                Where-Object { -not $_.assigned -and $_.benchmark -eq $benchmarkName }
        )
        $cases = @(
            $CaptureCases |
                Where-Object {
                    $null -eq $_.benchmarkId -and
                    $_.id -notmatch '-hash\d+(?:-|$)' -and
                    ($_.id.StartsWith("$benchmarkName-", [StringComparison]::Ordinal) -or
                        $_.id.StartsWith("$benchmarkName(", [StringComparison]::Ordinal))
                } |
                Sort-Object { $_.capturedUtc }, { $_.id }
        )
        if ($benchmarks.Count -ne $cases.Count) { continue }

        # Parameter values are not encoded in profiler filenames. BenchmarkDotNet
        # executes cases sequentially, so within one exact benchmark name use logged
        # execution order only when each completed trace has a distinct timestamp.
        # Otherwise leave identity null rather than silently mis-pair parameters.
        if ($cases.Count -gt 1 -and @($cases.capturedUtc | Select-Object -Unique).Count -ne $cases.Count) {
            continue
        }

        for ($index = 0; $index -lt $cases.Count; $index++) {
            Set-CaptureCaseIdentity $cases[$index] $benchmarks[$index]
            $benchmarks[$index].assigned = $true
        }
    }
}

function Set-CaptureCaseIdentity(
    [System.Collections.IDictionary]$CaptureCase,
    [System.Collections.IDictionary]$Benchmark) {
    $CaptureCase.benchmarkId = $Benchmark.benchmarkId
    $CaptureCase.benchmark = $Benchmark.benchmark
    $CaptureCase.parameters = $Benchmark.parameters
    $CaptureCase.benchmarkDisplay = $Benchmark.benchmarkDisplay
    $CaptureCase.runtime = $Benchmark.runtime
}

function Get-TraceBenchmarkName([string]$TracePath, [string]$FiltraceCommand) {
    try {
        $global:LASTEXITCODE = 0
        $json = & $FiltraceCommand events $TracePath `
            --name 'BenchmarkDotNet.EngineEventSource/Benchmark/Start' `
            --take 2 --max-payload 4096 --format json 2>$null | Out-String
        if ($LASTEXITCODE -ne 0) { return $null }
        $result = ($json | ConvertFrom-Json).result
        $events = @(
            $result.events |
                Where-Object {
                    $_.provider -ceq 'BenchmarkDotNet.EngineEventSource' -and
                    $_.eventName -ceq 'Benchmark/Start'
                }
        )
            if ([int]$result.totalMatched -ne 1 -or $events.Count -ne 1) { return $null }
        $payload = [string]$events[0].payload
        $prefix = 'benchmarkName='
        if (-not $payload.StartsWith($prefix, [StringComparison]::Ordinal)) { return $null }
        return $payload.Substring($prefix.Length)
    }
    catch {
        return $null
    }
}

function ConvertFrom-BenchmarkNameArgumentFull([string]$BenchmarkNameArgument) {
    if ([string]::IsNullOrWhiteSpace($BenchmarkNameArgument)) { return $null }

    $decoded = [regex]::Replace(
        $BenchmarkNameArgument.Trim(),
        '(?:\\+u0026#34;|&#34;|&quot;)',
        '"').Trim('"').Replace('\"', '"')
    if ([string]::IsNullOrWhiteSpace($decoded)) { return $null }
    return $decoded
}

function ConvertFrom-BenchmarkNameArgument([string]$BenchmarkNameArgument) {
    return Get-BenchmarkName (ConvertFrom-BenchmarkNameArgumentFull $BenchmarkNameArgument)
}

function Get-BenchmarkName([string]$FullBenchmarkName) {
    if ([string]::IsNullOrWhiteSpace($FullBenchmarkName)) { return $null }
    $parameters = $FullBenchmarkName.IndexOf('(')
    if ($parameters -ge 0) {
        return $FullBenchmarkName.Substring(0, $parameters)
    }
    return $FullBenchmarkName
}

function Get-BenchmarkParameters([string]$BenchmarkDisplay) {
    $trimmedDisplay = $BenchmarkDisplay.TrimEnd()
    if ($trimmedDisplay.EndsWith(']', [StringComparison]::Ordinal)) {
        $openBracket = $trimmedDisplay.LastIndexOf('[')
        if ($openBracket -ge 0) {
            return $trimmedDisplay.Substring(
                $openBracket + 1,
                $trimmedDisplay.Length - $openBracket - 2)
        }
    }

    $close = $BenchmarkDisplay.LastIndexOf('): ', [StringComparison]::Ordinal)
    if ($close -lt 0) { return '' }
    $open = $BenchmarkDisplay.IndexOf('(')
    if ($open -ge 0 -and $open -lt $close) {
        return $BenchmarkDisplay.Substring($open + 1, $close - $open - 1)
    }

    return ''
}

function Get-SourceIdentity([string]$ProjectDirectory) {
    if ($null -eq (Get-Command git -ErrorAction SilentlyContinue)) { return $null }
    try {
        $repository = & git -C $ProjectDirectory rev-parse --show-toplevel 2>$null | Select-Object -First 1
        $commit = & git -C $ProjectDirectory rev-parse HEAD 2>$null | Select-Object -First 1
        if ($LASTEXITCODE -ne 0 -or -not $repository -or -not $commit) { return $null }
        return [ordered]@{
            repository = [System.IO.Path]::GetFullPath($repository)
            commit = $commit
        }
    }
    catch {
        return $null
    }
}

function Get-DefaultCaptureStatuses([string]$CaptureProfiler, [bool]$HasRawTrace) {
    if (-not $HasRawTrace) {
        return [ordered]@{ cpu = 'enabled' }
    }

    if ($CaptureProfiler -eq 'ETW') {
        return [ordered]@{
            cpu = 'enabled'; threadtime = 'enabled'; classify = 'enabled';
            processes = 'enabled'; diskio = 'disabled'; events = 'enabled'
        }
    }

    return [ordered]@{
        cpu = 'enabled'; alloc = 'disabled'; exceptions = 'enabled';
        contention = 'enabled'; wait = 'disabled'; activity = 'unknown';
        gcstats = 'enabled'; jitstats = 'enabled'; threadpool = 'enabled';
        events = 'enabled'
    }
}

function ConvertTo-CaptureStatus($Status) {
    switch ([string]$Status) {
        'enabled' { return 'enabled' }
        'disabled' { return 'disabled' }
        'unknown' { return 'unknown' }
        default { return 'unknown' }
    }
}

function Test-HasAnalysisInfo($TraceInfo) {
    return $null -ne $TraceInfo -and
        $null -ne $TraceInfo.analyses -and
        @($TraceInfo.analyses.PSObject.Properties).Count -gt 0
}

function ConvertTo-AnalysisMap(
    $TraceInfo,
    [System.Collections.IDictionary]$CaptureStatuses,
    [bool]$AllowRecorderFallback) {
    $analyses = [ordered]@{}
    if (Test-HasAnalysisInfo $TraceInfo) {
        foreach ($property in $TraceInfo.analyses.PSObject.Properties) {
            $analyses[$property.Name] = [ordered]@{
                captureStatus = ConvertTo-CaptureStatus $property.Value.captureStatus
                eventCount = $property.Value.eventCount
            }
        }
        return $analyses
    }

    foreach ($name in $CaptureStatuses.Keys) {
        $status = if ($AllowRecorderFallback) {
            ConvertTo-CaptureStatus $CaptureStatuses[$name]
        }
        else {
            'unknown'
        }
        $analyses[$name] = [ordered]@{
            captureStatus = $status
            eventCount = $null
        }
    }
    return $analyses
}

function Get-TraceInfoResult(
    [string]$TracePath,
    [string]$SymbolsDirectory,
    [string]$FiltraceCommand) {
    try {
        $arguments = @('info', $TracePath, '--format', 'json')
        if ($SymbolsDirectory) { $arguments += @('--symbols', $SymbolsDirectory) }
        $json = & $FiltraceCommand @arguments 2>$null | Out-String
        if ($LASTEXITCODE -ne 0) { return $null }
        return ($json | ConvertFrom-Json).result
    }
    catch {
        return $null
    }
}

function Get-ObjectPropertyInfo($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    return $Object.PSObject.Properties[$Name]
}

function Assert-FiltraceCompatibility([string]$FiltraceCommand) {
    $minimumVersion = [Version]'0.6.0'
    $expectedSchemaVersion = 8
    $upgradeGuidance = 'Upgrade with: dotnet tool update -g KlutzyNinja.Filtrace'

    try {
        $global:LASTEXITCODE = 0
        $versionOutput = (& $FiltraceCommand --version 2>$null | Out-String).Trim()
        $versionExitCode = $LASTEXITCODE
        $versionMatch = [regex]::Match($versionOutput, '(?<!\d)(\d+\.\d+\.\d+)')
        if ($versionExitCode -ne 0 -or -not $versionMatch.Success) {
            throw 'the --version query did not return a semantic version'
        }

        $resolvedVersion = [Version]$versionMatch.Groups[1].Value
        if ($resolvedVersion -lt $minimumVersion) {
            throw "version $resolvedVersion is older than required version $minimumVersion"
        }
    }
    catch {
        throw "Resolved filtrace '$FiltraceCommand' is incompatible: $($_.Exception.Message). $upgradeGuidance"
    }

    $preflightTrace = Join-Path (
        [System.IO.Path]::GetTempPath()) "filtrace-preflight-$([Guid]::NewGuid().ToString('N')).speedscope.json"
    try {
        $profile = [ordered]@{
            '$schema' = 'https://www.speedscope.app/file-format-schema.json'
            shared = [ordered]@{ frames = @([ordered]@{ name = 'preflight' }) }
            profiles = @(
                [ordered]@{
                    type = 'sampled'
                    name = 'preflight'
                    unit = 'milliseconds'
                    startValue = 0
                    endValue = 1
                    samples = ,([int[]]@(0))
                    weights = @(1)
                }
            )
            activeProfileIndex = 0
            exporter = 'filtrace compatibility preflight'
            name = 'filtrace compatibility preflight'
        } | ConvertTo-Json -Depth 6 -Compress
        $encoding = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($preflightTrace, $profile, $encoding)

        $global:LASTEXITCODE = 0
        $infoJson = & $FiltraceCommand info $preflightTrace --format json 2>$null | Out-String
        if ($LASTEXITCODE -ne 0) {
            throw 'info --format json returned a nonzero exit code'
        }

        $infoEnvelope = $infoJson | ConvertFrom-Json
        $schemaVersionProperty = Get-ObjectPropertyInfo $infoEnvelope 'schemaVersion'
        $resultProperty = Get-ObjectPropertyInfo $infoEnvelope 'result'
        $infoResult = $null
        if ($null -ne $resultProperty) { $infoResult = $resultProperty.Value }
        $analysesProperty = Get-ObjectPropertyInfo $infoResult 'analyses'
        $analyses = $null
        if ($null -ne $analysesProperty) { $analyses = $analysesProperty.Value }
        $cpuProperty = Get-ObjectPropertyInfo $analyses 'cpu'
        $cpuAnalysis = $null
        if ($null -ne $cpuProperty) { $cpuAnalysis = $cpuProperty.Value }
        $captureStatusProperty = Get-ObjectPropertyInfo $cpuAnalysis 'captureStatus'
        $eventCountProperty = Get-ObjectPropertyInfo $cpuAnalysis 'eventCount'
        if ($null -eq $schemaVersionProperty -or
            [int]$schemaVersionProperty.Value -ne $expectedSchemaVersion -or
            $null -eq $resultProperty -or
            $null -eq $analysesProperty -or
            $null -eq $cpuProperty -or
            $null -eq $captureStatusProperty -or
            $null -eq $eventCountProperty) {
            throw "info --format json did not match schema $expectedSchemaVersion"
        }
    }
    catch {
        throw "Resolved filtrace '$FiltraceCommand' is incompatible: $($_.Exception.Message). $upgradeGuidance"
    }
    finally {
        Remove-Item -LiteralPath $preflightTrace -Force -ErrorAction SilentlyContinue
    }
}

function Find-ExactSymbolDirectory([string]$TracePath, [string[]]$Candidates, [string]$FiltraceCommand) {
    $bestDirectory = $null
    $bestMappedFrames = 0
    foreach ($candidate in $Candidates) {
        $traceInfo = Get-TraceInfoResult $TracePath $candidate $FiltraceCommand
        $source = if ($null -ne $traceInfo) { $traceInfo.sourceResolution } else { $null }
        if ($null -eq $source -or $source.matchingPdbModules.Count -eq 0) { continue }
        $mappedFrames = [int]$source.mappedManagedFrameCount
        if ($mappedFrames -gt $bestMappedFrames) {
            $bestMappedFrames = $mappedFrames
            $bestDirectory = $candidate
        }
    }

    return $bestDirectory
}

function ConvertTo-PowerShellArgument([string]$Value) {
    return "'$($Value.Replace("'", "''"))'"
}

function Test-AnalysisEnabled([System.Collections.IDictionary]$Analyses, [string]$Name) {
    return $Analyses.Contains($Name) -and $Analyses[$Name].captureStatus -eq 'enabled'
}

function Get-CaseCommands(
    [System.Collections.IDictionary]$CaptureCase,
    [string]$CaptureProfiler,
    [string]$ProcessName,
    [string]$MethodFilter,
    [int]$TopRows) {
    $commands = New-Object 'System.Collections.Generic.List[string]'
    $analysisPath = if ($CaptureCase.trace) { $CaptureCase.trace } else { $CaptureCase.speedscope }
    $trace = ConvertTo-PowerShellArgument $analysisPath
    $symbols = if ($CaptureCase.symbolsDirectory) { ConvertTo-PowerShellArgument $CaptureCase.symbolsDirectory } else { $null }

    if ($CaptureProfiler -eq 'ETW') {
        $process = ConvertTo-PowerShellArgument $ProcessName
        if (Test-AnalysisEnabled $CaptureCase.analyses 'processes') {
            $commands.Add("filtrace processes $trace")
        }
        if (Test-AnalysisEnabled $CaptureCase.analyses 'cpu') {
            $commands.Add("filtrace cpu $trace --process $process --benchmark --top $TopRows")
            if ($symbols) {
                $method = ConvertTo-PowerShellArgument $MethodFilter
                $commands.Add("filtrace lines $trace --process $process --method $method --symbols $symbols")
            }
            $exportSymbols = if ($symbols) { " --symbols $symbols" } else { '' }
            $commands.Add("filtrace export $trace --process $process --benchmark --native-symbols$exportSymbols -o flame.speedscope.json")
        }
        if (Test-AnalysisEnabled $CaptureCase.analyses 'threadtime') {
            $commands.Add("filtrace threadtime $trace --process $process --benchmark --top $TopRows")
        }
        if (Test-AnalysisEnabled $CaptureCase.analyses 'classify') {
            $commands.Add("filtrace classify $trace --process $process --benchmark --native-symbols")
        }
        if (Test-AnalysisEnabled $CaptureCase.analyses 'diskio') {
            $commands.Add("filtrace diskio $trace --top $TopRows")
        }
        return @($commands)
    }

    if (Test-AnalysisEnabled $CaptureCase.analyses 'cpu') {
        $commands.Add("filtrace cpu $trace --benchmark --top $TopRows")
        if ($symbols) {
            $method = ConvertTo-PowerShellArgument $MethodFilter
            $commands.Add("filtrace lines $trace --method $method --symbols $symbols")
            $commands.Add("filtrace export $trace --benchmark --symbols $symbols -o flame.speedscope.json")
        }
        else {
            $commands.Add("filtrace export $trace --benchmark -o flame.speedscope.json")
        }
    }
    if (Test-AnalysisEnabled $CaptureCase.analyses 'alloc') {
        $commands.Add("filtrace alloc $trace --benchmark --top $TopRows")
    }
    if (Test-AnalysisEnabled $CaptureCase.analyses 'exceptions') {
        $commands.Add("filtrace exceptions $trace --benchmark --top $TopRows")
    }
    foreach ($metric in @('contention', 'wait', 'activity')) {
        if (Test-AnalysisEnabled $CaptureCase.analyses $metric) {
            $commands.Add("filtrace rank $trace --metric $metric --benchmark --top $TopRows")
        }
    }
    foreach ($report in @('gcstats', 'jitstats', 'threadpool')) {
        if (Test-AnalysisEnabled $CaptureCase.analyses $report) {
            $commands.Add("filtrace $report $trace")
        }
    }
    return @($commands)
}

function Get-CaseWarnings([System.Collections.IDictionary]$CaptureCase) {
    $warnings = New-Object 'System.Collections.Generic.List[string]'
    if ($null -eq $CaptureCase.benchmarkId -or
        [string]::IsNullOrWhiteSpace($CaptureCase.benchmark) -or
        $null -eq $CaptureCase.parameters -or
        [string]::IsNullOrWhiteSpace($CaptureCase.benchmarkDisplay)) {
        $warnings.Add('benchmark identity unavailable or ambiguous; do not use this case with manifest batch/diff; analyze the trace directly')
    }
    foreach ($name in $CaptureCase.analyses.Keys) {
        $status = $CaptureCase.analyses[$name].captureStatus
        if ($status -eq 'disabled') {
            $warnings.Add("$name capture disabled; recapture with a profile that enables it")
        }
        elseif ($status -eq 'unknown') {
            $warnings.Add("$name capture status unknown; no command emitted")
        }
    }
    if ($CaptureCase.trace -and -not $CaptureCase.symbolsDirectory) {
        $warnings.Add('source lines unavailable; no logged child output had an exact matching PDB')
    }
    return @($warnings)
}

function Write-CaptureResult(
    [object[]]$CaptureCases,
    [string]$ManifestPath,
    [string]$CaptureRunId,
    [string]$OutputFormat,
    [bool]$QuietOutput,
    [ValidateSet('completed', 'timeout')]
    [string]$Status = 'completed',
    [string]$LogPath = $null,
    [string]$Message = $null) {
    if ($OutputFormat -eq 'Json') {
        if ($Status -eq 'timeout') {
            $result = [ordered]@{
                schemaVersion = 1
                status = 'timeout'
                runId = $CaptureRunId
                manifest = $null
                log = $LogPath
                message = $Message
                warnings = @()
                cases = @()
            }
        }
        else {
            $result = [ordered]@{
                schemaVersion = 1
                status = $Status
                runId = $CaptureRunId
                manifest = $ManifestPath
                warnings = @(
                    foreach ($captureCase in $CaptureCases) {
                        foreach ($warning in $captureCase.warnings) {
                            [ordered]@{ case = $captureCase.id; message = $warning }
                        }
                    }
                )
                cases = @(
                    foreach ($captureCase in $CaptureCases) {
                        [ordered]@{
                            id = $captureCase.id
                            trace = $captureCase.trace
                            speedscope = $captureCase.speedscope
                            commands = $captureCase.commands
                        }
                    }
                )
            }
        }

        $maxResultBytes = 20KB
        $encoding = New-Object System.Text.UTF8Encoding($false)
        $runDirectoryPath = if ($ManifestPath) {
            Split-Path -Parent $ManifestPath
        }
        elseif ($LogPath) {
            Split-Path -Parent $LogPath
        }
        else {
            "BenchmarkDotNet.Artifacts/filtrace-runs/$CaptureRunId"
        }
        $json = $result | ConvertTo-Json -Depth 6 -Compress
        if ($encoding.GetByteCount($json) -ge $maxResultBytes) {
            # Completed runs have a manifest; a timed-out child may have produced only
            # partial output, so the fallback guidance differs by status.
            $fallbackMessage = if ($Status -eq 'timeout') {
                'Timeout details exceeded 20 KiB; inspect the run directory for partial output.'
            }
            else {
                'JSON handoff exceeded 20 KiB; read the manifest for full cases, commands, and warnings.'
            }
            $result = [ordered]@{
                schemaVersion = 1
                status = $Status
                runId = $CaptureRunId
                manifest = $ManifestPath
                runDirectory = $runDirectoryPath
                message = $fallbackMessage
            }
            $json = $result | ConvertTo-Json -Depth 3 -Compress
            if ($encoding.GetByteCount($json) -ge $maxResultBytes) {
                $result.manifest = $null
                $result.runDirectory = "BenchmarkDotNet.Artifacts/filtrace-runs/$CaptureRunId"
                $result.message = 'JSON handoff exceeded 20 KiB; inspect runDirectory relative to the invocation working directory.'
                $json = $result | ConvertTo-Json -Depth 3 -Compress
            }
        }

        $json
        return
    }

    if ($Status -eq 'timeout') {
        Write-Warning $Message
        return
    }

    if (-not $QuietOutput) {
        Write-Host "`nCaptured $($CaptureCases.Count) case(s)." -ForegroundColor Green
        Write-Host "Manifest: $ManifestPath" -ForegroundColor Green
        foreach ($captureCase in $CaptureCases) {
            $analysisPath = if ($captureCase.trace) { $captureCase.trace } else { $captureCase.speedscope }
            Write-Host "`nCase: $($captureCase.id)" -ForegroundColor Green
            Write-Host "Captured: $analysisPath"
            if ($captureCase.commands.Count -gt 0) {
                Write-Host 'Next-step filtrace commands:'
                foreach ($command in $captureCase.commands) { Write-Host "  $command" }
            }
            foreach ($warning in $captureCase.warnings) { Write-Warning "[$($captureCase.id)] $warning" }
        }
        return
    }

    foreach ($captureCase in $CaptureCases) {
        foreach ($warning in $captureCase.warnings) { Write-Warning "[$($captureCase.id)] $warning" }
    }
}

# Resolve the project file (accept either a .csproj or a directory holding one).
$projItem = Get-Item -LiteralPath $Project
if ($projItem.PSIsContainer) {
    $projFile = Get-ChildItem -LiteralPath $Project -Filter *.csproj | Select-Object -First 1
    if ($null -eq $projFile) { Write-Error "No .csproj found under $Project." -ErrorAction Continue ; exit 1 }
}
else {
    $projFile = $projItem
}
if (-not $Process) { $Process = [System.IO.Path]::GetFileNameWithoutExtension($projFile.Name) }
$hasOperationCount = $PSBoundParameters.ContainsKey('OperationCount')
$hasOperationUnit = $PSBoundParameters.ContainsKey('OperationUnit') -and
    -not [string]::IsNullOrWhiteSpace($OperationUnit)
if ($hasOperationCount -ne $hasOperationUnit) {
    Write-Error 'Specify OperationCount and OperationUnit together, or omit both.' -ErrorAction Continue
    exit 1
}

$repoRoot = (Get-Location).Path
if (-not $RunId) {
    $RunId = "$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
}
$runDirectory = Join-Path $repoRoot "BenchmarkDotNet.Artifacts/filtrace-runs/$RunId"
$artifacts = Join-Path $runDirectory 'artifacts'
$log = Join-Path $runDirectory 'capture.log'

function Test-Elevated {
    $id = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object System.Security.Principal.WindowsPrincipal($id)
    return $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Test-SafeElevationArgument([string]$Value) {
    return $null -ne $Value -and
        $Value.IndexOfAny([char[]]@('"', "`r", "`n")) -lt 0 -and
        -not $Value.EndsWith('\', [StringComparison]::Ordinal)
}

if ($ElevatedChild -and $Profiler -ne 'ETW') {
    Write-Error '-ElevatedChild is reserved for the internal elevated ETW handoff.' -ErrorAction Continue
    exit 1
}

if ($Profiler -eq 'ETW') {
    $elevationArguments = [ordered]@{
        Script = $PSCommandPath
        Project = $projFile.FullName
        Filter = $Filter
        Tfm = $Tfm
        Process = $Process
        DotnetPath = $DotnetPath
        FiltracePath = $FiltracePath
    }
    if ($hasOperationUnit) { $elevationArguments.OperationUnit = $OperationUnit }
    foreach ($argument in $elevationArguments.GetEnumerator()) {
        if (-not (Test-SafeElevationArgument ([string]$argument.Value))) {
            Write-Error "ETW elevation argument '$($argument.Key)' cannot contain quotes, newlines, or end in a backslash." -ErrorAction Continue
            exit 1
        }
    }
}

# Recording an .etl is Windows-only, and Test-Elevated below calls a Windows-only API, so
# fail fast with a clear message rather than a PlatformNotSupportedException. Compare
# against $false so Windows PowerShell 5.1 (undefined $IsWindows) is not mistaken for a
# non-Windows OS.
if ($Profiler -eq 'ETW' -and $IsWindows -eq $false) {
    Write-Error 'ETW capture is Windows-only. Use -Profiler EP on this OS.' -ErrorAction Continue
    exit 1
}

if ($ElevatedChild -and -not (Test-Elevated)) {
    Write-Error '-ElevatedChild requires an elevated Windows process.' -ErrorAction Continue
    exit 1
}

$filtraceAvailable = $null -ne (Get-Command $FiltracePath -ErrorAction SilentlyContinue)
if ($filtraceAvailable) {
    Assert-FiltraceCompatibility $FiltracePath
}

# ETW kernel sessions require Administrator. When not elevated, relaunch this script
# in an elevated window, then wait for it.
# -WorkingDirectory anchors the child at the repo root so BenchmarkDotNet.Artifacts
# and capture.log are created there, not in the elevated shell's system32 directory.
if ($Profiler -eq 'ETW' -and -not (Test-Elevated)) {
    if ($showProgress) {
        Write-Host 'ETW capture needs Administrator; relaunching elevated (a UAC prompt will appear).' -ForegroundColor Yellow
    }
    # Quote path/value args so a project path, filter, or process name containing spaces
    # survives Start-Process joining the array into a single command line.
    $argList = @('-NoProfile', '-File', "`"$PSCommandPath`"", '-Project', "`"$($projFile.FullName)`"",
        '-Filter', "`"$Filter`"", '-Profiler', 'ETW', '-Tfm', "`"$Tfm`"", '-Process', "`"$Process`"", '-Top', $Top,
        '-RunId', $RunId, '-DotnetPath', "`"$DotnetPath`"", '-FiltracePath', "`"$FiltracePath`"", '-Format', $Format,
        '-ElevatedChild')
    if ($hasOperationCount) {
        $argList += @(
            '-OperationCount', $OperationCount.ToString('R', [Globalization.CultureInfo]::InvariantCulture),
            '-OperationUnit', "`"$OperationUnit`"")
    }
    if ($Quiet) { $argList += '-Quiet' }
    # Relaunch with the host that is ALREADY running this script, not a hardcoded 'pwsh' -
    # a caller on Windows PowerShell 5.1 without PowerShell 7 installed would otherwise
    # fail here with pwsh unresolved.
    $hostExe = (Get-Process -Id $PID).Path
    # Do NOT pass -Wait here. With -Verb RunAs, Start-Process -Wait can fail to release
    # after the elevated child self-closes, hanging the parent forever even though the
    # capture already finished and the .etl is on disk. Take the process object and wait on
    # it directly with a bounded WaitForExit, so a lost or access-denied handle degrades to
    # a timeout result that reports the log path instead of an indefinite hang.
    $proc = Start-Process -FilePath $hostExe -Verb RunAs -PassThru -WorkingDirectory $repoRoot -ArgumentList $argList
    if ($null -eq $proc) {
        Write-Error 'Elevated relaunch returned no process handle; cannot wait for the capture. Check for a blocked UAC prompt.' -ErrorAction Continue
        exit 1
    }
    # WaitForExit / HasExited / ExitCode can each throw (e.g. Access Denied reading the
    # elevated, higher-integrity child's handle). Under $ErrorActionPreference='Stop' an
    # uncaught throw would abort the script instead of producing the bounded timeout result,
    # so guard every handle access and treat a throw as a timeout-like miss.
    # Clamp to Int32.MaxValue so a large timeout cannot overflow the millisecond argument.
    $waitMs = [int][Math]::Min([long]$ElevatedTimeoutSeconds * 1000, [int]::MaxValue)
    $exited = $false
    try { $exited = $proc.WaitForExit($waitMs) } catch { $exited = $false }
    if (-not $exited) {
        $timeoutMessage = "Elevated capture did not signal completion within $ElevatedTimeoutSeconds s; not blocking further. See $log for progress."
        Write-CaptureResult -CaptureCases @() -ManifestPath $null -CaptureRunId $RunId `
            -OutputFormat $Format -QuietOutput ([bool]$Quiet) -Status timeout `
            -LogPath $log -Message $timeoutMessage
        exit 0
    }
    # ExitCode is only defined once the child has exited, and reading it on a higher-integrity
    # (elevated) process can throw Access Denied - treat either as 'not observed', non-fatal.
    $childExit = 0
    try { if ($proc.HasExited) { $childExit = $proc.ExitCode } } catch { $childExit = 0 }
    if ($childExit -ne 0) { Write-Error "Elevated capture failed (exit $childExit). See $log." -ErrorAction Continue ; exit $childExit }
    $manifestPath = Join-Path $runDirectory 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        Write-Error "Elevated capture did not produce $manifestPath. See $log for details." -ErrorAction Continue
        exit 1
    }
    $childManifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Write-CaptureResult @($childManifest.cases) $manifestPath $RunId $Format ([bool]$Quiet)
    exit 0
}

$projectLockName = [regex]::Replace(
    [System.IO.Path]::GetFileNameWithoutExtension($projFile.Name),
    '[^A-Za-z0-9._-]',
    '_')
if ([string]::IsNullOrEmpty($projectLockName)) { $projectLockName = 'project' }
$tfmLockName = [regex]::Replace($Tfm, '[^A-Za-z0-9._-]', '_')
if ([string]::IsNullOrEmpty($tfmLockName)) { $tfmLockName = 'default' }
$lockName = "$projectLockName-$tfmLockName"
$lockDirectory = Join-Path $projFile.DirectoryName 'obj/filtrace-capture-locks'
New-Item -ItemType Directory -Force -Path $lockDirectory | Out-Null
$lockPath = Join-Path $lockDirectory "$lockName.lock"
try {
    $captureLock = [System.IO.File]::Open(
        $lockPath,
        [System.IO.FileMode]::OpenOrCreate,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
}
catch [System.IO.IOException] {
    Write-Error "A capture is already active for project '$($projFile.FullName)' and TFM '$Tfm'. Wait for it to finish before starting another." -ErrorAction Continue
    exit 1
}

try {
    $runsDirectory = Split-Path -Parent $runDirectory
    New-Item -ItemType Directory -Force -Path $runsDirectory | Out-Null
    if (Test-Path -LiteralPath $runDirectory) {
        Write-Error "Capture run ID '$RunId' already exists at '$runDirectory'. Choose a new RunId; existing run artifacts are never reused." -ErrorAction Continue
        exit 1
    }

    $runClaimPath = Join-Path $runsDirectory "$RunId.claim"
    try {
        $runClaim = [System.IO.File]::Open(
            $runClaimPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        $runClaim.Dispose()
    }
    catch [System.IO.IOException] {
        Write-Error "Capture run ID '$RunId' is already reserved. Choose a new RunId; existing run artifacts are never reused." -ErrorAction Continue
        exit 1
    }

    # A pre-existing directory from an older helper may not have a claim file. Recheck
    # after the atomic claim to close the check/create race before writing any output.
    if (Test-Path -LiteralPath $runDirectory) {
        Write-Error "Capture run ID '$RunId' already exists at '$runDirectory'. Choose a new RunId; existing run artifacts are never reused." -ErrorAction Continue
        exit 1
    }

    New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# Without BenchmarkDotNet.Diagnostics.Windows the `-p ETW` profiler silently resolves
# to UnresolvedDiagnoser and no .etl is written - fail fast with guidance.
if ($Profiler -eq 'ETW' -and -not (Select-String -Path $projFile.FullName -Pattern 'BenchmarkDotNet.Diagnostics.Windows' -Quiet)) {
    Write-Error "$($projFile.Name) does not reference BenchmarkDotNet.Diagnostics.Windows; -p ETW will no-op. Add the package first." -ErrorAction Continue
    exit 1
}

# Preserve the BenchmarkDotNet build output for source-symbol resolution under both
# profilers. Both branches are multi-element arrays, so they stay arrays (a
# single-element if-expression would unwrap to a scalar under Set-StrictMode).
$profArg = @('-p', $Profiler, '--keepFiles')
$benchmarkArguments = @('run', '-c', 'Release', '-f', $Tfm, '--project', $projFile.FullName, '--', '--filter', $Filter) +
    $profArg + @('--artifacts', $artifacts)
$startedUtc = [DateTimeOffset]::UtcNow

if ($showProgress) {
    Write-Host "Capturing $Profiler trace: $Filter ($Tfm)..." -ForegroundColor Cyan
}
# Keep full BenchmarkDotNet output in the run log; stdout remains a compact filtrace
# handoff rather than a duplicate benchmark transcript.
& $DotnetPath @benchmarkArguments 2>&1 |
    Tee-Object -FilePath $log | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Error "Benchmark run failed (exit $LASTEXITCODE). See $log." -ErrorAction Continue ; exit $LASTEXITCODE }

$captureCases = @(Get-CaptureCases $artifacts $Profiler)
if ($captureCases.Count -eq 0) {
    Write-Error "No capture files found in $artifacts. Did the capture run?" -ErrorAction Continue
    exit 1
}
Set-BenchmarkIdentities $captureCases $log $FiltracePath $filtraceAvailable
if ($hasOperationCount) {
    foreach ($captureCase in $captureCases) {
        $captureCase.operationCount = $OperationCount
        $captureCase.operationUnit = $OperationUnit
    }
}

# The project build output carries matching PDBs for source lines; --keepFiles also
# preserves BenchmarkDotNet's generated build output for manual follow-up.
$symbols = Join-Path (Split-Path -Parent $projFile.FullName) "bin/Release/$Tfm"
$symbolCandidates = @(Get-SymbolCandidates $log $symbols)
foreach ($captureCase in $captureCases) {
    $captureCase.symbolCandidates = $symbolCandidates
    if ($captureCase.trace -and $filtraceAvailable) {
        $captureCase.symbolsDirectory = Find-ExactSymbolDirectory $captureCase.trace $symbolCandidates $FiltracePath
    }
}
$methodFilter = $Filter.Trim('*')
if ([string]::IsNullOrWhiteSpace($methodFilter)) { $methodFilter = 'BenchmarkMethod' }

foreach ($captureCase in $captureCases) {
    $captureStatuses = Get-DefaultCaptureStatuses $Profiler ([bool]$captureCase.trace)
    if ($captureCase.trace) {
        Write-CaptureMetadata $captureCase.trace $captureStatuses
    }

    $analysisPath = if ($captureCase.trace) { $captureCase.trace } else { $captureCase.speedscope }
    $traceInfo = if ($filtraceAvailable) {
        Get-TraceInfoResult $analysisPath $captureCase.symbolsDirectory $FiltracePath
    }
    else {
        $null
    }
    $traceInfoFailed = $filtraceAvailable -and -not (Test-HasAnalysisInfo $traceInfo)
    $captureCase.analyses = ConvertTo-AnalysisMap $traceInfo $captureStatuses (-not $filtraceAvailable)
    $captureCase.commands = Get-CaseCommands $captureCase $Profiler $Process $methodFilter $Top
    $captureCase.warnings = @(
        if ($traceInfoFailed) {
            'filtrace info could not verify analysis availability; no commands emitted'
        }
        Get-CaseWarnings $captureCase
    )
}

$manifestPath = Join-Path $runDirectory 'manifest.json'
$manifest = [ordered]@{
    schemaVersion = 1
    runId = $RunId
    startedUtc = $startedUtc.ToString('O')
    completedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    command = [ordered]@{
        executable = $DotnetPath
        arguments = $benchmarkArguments
    }
    project = $projFile.FullName
    tfm = $Tfm
    filter = $Filter
    profiler = $Profiler
    process = $Process
    source = Get-SourceIdentity $projFile.DirectoryName
    runtimes = @(Get-RuntimeSummaries $log)
    paths = [ordered]@{
        runDirectory = $runDirectory
        artifactsDirectory = $artifacts
        log = $log
    }
    cases = $captureCases
}
Write-RunManifest $manifestPath $manifest

if (-not $ElevatedChild) {
    Write-CaptureResult $captureCases $manifestPath $RunId $Format ([bool]$Quiet)
}
}
finally {
    $captureLock.Dispose()
}
