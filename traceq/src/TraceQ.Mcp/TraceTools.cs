// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using TraceQ.Output;
using TraceQ.Server;
using TraceQ.Tracing;
using TraceQ.Tracing.Providers;

namespace TraceQ.Mcp;

/// <summary>
///  The curated MCP tool surface over the TraceQ analysis core: load a trace and
///  query its quality signals, folded self/inclusive rankings, immediate callers,
///  line-level attribution, two-trace diffs, the garbage-collection report, and a
///  raw event query across speedscope, EventPipe (<c>.nettrace</c>), and ETW
///  (<c>.etl</c>) inputs, plus export a flame graph to a file. Every tool but
///  <c>trace_export</c> is read-only; <c>trace_export</c> writes a file.
/// </summary>
/// <remarks>
///  <para>
///   Each tool returns a typed <see cref="AnalysisResult{T}"/> envelope - the same
///   shape the CLI emits - rather than a pre-serialized string, so the server can
///   advertise an <c>outputSchema</c> per tool and return both structured content
///   (the parsed object) and a text mirror. Every tool shares one shape: a schema
///   version, warnings, hints, and the typed result. The host registers the tools
///   with <see cref="OutputJson.SerializerOptions"/>, so that envelope is serialized
///   with the same deterministic naming, encoding, and double-rounding the CLI uses.
///   The single injected <see cref="TraceStore"/> caches parsed traces, so repeated
///   queries against the same path reuse one parse.
///  </para>
/// </remarks>
[McpServerToolType]
public sealed class TraceTools
{
    /// <summary>
    ///  Loads a trace and returns its format, total weight, sample count, symbol
    ///  resolution rate, per-thread sample counts, and quality warnings.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <param name="process">Optional process-name substring scoping a multi-process .etl capture to one process tree.</param>
    /// <returns>The trace summary envelope.</returns>
    [McpServerTool(Name = "trace_info", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Load a trace (.speedscope.json, .nettrace, or .etl) and return its format, total weight (CPU "
        + "milliseconds, bytes allocated, or event counts depending on the trace), sample count, "
        + "symbol-resolution rate, per-thread sample counts, and quality warnings. Call this first: a "
        + "resolution rate below 0.8 means symbols are missing and the rankings should not be trusted.")]
    public static AnalysisResult<TraceInfoView> Info(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' "
            + "embedded portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "",
        [Description(
            "Optional process-name substring scoping a multi-process .etl capture to one process tree; omit "
            + "to auto-scope to the busiest. Ignored for single-process .nettrace/speedscope traces.")]
        string process = "")
    {
        TraceInfo info = Load(store, path, NullIfEmpty(symbols), scope: ResolveScope(process)).Info;
        TraceInfoView view = new(
            info.Path,
            info.Format.ToString(),
            info.TotalWeight,
            info.SampleCount,
            info.SymbolResolutionRate,
            info.Threads);
        return new AnalysisResult<TraceInfoView>(view, info.Warnings);
    }

    /// <summary>
    ///  Ranks the hottest frames over a chosen provider metric by self or inclusive
    ///  time, folding JIT-helper sampling artifacts back into the real methods.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="metric">The provider view to rank.</param>
    /// <param name="measure">Whether to report self time or inclusive time.</param>
    /// <param name="root">Optional substring scoping the ranking to a subtree.</param>
    /// <param name="top">Maximum rows to return.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <param name="process">Optional process-name substring scoping a multi-process .etl capture to one process tree.</param>
    /// <returns>The ranking envelope.</returns>
    [McpServerTool(Name = "trace_rank", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Rank the hottest frames over a chosen provider metric. measure=self credits the executing leaf "
        + "(JIT-helper leaves folded into the real method that incurred them); measure=inclusive credits a "
        + "frame and everything it calls. One tool spans every family - metric=cpu (sampled milliseconds, any "
        + "format), threadtime (wall-clock per thread, .etl only), alloc (bytes allocated, .nettrace only), or "
        + "exceptions (throw count, .nettrace only). Scope to a subtree with root; for a BenchmarkDotNet "
        + "capture set root to the measured workload to exclude harness warmup.")]
    public static AnalysisResult<RankingResult> Rank(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description("Provider view to rank: cpu, threadtime, alloc, or exceptions.")] string metric = "cpu",
        [Description("Which measure to report: self or inclusive.")] string measure = "self",
        [Description("Optional substring of a frame name to scope the ranking to its subtree.")] string root = "",
        [Description("Maximum number of ranked rows to return.")] int top = 25,
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description(
            "Optional build-output directory whose assemblies' embedded portable PDBs are extracted so "
            + "managed frames resolve to source lines (cpu metric only).")]
        string symbols = "",
        [Description(
            "Optional process-name substring scoping a multi-process .etl capture to one process tree; omit "
            + "to auto-scope to the busiest. Ignored for single-process .nettrace/speedscope traces.")]
        string process = "")
    {
        TraceMetric resolved = ResolveMetric(metric);
        bool inclusive = ResolveMeasure(measure);
        RequirePositiveTop(top);
        IReadOnlyList<string> foldPatterns = ResolveFold(fold);

        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols), resolved, ResolveScope(process));
        TraceInfo info = trace.Info;
        RankingResult ranking = inclusive
            ? trace.Aggregator.InclusiveTime(root, foldPatterns, top)
            : trace.Aggregator.SelfTime(root, foldPatterns, top);

        return new AnalysisResult<RankingResult>(ranking, info.Warnings, SteeringHints.ForRanking(ranking));
    }

    /// <summary>
    ///  Reports the immediate callers of the frame matching <paramref name="frame"/>,
    ///  with the CPU time each contributes.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="frame">Substring identifying the focus frame whose callers to report.</param>
    /// <param name="root">Optional substring scoping the analysis to a subtree.</param>
    /// <param name="top">Maximum caller rows to return.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <param name="process">Optional process-name substring scoping a multi-process .etl capture to one process tree.</param>
    /// <returns>The caller-breakdown envelope.</returns>
    [McpServerTool(Name = "trace_callers", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Immediate callers of the frame matching 'frame', with the CPU time each contributes. Use it to learn "
        + "what a JIT-helper or shared-utility frame (e.g. BulkMoveWithWriteBarrier) is really attributable to, "
        + "or to walk up a hot stack one level at a time. Scope to a subtree with root.")]
    public static AnalysisResult<CallersResult> Callers(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description("Substring identifying the focus frame whose callers to report.")] string frame,
        [Description("Optional substring of a frame name to scope the analysis to its subtree.")] string root = "",
        [Description("Maximum number of caller rows to return.")] int top = 25,
        [Description(
            "Optional build-output directory whose assemblies' embedded portable PDBs are extracted so "
            + "managed frames resolve to source lines.")]
        string symbols = "",
        [Description(
            "Optional process-name substring scoping a multi-process .etl capture to one process tree; omit "
            + "to auto-scope to the busiest. Ignored for single-process .nettrace/speedscope traces.")]
        string process = "")
    {
        RequirePositiveTop(top);
        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols), scope: ResolveScope(process));
        TraceInfo info = trace.Info;
        CallersResult callers = trace.Aggregator.CallersOf(frame, root, top);

        return new AnalysisResult<CallersResult>(callers, info.Warnings, SteeringHints.ForCallers(callers));
    }

    /// <summary>
    ///  Returns the line-level self-time ranking, attributing leaf samples to the
    ///  source line that was executing, scoped to the matching methods.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="method">Optional substring scoping to matching methods.</param>
    /// <param name="top">Maximum rows to return.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <param name="process">Optional process-name substring scoping a multi-process .etl capture to one process tree.</param>
    /// <returns>The line-level self-time envelope.</returns>
    [McpServerTool(Name = "trace_lines", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Line-level self time: each leaf sample (JIT-helper leaves folded into their caller) attributed to the "
        + "source file:line that was executing, scoped to methods whose name contains 'method'. Requires a "
        + ".nettrace or .etl trace whose modules have portable PDBs; pass 'symbols' pointing at the build-output "
        + "directory. Speedscope inputs carry no line data and return an empty ranking. A '<no source>' row is "
        + "time in matching methods whose PDB was not found.")]
    public static AnalysisResult<LineRankingResult> Lines(
        TraceStore store,
        [Description("Path to a .nettrace or .etl trace file (speedscope carries no line data).")] string path,
        [Description("Optional substring of a method name to scope the ranking; omit for every method.")] string method = "",
        [Description("Maximum number of rows to return.")] int top = 25,
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' embedded "
            + "portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "",
        [Description(
            "Optional process-name substring scoping a multi-process .etl capture to one process tree; omit "
            + "to auto-scope to the busiest. Ignored for single-process .nettrace/speedscope traces.")]
        string process = "")
    {
        RequirePositiveTop(top);
        IReadOnlyList<string> foldPatterns = ResolveFold(fold);
        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols), scope: ResolveScope(process));
        TraceInfo info = trace.Info;
        LineRankingResult lines = trace.Aggregator.HotLines(method, foldPatterns, top);

        return new AnalysisResult<LineRankingResult>(lines, info.Warnings);
    }

    /// <summary>
    ///  Builds a per-line self-time heat map for one source file, ordered by line
    ///  number for overlaying onto the source.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="file">Path or file name of the source file to map.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <param name="process">Optional process-name substring scoping a multi-process .etl capture to one process tree.</param>
    /// <returns>The heat-map envelope.</returns>
    [McpServerTool(Name = "trace_heatmap", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Per-line self-time heat map for one source file: each leaf sample (JIT-helper leaves folded into their "
        + "caller) attributed to the line that was executing, ordered by line number to overlay onto the source. "
        + "Matching is by file name only. Requires a .nettrace or .etl trace whose modules have portable PDBs; "
        + "pass 'symbols' pointing at the build-output directory. Speedscope inputs return an empty map.")]
    public static AnalysisResult<SourceHeatmapResult> Heatmap(
        TraceStore store,
        [Description("Path to a .nettrace or .etl trace file (speedscope carries no line data).")] string path,
        [Description("Path or bare name of the source file to map, e.g. ExtGlob.cs.")] string file,
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' embedded "
            + "portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "",
        [Description(
            "Optional process-name substring scoping a multi-process .etl capture to one process tree; omit "
            + "to auto-scope to the busiest. Ignored for single-process .nettrace/speedscope traces.")]
        string process = "")
    {
        IReadOnlyList<string> foldPatterns = ResolveFold(fold);
        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols), scope: ResolveScope(process));
        TraceInfo info = trace.Info;

        // The trace records the build-time file name, not its full path, so match on the file name.
        string fileName = Path.GetFileName(file);
        SourceHeatmapResult heatmap = trace.Aggregator.SourceHeatmap(fileName, foldPatterns);

        return new AnalysisResult<SourceHeatmapResult>(heatmap, info.Warnings);
    }

    /// <summary>
    ///  Compares two CPU traces and reports the per-frame change, largest absolute
    ///  change first, so an agent can see what got slower or faster between runs.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="beforePath">The baseline trace file path.</param>
    /// <param name="afterPath">The current trace file path.</param>
    /// <param name="measure">Whether to compare self time or inclusive time.</param>
    /// <param name="root">Optional substring scoping both rankings to a subtree.</param>
    /// <param name="top">Maximum changed rows to return.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <returns>The diff envelope.</returns>
    [McpServerTool(Name = "trace_diff", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Compare two CPU traces and report the per-frame change (regressions and improvements), largest absolute "
        + "change first. Both traces are ranked fully (no row cap) before diffing, so a frame hot on only one side "
        + "is not misreported as a total regression. measure=self credits the executing leaf (JIT-helper leaves "
        + "folded into the real method); measure=inclusive credits a frame and everything it calls. Use it to find "
        + "what got slower or faster between two runs - for example a capture from before and after a change.")]
    public static AnalysisResult<RankingDiffResult> Diff(
        TraceStore store,
        [Description("Path to the baseline (before) .speedscope.json, .nettrace, or .etl trace file.")] string beforePath,
        [Description("Path to the current (after) .speedscope.json, .nettrace, or .etl trace file.")] string afterPath,
        [Description("Which measure to compare: self or inclusive.")] string measure = "self",
        [Description("Optional substring of a frame name to scope both rankings to its subtree.")] string root = "",
        [Description("Maximum number of changed rows to return.")] int top = 25,
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description(
            "Optional build-output directory whose assemblies' embedded portable PDBs are extracted so "
            + "managed frames resolve to source lines.")]
        string symbols = "")
    {
        bool inclusive = ResolveMeasure(measure);
        RequirePositiveTop(top);
        IReadOnlyList<string> foldPatterns = ResolveFold(fold);
        string? resolvedSymbols = NullIfEmpty(symbols);

        LoadedTrace before = Load(store, beforePath, resolvedSymbols);
        LoadedTrace after = Load(store, afterPath, resolvedSymbols);

        // Rank every frame (no row cap) so the diff is not skewed by per-side truncation;
        // RankingDiff applies the requested top to the changed rows instead.
        RankingResult beforeRanking = inclusive
            ? before.Aggregator.InclusiveTime(root, foldPatterns, int.MaxValue)
            : before.Aggregator.SelfTime(root, foldPatterns, int.MaxValue);
        RankingResult afterRanking = inclusive
            ? after.Aggregator.InclusiveTime(root, foldPatterns, int.MaxValue)
            : after.Aggregator.SelfTime(root, foldPatterns, int.MaxValue);

        RankingDiffResult diff = RankingDiff.Diff(beforeRanking, afterRanking, top);

        return new AnalysisResult<RankingDiffResult>(diff, DiffWarnings(before.Info, after.Info), SteeringHints.ForDiff(diff));
    }

    /// <summary>
    ///  Returns the garbage-collection report for a <c>.nettrace</c> EventPipe trace:
    ///  the aggregate pause and heap summary plus the hottest per-collection records.
    /// </summary>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="top">Maximum per-collection records to return, ranked by pause time.</param>
    /// <returns>The GC-report envelope.</returns>
    [McpServerTool(Name = "trace_gc", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Garbage-collection report for a .nettrace EventPipe trace: aggregate counts (gen 0/1/2), total, max, and "
        + "mean pause time, peak heap size, total promoted bytes, and the per-collection records capped to the "
        + "'top' hottest pauses. Use it to judge GC pressure - frequent gen-2 collections or long pauses point at "
        + "allocation problems. Requires a .nettrace trace; .etl and speedscope inputs are rejected.")]
    public static AnalysisResult<GcStatsResult> Gc(
        [Description("Path to a .nettrace EventPipe trace file.")] string path,
        [Description("Maximum number of per-collection records to return, ranked by pause time.")] int top = 25)
    {
        RequirePositiveTop(top);
        GcStatsResult full = ReadGcStats(path);

        // Keep the full aggregate summary, but cap the per-collection detail to the
        // hottest pauses so a long trace cannot blow the output budget.
        List<string> warnings = [];
        IReadOnlyList<GcRecord> shown = full.Gcs;
        if (shown.Count > top)
        {
            shown = [.. shown.OrderByDescending(static g => g.PauseMs).Take(top)];
            warnings.Add($"Showing the top {top} of {full.GcCount} collections by pause time.");
        }

        GcStatsResult report = full with { Gcs = shown };
        return new AnalysisResult<GcStatsResult>(report, warnings);
    }

    /// <summary>
    ///  Queries the raw events of a <c>.nettrace</c> EventPipe trace by name, paged and
    ///  with each event's payload truncated, so an agent can inspect arbitrary events.
    /// </summary>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="name">Substring matched against <c>Provider/EventName</c>; empty matches every event.</param>
    /// <param name="skip">The number of matches to skip, for paging.</param>
    /// <param name="take">The maximum number of matches to return on this page.</param>
    /// <param name="maxPayload">The per-event payload character cap.</param>
    /// <returns>The events-page envelope.</returns>
    [McpServerTool(Name = "trace_query_events", ReadOnly = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Query the raw events of a .nettrace EventPipe trace by name - the escape hatch for inspecting arbitrary "
        + "events the structured reports do not cover. 'name' is a case-insensitive substring matched against "
        + "Provider/EventName (empty matches every event). Results are paged with 'skip'/'take' and each event's "
        + "payload is truncated to 'maxPayload' characters, so a query matching hundreds of thousands of events "
        + "stays within budget; when more matches remain, a hint gives the next page's skip. Requires a .nettrace "
        + "trace; .etl and speedscope inputs are rejected.")]
    public static AnalysisResult<EventQueryResult> QueryEvents(
        [Description("Path to a .nettrace EventPipe trace file.")] string path,
        [Description("Substring matched against Provider/EventName; omit to match every event.")] string name = "",
        [Description("The number of matches to skip, for paging.")] int skip = 0,
        [Description("The maximum number of matches to return on this page.")] int take = 100,
        [Description("The per-event payload character cap.")] int maxPayload = 200)
    {
        if (skip < 0)
        {
            throw new McpException("skip must be 0 or greater.");
        }

        if (take < 0)
        {
            throw new McpException("take must be 0 or greater.");
        }

        if (maxPayload < 0)
        {
            throw new McpException("maxPayload must be 0 or greater.");
        }

        EventQueryResult result = ReadEvents(path, name, skip, take, maxPayload);

        // When matches remain beyond this page, steer toward the next one rather than
        // leaving the agent to work out the skip arithmetic.
        List<string> hints = [];
        int shownThrough = result.Skipped + result.Events.Count;
        if (shownThrough < result.TotalMatched)
        {
            hints.Add($"{result.TotalMatched - shownThrough} more match; page with skip {shownThrough}.");
        }

        return new AnalysisResult<EventQueryResult>(result, hints: hints);
    }

    /// <summary>
    ///  Exports a trace's CPU sample source to a speedscope or Chrome-trace flame-graph
    ///  file an agent can hand a human to open in a viewer.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="output">The file path to write the flame graph to.</param>
    /// <param name="format">The flame-graph format: <c>speedscope</c> or <c>chromium</c>.</param>
    /// <param name="name">The profile name embedded in the flame graph, shown in the viewer.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <returns>The export-confirmation envelope.</returns>
    [McpServerTool(Name = "trace_export", ReadOnly = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description(
        "Export a trace's CPU samples to a flame-graph file for a human to open in a viewer - this is how you hand "
        + "off a visual. format=speedscope (the default) opens at speedscope.app; format=chromium writes the Chrome "
        + "Trace Event Format for chrome://tracing or the Perfetto UI. 'output' is the file path to write (required; "
        + "unlike the query tools this writes a file rather than returning the data, and overwrites an existing file "
        + "at that path). The whole sample source is exported - no folding, scoping, or ranking. The response "
        + "confirms the path, format, and byte count.")]
    public static AnalysisResult<ExportResult> Export(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description("The file path to write the flame graph to (it is overwritten if it exists).")] string output,
        [Description("The flame-graph format: speedscope or chromium.")] string format = "speedscope",
        [Description("The profile name embedded in the flame graph, shown in the viewer.")] string name = "traceq",
        [Description(
            "Optional build-output directory whose assemblies' embedded portable PDBs are extracted so "
            + "managed frames resolve to source lines.")]
        string symbols = "")
    {
        bool chromium = ResolveExportFormat(format);

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new McpException("output is required: the file path to write the flame graph to.");
        }

        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols));
        TraceInfo info = trace.Info;

        string exported = chromium
            ? ChromiumExporter.Export(trace.Source, name)
            : SpeedscopeExporter.Export(trace.Source, name);

        string outputPath = WriteExport(output, exported);
        long byteCount = new FileInfo(outputPath).Length;

        ExportResult result = new(chromium ? "chromium" : "speedscope", outputPath, byteCount, name);
        string hint = chromium
            ? $"open {outputPath} in chrome://tracing or the Perfetto UI (https://ui.perfetto.dev)"
            : $"open {outputPath} at https://speedscope.app";

        return new AnalysisResult<ExportResult>(result, info.Warnings, [hint]);
    }

    /// <summary>
    ///  Loads the <paramref name="metric"/> view of the trace, mapping the loader's
    ///  failure modes to a clean <see cref="McpException"/> rather than letting an
    ///  opaque exception propagate to the client.
    /// </summary>
    private static LoadedTrace Load(
        TraceStore store,
        string path,
        string? symbols,
        TraceMetric metric = TraceMetric.Cpu,
        ScopeRequest? scope = null)
    {
        try
        {
            return store.Get(path, symbols, metric, scope);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or JsonException
            or KeyNotFoundException
            or InvalidOperationException
            or FormatException
            or ArgumentException)
        {
            // Missing, unreadable, or malformed trace input - including a format that
            // does not carry the selected metric's data (NotSupportedException) -
            // surfaces as a clean tool error rather than an unhandled exception.
            throw new McpException(ex.Message);
        }
    }

    private static TraceMetric ResolveMetric(string metric) =>
        TraceMetricSelector.TryResolve(metric, out TraceMetric resolved)
            ? resolved
            : throw new McpException(
                $"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", TraceMetricSelector.Selectors)}.");

    private static bool ResolveMeasure(string measure)
    {
        if (string.Equals(measure, "self", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(measure, "inclusive", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new McpException($"Unknown measure '{measure}'. Valid measures: self, inclusive.");
    }

    /// <summary>
    ///  Resolves the export <c>format</c> selector to whether the Chrome-trace exporter
    ///  is used (otherwise speedscope).
    /// </summary>
    private static bool ResolveExportFormat(string format)
    {
        if (string.Equals(format, "speedscope", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(format, "chromium", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        throw new McpException($"Unknown format '{format}'. Valid formats: speedscope, chromium.");
    }

    /// <summary>
    ///  Writes the exported flame graph to <paramref name="output"/>, mapping a bad or
    ///  unwritable path to a clean <see cref="McpException"/>, and returns the absolute
    ///  path written.
    /// </summary>
    private static string WriteExport(string output, string content)
    {
        try
        {
            string fullPath = Path.GetFullPath(output);
            File.WriteAllText(fullPath, content);
            return fullPath;
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or System.Security.SecurityException
            or ArgumentException)
        {
            // A bad or unwritable output path (missing directory, permission denied,
            // invalid characters) surfaces as a clean tool error rather than an
            // unhandled exception.
            throw new McpException($"Could not write '{output}': {ex.Message}");
        }
    }

    private static void RequirePositiveTop(int top)
    {
        if (top < 1)
        {
            throw new McpException("top must be 1 or greater.");
        }
    }

    /// <summary>
    ///  Resolves the fold patterns to use - the caller's patterns or the built-in
    ///  JIT-helper defaults - and validates them up front so a malformed user-supplied
    ///  regex surfaces as a clean, actionable tool error rather than escaping the
    ///  aggregator as a framework-level exception.
    /// </summary>
    private static IReadOnlyList<string> ResolveFold(string[]? fold)
    {
        IReadOnlyList<string> patterns = fold is { Length: > 0 } ? fold : FrameNames.DefaultFoldPatterns;

        try
        {
            // Compile only to validate; the aggregator compiles its own copy when it runs.
            // CompileFoldPatterns also caps each match with a timeout, so a pathological
            // user pattern cannot hang the server.
            _ = FrameNames.CompileFoldPatterns(patterns);
        }
        catch (ArgumentException ex)
        {
            // A malformed fold regex is a usage error; surface the message (which names the
            // offending pattern) as a clean tool error instead of an internal failure.
            throw new McpException(ex.Message);
        }

        return patterns;
    }

    /// <summary>
    ///  Reads the GC report for a <c>.nettrace</c> trace, applying the format guardrail
    ///  and mapping the provider's failure modes to a clean <see cref="McpException"/>.
    /// </summary>
    private static GcStatsResult ReadGcStats(string path)
    {
        RequireNetTrace(path, "GC report");

        try
        {
            return new GcStatsProvider().Read(path);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException
            or FormatException
            or ArgumentException)
        {
            // A missing, unreadable, or malformed .nettrace surfaces as a clean tool
            // error rather than an unhandled exception.
            throw new McpException(ex.Message);
        }
    }

    /// <summary>
    ///  Reads an events page for a <c>.nettrace</c> trace, applying the format guardrail
    ///  and mapping the provider's failure modes to a clean <see cref="McpException"/>.
    /// </summary>
    private static EventQueryResult ReadEvents(string path, string name, int skip, int take, int maxPayload)
    {
        RequireNetTrace(path, "events query");

        try
        {
            return new EventQueryProvider().Query(path, name, skip, take, maxPayload);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidOperationException
            or FormatException
            or ArgumentException)
        {
            // A missing, unreadable, or malformed .nettrace surfaces as a clean tool
            // error rather than an unhandled exception.
            throw new McpException(ex.Message);
        }
    }

    /// <summary>
    ///  Rejects a non-<c>.nettrace</c> input up front for the EventPipe-only report
    ///  providers, with a clean message naming the report, rather than failing deep
    ///  inside the EventPipe parser.
    /// </summary>
    private static void RequireNetTrace(string path, string reportName)
    {
        // Format guardrail (an extension test, no I/O): the report providers parse the
        // EventPipe format, so reject an .etl or speedscope export cleanly here.
        if (!path.EndsWith(".nettrace", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(
                $"The {reportName} requires a .nettrace EventPipe trace; '{path}' is not a .nettrace file.");
        }
    }

    /// <summary>
    ///  Builds the diff warnings: the baseline and current traces' quality warnings,
    ///  each prefixed with which side it came from.
    /// </summary>
    private static IReadOnlyList<string> DiffWarnings(TraceInfo before, TraceInfo after)
    {
        List<string> warnings = [];
        foreach (string warning in before.Warnings)
        {
            warnings.Add($"baseline: {warning}");
        }

        foreach (string warning in after.Warnings)
        {
            warnings.Add($"current: {warning}");
        }

        return warnings;
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    // An empty process selector means "auto-scope to the busiest process tree" (the
    // Load default), a no-op on a single-process .nettrace/speedscope trace; a non-empty
    // value scopes a multi-process .etl capture to the named process tree.
    private static ScopeRequest? ResolveScope(string process) =>
        string.IsNullOrEmpty(process) ? null : ScopeRequest.ForProcess(process);
}
