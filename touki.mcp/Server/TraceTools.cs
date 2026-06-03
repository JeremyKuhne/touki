// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using Touki.Mcp.Tracing;

namespace Touki.Mcp.Server;

/// <summary>
///  The MCP tool surface: load a CPU trace and query folded self-time,
///  inclusive-time and caller rankings without a GUI, across speedscope,
///  EventPipe (<c>.nettrace</c>) and ETW (<c>.etl</c>) inputs.
/// </summary>
[McpServerToolType]
public static class TraceTools
{
    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    ///  Loads a trace and returns its format, duration, sample count, symbol
    ///  resolution rate, per-thread sample counts and quality warnings.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Absolute or relative path to a .speedscope.json, .nettrace or .etl file.</param>
    /// <returns>A JSON summary of the loaded trace.</returns>
    [McpServerTool(Name = "load_trace")]
    [Description(
        "Load a CPU trace (.speedscope.json, .nettrace, or .etl) and return its format, total duration, "
        + "sample count, symbol resolution rate, per-thread sample counts, and quality warnings. "
        + "Call this first; a resolution rate below 0.8 means symbols are missing and rankings are unreliable.")]
    public static string LoadTrace(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' embedded "
            + "portable PDBs are extracted to resolve managed frames to source lines for the hot_lines tool.")]
        string symbols = "")
    {
        TraceInfo info = store.Get(path, NullIfEmpty(symbols)).Info;
        return Serialize(new
        {
            path = info.Path,
            format = info.Format,
            durationMs = info.DurationMs,
            sampleCount = info.SampleCount,
            symbolResolutionRate = info.SymbolResolutionRate,
            threads = info.Threads.Take(20).Select(static t => new { thread = t.Thread, sampleCount = t.SampleCount }),
            warnings = info.Warnings
        });
    }

    /// <summary>
    ///  Returns the folded self-time ranking, crediting JIT-helper and synthetic
    ///  marker samples to the real method that incurred them.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="rootFrame">Optional substring scoping the ranking to a subtree.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="top">Maximum rows to return.</param>
    /// <returns>A JSON self-time ranking.</returns>
    [McpServerTool(Name = "hotspots_self")]
    [Description(
        "Top self-time methods, with JIT-helper thunks (write barriers, memmove, GC-poll) and the synthetic "
        + "CPU_TIME marker folded into the real method that incurred them. Use 'rootFrame' to scope to the measured "
        + "workload. WARNING: BenchmarkDotNet wraps the workload in an 'Activity Benchmark(...benchmarkName=Foo...)' "
        + "frame whose name contains the method name, so do not use the benchmark method name as rootFrame; scope to a "
        + "frame inside the workload (e.g. an enumerator MoveNext).")]
    public static string HotspotsSelf(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description("Optional substring of a frame name to scope the ranking to its subtree.")] string rootFrame = "",
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description("Maximum number of rows to return.")] int top = 25)
    {
        FoldingAggregator aggregator = store.Get(path).Aggregator;
        return Serialize(aggregator.SelfTime(rootFrame, fold ?? FrameNames.DefaultFoldPatterns, top));
    }

    /// <summary>
    ///  Returns the inclusive-time ranking, skipping folded frames.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="rootFrame">Optional substring scoping the ranking to a subtree.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="top">Maximum rows to return.</param>
    /// <returns>A JSON inclusive-time ranking.</returns>
    [McpServerTool(Name = "hotspots_inclusive")]
    [Description(
        "Top inclusive-time methods (time spent in a method and everything it calls), with folded frames skipped. "
        + "Use 'rootFrame' to scope to the measured workload; see the hotspots_self warning about the BenchmarkDotNet "
        + "wrapper frame.")]
    public static string HotspotsInclusive(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description("Optional substring of a frame name to scope the ranking to its subtree.")] string rootFrame = "",
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description("Maximum number of rows to return.")] int top = 25)
    {
        FoldingAggregator aggregator = store.Get(path).Aggregator;
        return Serialize(aggregator.InclusiveTime(rootFrame, fold ?? FrameNames.DefaultFoldPatterns, top));
    }

    /// <summary>
    ///  Reports the immediate callers of a focus frame, with the time each
    ///  contributes - used to confirm what a JIT-helper artifact is really
    ///  attributable to.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="frame">Substring identifying the focus frame.</param>
    /// <param name="rootFrame">Optional substring scoping the analysis to a subtree.</param>
    /// <param name="top">Maximum caller rows to return.</param>
    /// <returns>A JSON caller breakdown.</returns>
    [McpServerTool(Name = "callers_of")]
    [Description(
        "Immediate callers of the frame matching 'frame', with the time each caller contributes. Use it to confirm "
        + "what a JIT-helper artifact (e.g. BulkMoveWithWriteBarrier) is really attributable to.")]
    public static string CallersOf(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path,
        [Description("Substring identifying the focus frame whose callers to report.")] string frame,
        [Description("Optional substring of a frame name to scope the analysis to its subtree.")] string rootFrame = "",
        [Description("Maximum number of caller rows to return.")] int top = 25)
    {
        FoldingAggregator aggregator = store.Get(path).Aggregator;
        return Serialize(aggregator.CallersOf(frame, rootFrame, top));
    }

    /// <summary>
    ///  Returns the line-level self-time ranking, attributing leaf samples to the
    ///  source line that was executing, scoped to the matching methods.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="method">Optional substring scoping to matching methods.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="top">Maximum rows to return.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <returns>A JSON line-level self-time ranking.</returns>
    [McpServerTool(Name = "hot_lines")]
    [Description(
        "Line-level self-time: each leaf sample (after folding JIT-helper leaves into their caller) attributed to the "
        + "source 'file:line' that was executing. Use 'method' to scope to one method (e.g. 'RunEngine') to see which "
        + "lines of its hot loop dominate. Requires a .nettrace or .etl trace whose modules have portable PDBs; pass "
        + "'symbols' pointing at the build-output directory (e.g. artifacts/.../touki.perf/net10.0) for assemblies that "
        + "ship embedded PDBs. Speedscope inputs carry no line data and yield an empty ranking. A '<no source>' row is "
        + "time in matching methods whose PDB was not found.")]
    public static string HotLines(
        TraceStore store,
        [Description("Path to a .nettrace or .etl trace file (speedscope carries no line data).")] string path,
        [Description("Optional substring of a method name to scope the ranking; omit for every method.")] string method = "",
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description("Maximum number of rows to return.")] int top = 25,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' embedded "
            + "portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "")
    {
        FoldingAggregator aggregator = store.Get(path, NullIfEmpty(symbols)).Aggregator;
        return Serialize(aggregator.HotLines(method, fold ?? FrameNames.DefaultFoldPatterns, top));
    }

    /// <summary>
    ///  Builds a per-line self-time heat map for one source file and, when the
    ///  file is found on disk, a ready-to-render annotated source view.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <param name="file">Path or file name of the source file to map.</param>
    /// <param name="fold">Optional fold patterns; defaults to the built-in JIT-helper list.</param>
    /// <param name="symbols">Optional build-output directory supplying embedded PDBs for line resolution.</param>
    /// <returns>A JSON heat map plus, when resolvable, an annotated source listing.</returns>
    [McpServerTool(Name = "source_heatmap")]
    [Description(
        "Per-line self-time heat map for a single source file: every leaf sample (after folding JIT-helper leaves "
        + "into their caller) attributed to the line that was executing, ordered by line number so you can overlay "
        + "hotness onto the source. Pass 'file' as the full on-disk path (e.g. n:/repos/touki/touki/Io/ExtGlob.cs) to "
        + "also get a ready-to-render annotated listing with a per-line ms/percent/heat gutter; a bare file name still "
        + "returns the JSON line data. Requires a .nettrace or .etl trace whose modules have portable PDBs; pass "
        + "'symbols' pointing at the build-output directory (e.g. artifacts/.../touki.perf/net10.0). Speedscope inputs "
        + "carry no line data and yield an empty map.")]
    public static string SourceHeatmap(
        TraceStore store,
        [Description("Path to a .nettrace or .etl trace file (speedscope carries no line data).")] string path,
        [Description("Full on-disk path (preferred) or bare name of the source file to map, e.g. ExtGlob.cs.")] string file,
        [Description("Optional regex fold patterns; omit to use the built-in JIT-helper defaults.")] string[]? fold = null,
        [Description(
            "Optional build-output directory (e.g. artifacts/.../touki.perf/net10.0) whose assemblies' embedded "
            + "portable PDBs are extracted so managed frames resolve to source lines.")]
        string symbols = "")
    {
        FoldingAggregator aggregator = store.Get(path, NullIfEmpty(symbols)).Aggregator;
        string fileName = Path.GetFileName(file);
        SourceHeatmapResult result = aggregator.SourceHeatmap(fileName, fold ?? FrameNames.DefaultFoldPatterns);

        string? annotatedSource = null;
        bool sourceFound = false;
        string? note = null;

        if (result.Lines.Count == 0)
        {
            note =
                $"No samples were attributed to '{fileName}'. Confirm the trace is a .nettrace or .etl read with "
                + "PDBs (pass 'symbols'), and that the file name is correct - hot_lines lists the files that resolved.";
        }
        else if (SourceAnnotator.TryReadSourceLines(file, out string[] sourceLines))
        {
            sourceFound = true;
            annotatedSource = SourceAnnotator.Render(sourceLines, result.Lines, result.FileMilliseconds);
        }
        else
        {
            note =
                "Pass 'file' as the full on-disk path to also get an annotated source listing; "
                + "the file was not found on disk, so only line data is returned.";
        }

        return Serialize(new
        {
            scopeMilliseconds = result.ScopeMilliseconds,
            file = result.File,
            fileMilliseconds = result.FileMilliseconds,
            sourceFound,
            sourcePath = sourceFound ? Path.GetFullPath(file) : null,
            note,
            lines = result.Lines.Select(line => new
            {
                line = line.Line,
                method = line.Method,
                milliseconds = line.Milliseconds,
                percentOfTrace = line.PercentOfScope,
                percentOfFile = result.FileMilliseconds > 0 ? 100.0 * line.Milliseconds / result.FileMilliseconds : 0.0,
                sampleCount = line.SampleCount
            }),
            annotatedSource
        });
    }

    /// <summary>
    ///  Lists the per-thread sample counts for a trace, to help pick a root frame
    ///  or spot idle thread-pool noise.
    /// </summary>
    /// <param name="store">The trace cache (injected).</param>
    /// <param name="path">Path to the trace file.</param>
    /// <returns>A JSON list of per-thread sample counts.</returns>
    [McpServerTool(Name = "list_threads")]
    [Description("Per-thread sample counts for a trace, highest first - useful for picking a rootFrame or spotting idle thread-pool noise.")]
    public static string ListThreads(
        TraceStore store,
        [Description("Path to a .speedscope.json, .nettrace, or .etl trace file.")] string path)
    {
        TraceInfo info = store.Get(path).Info;
        return Serialize(new
        {
            path = info.Path,
            threads = info.Threads.Select(static t => new { thread = t.Thread, sampleCount = t.SampleCount })
        });
    }

    private static string Serialize(object value) => JsonSerializer.Serialize(value, s_json);

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
