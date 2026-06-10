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
///  The curated read-only MCP tool surface over the TraceQ analysis core: load a
///  trace and query its quality signals, folded self/inclusive rankings, immediate
///  callers, line-level attribution, two-trace diffs, and the garbage-collection
///  report across speedscope, EventPipe (<c>.nettrace</c>), and ETW (<c>.etl</c>)
///  inputs.
/// </summary>
/// <remarks>
///  <para>
///   Each tool returns the same JSON the CLI's <c>--format json</c> emits: an
///   <see cref="AnalysisResult{T}"/> envelope serialized by <see cref="OutputJson"/>,
///   so a client gets one shape - schema version, warnings, hints, typed result -
///   across every tool. The single injected <see cref="TraceStore"/> caches parsed
///   traces, so repeated queries against the same path reuse one parse.
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
    /// <returns>The trace summary envelope, as compact JSON.</returns>
    [McpServerTool(Name = "trace_info", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Load a trace (.speedscope.json, .nettrace, or .etl) and return its format, total weight (CPU "
        + "milliseconds, bytes allocated, or event counts depending on the trace), sample count, "
        + "symbol-resolution rate, per-thread sample counts, and quality warnings. Call this first: a "
        + "resolution rate below 0.8 means symbols are missing and the rankings should not be trusted.")]
    public static string Info(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' "
            + "embedded portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "")
    {
        TraceInfo info = Load(store, path, NullIfEmpty(symbols)).Info;
        TraceInfoView view = new(
            info.Path,
            info.Format.ToString(),
            info.TotalWeight,
            info.SampleCount,
            info.SymbolResolutionRate,
            info.Threads);
        return OutputJson.Serialize(new AnalysisResult<TraceInfoView>(view, info.Warnings));
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
    /// <returns>The ranking envelope, as compact JSON.</returns>
    [McpServerTool(Name = "trace_rank", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Rank the hottest frames over a chosen provider metric. measure=self credits the executing leaf "
        + "(JIT-helper leaves folded into the real method that incurred them); measure=inclusive credits a "
        + "frame and everything it calls. One tool spans every family - metric=cpu (sampled milliseconds, any "
        + "format), threadtime (wall-clock per thread, .etl only), alloc (bytes allocated, .nettrace only), or "
        + "exceptions (throw count, .nettrace only). Scope to a subtree with root; for a BenchmarkDotNet "
        + "capture set root to the measured workload to exclude harness warmup.")]
    public static string Rank(
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
        string symbols = "")
    {
        TraceMetric resolved = ResolveMetric(metric);
        bool inclusive = ResolveMeasure(measure);
        RequirePositiveTop(top);
        IReadOnlyList<string> foldPatterns = ResolveFold(fold);

        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols), resolved);
        TraceInfo info = trace.Info;
        RankingResult ranking = inclusive
            ? trace.Aggregator.InclusiveTime(root, foldPatterns, top)
            : trace.Aggregator.SelfTime(root, foldPatterns, top);

        return OutputJson.Serialize(
            new AnalysisResult<RankingResult>(ranking, info.Warnings, SteeringHints.ForRanking(ranking)));
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
    /// <returns>The caller-breakdown envelope, as compact JSON.</returns>
    [McpServerTool(Name = "trace_callers", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Immediate callers of the frame matching 'frame', with the CPU time each contributes. Use it to learn "
        + "what a JIT-helper or shared-utility frame (e.g. BulkMoveWithWriteBarrier) is really attributable to, "
        + "or to walk up a hot stack one level at a time. Scope to a subtree with root.")]
    public static string Callers(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description("Substring identifying the focus frame whose callers to report.")] string frame,
        [Description("Optional substring of a frame name to scope the analysis to its subtree.")] string root = "",
        [Description("Maximum number of caller rows to return.")] int top = 25,
        [Description(
            "Optional build-output directory whose assemblies' embedded portable PDBs are extracted so "
            + "managed frames resolve to source lines.")]
        string symbols = "")
    {
        RequirePositiveTop(top);
        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols));
        TraceInfo info = trace.Info;
        CallersResult callers = trace.Aggregator.CallersOf(frame, root, top);

        return OutputJson.Serialize(
            new AnalysisResult<CallersResult>(callers, info.Warnings, SteeringHints.ForCallers(callers)));
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
    /// <returns>The line-level self-time envelope, as compact JSON.</returns>
    [McpServerTool(Name = "trace_lines", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Line-level self time: each leaf sample (JIT-helper leaves folded into their caller) attributed to the "
        + "source file:line that was executing, scoped to methods whose name contains 'method'. Requires a "
        + ".nettrace or .etl trace whose modules have portable PDBs; pass 'symbols' pointing at the build-output "
        + "directory. Speedscope inputs carry no line data and return an empty ranking. A '<no source>' row is "
        + "time in matching methods whose PDB was not found.")]
    public static string Lines(
        TraceStore store,
        [Description("Path to a .nettrace or .etl trace file (speedscope carries no line data).")] string path,
        [Description("Optional substring of a method name to scope the ranking; omit for every method.")] string method = "",
        [Description("Maximum number of rows to return.")] int top = 25,
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' embedded "
            + "portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "")
    {
        RequirePositiveTop(top);
        IReadOnlyList<string> foldPatterns = ResolveFold(fold);
        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols));
        TraceInfo info = trace.Info;
        LineRankingResult lines = trace.Aggregator.HotLines(method, foldPatterns, top);

        return OutputJson.Serialize(new AnalysisResult<LineRankingResult>(lines, info.Warnings));
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
    /// <returns>The heat-map envelope, as compact JSON.</returns>
    [McpServerTool(Name = "trace_heatmap", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Per-line self-time heat map for one source file: each leaf sample (JIT-helper leaves folded into their "
        + "caller) attributed to the line that was executing, ordered by line number to overlay onto the source. "
        + "Matching is by file name only. Requires a .nettrace or .etl trace whose modules have portable PDBs; "
        + "pass 'symbols' pointing at the build-output directory. Speedscope inputs return an empty map.")]
    public static string Heatmap(
        TraceStore store,
        [Description("Path to a .nettrace or .etl trace file (speedscope carries no line data).")] string path,
        [Description("Path or bare name of the source file to map, e.g. ExtGlob.cs.")] string file,
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' embedded "
            + "portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "")
    {
        IReadOnlyList<string> foldPatterns = ResolveFold(fold);
        LoadedTrace trace = Load(store, path, NullIfEmpty(symbols));
        TraceInfo info = trace.Info;

        // The trace records the build-time file name, not its full path, so match on the file name.
        string fileName = Path.GetFileName(file);
        SourceHeatmapResult heatmap = trace.Aggregator.SourceHeatmap(fileName, foldPatterns);

        return OutputJson.Serialize(new AnalysisResult<SourceHeatmapResult>(heatmap, info.Warnings));
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
    /// <returns>The diff envelope, as compact JSON.</returns>
    [McpServerTool(Name = "trace_diff", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Compare two CPU traces and report the per-frame change (regressions and improvements), largest absolute "
        + "change first. Both traces are ranked fully (no row cap) before diffing, so a frame hot on only one side "
        + "is not misreported as a total regression. measure=self credits the executing leaf (JIT-helper leaves "
        + "folded into the real method); measure=inclusive credits a frame and everything it calls. Use it to find "
        + "what got slower or faster between two runs - for example a capture from before and after a change.")]
    public static string Diff(
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

        return OutputJson.Serialize(
            new AnalysisResult<RankingDiffResult>(diff, DiffWarnings(before.Info, after.Info), SteeringHints.ForDiff(diff)));
    }

    /// <summary>
    ///  Returns the garbage-collection report for a <c>.nettrace</c> EventPipe trace:
    ///  the aggregate pause and heap summary plus the hottest per-collection records.
    /// </summary>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="top">Maximum per-collection records to return, ranked by pause time.</param>
    /// <returns>The GC-report envelope, as compact JSON.</returns>
    [McpServerTool(Name = "trace_gc", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description(
        "Garbage-collection report for a .nettrace EventPipe trace: aggregate counts (gen 0/1/2), total, max, and "
        + "mean pause time, peak heap size, total promoted bytes, and the per-collection records capped to the "
        + "'top' hottest pauses. Use it to judge GC pressure - frequent gen-2 collections or long pauses point at "
        + "allocation problems. Requires a .nettrace trace; .etl and speedscope inputs are rejected.")]
    public static string Gc(
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
        return OutputJson.Serialize(new AnalysisResult<GcStatsResult>(report, warnings));
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
        TraceMetric metric = TraceMetric.Cpu)
    {
        try
        {
            return store.Get(path, symbols, metric);
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
        // Format guardrail (an extension test, no I/O): the GC provider parses the
        // EventPipe format, so reject an .etl or speedscope export cleanly here rather
        // than failing deep inside the EventPipe parser with an opaque message.
        if (!path.EndsWith(".nettrace", StringComparison.OrdinalIgnoreCase))
        {
            throw new McpException(
                $"The GC report requires a .nettrace EventPipe trace; '{path}' is not a .nettrace file.");
        }

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
}
