// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.ComponentModel.DataAnnotations;
using ConsoleAppFramework;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  The command surface ConsoleAppFramework binds the verbs to: each public method
///  is a verb, its parameters are the options, and its XML doc comments supply the
///  generated help. The bodies validate the provider selector and delegate the work
///  to <see cref="RankingExecutor"/>.
/// </summary>
internal sealed class TraceCommands
{
    /// <summary>
    ///  Rank the hottest frames in a trace by self- or inclusive-time.
    /// </summary>
    /// <param name="trace">Path to a .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="metric">Provider metric to rank: cpu (default), alloc, exceptions, or threadtime.</param>
    /// <param name="measure">-m, Which measure to report: self (leaf time, helpers folded) or inclusive.</param>
    /// <param name="root">Substring scoping the ranking to the subtree under a frame.</param>
    /// <param name="top">-n, Maximum number of rows to return.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <param name="strict">Exit 3 when symbol resolution is below the trusted threshold.</param>
    /// <returns>A process exit code.</returns>
    [Command("rank")]
    public int Rank(
        [Argument] string trace,
        string metric = RankRequestFactory.CpuMetric,
        Measure measure = Measure.Self,
        string root = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string[]? fold = null,
        string? symbols = null,
        OutputFormat format = OutputFormat.Text,
        bool strict = false)
    {
        if (!RankRequestFactory.TryResolveMetric(metric, out TraceMetric resolved))
        {
            Console.Error.WriteLine(
                $"Unknown metric '{metric}'. Supported stack metrics: cpu, alloc, exceptions, threadtime.");
            return ExitCodes.UsageError;
        }

        RankRequest request = RankRequestFactory.Create(
            trace, resolved, measure, root, top, fold, symbols, format, strict);
        return RankingExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Rank CPU-time hotspots; the shortcut for 'rank --metric cpu'.
    /// </summary>
    /// <param name="trace">Path to a .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="measure">-m, Which measure to report: self (leaf time, helpers folded) or inclusive.</param>
    /// <param name="root">Substring scoping the ranking to the subtree under a frame.</param>
    /// <param name="top">-n, Maximum number of rows to return.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <param name="strict">Exit 3 when symbol resolution is below the trusted threshold.</param>
    /// <returns>A process exit code.</returns>
    [Command("cpu")]
    public int Cpu(
        [Argument] string trace,
        Measure measure = Measure.Self,
        string root = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string[]? fold = null,
        string? symbols = null,
        OutputFormat format = OutputFormat.Text,
        bool strict = false)
    {
        RankRequest request = RankRequestFactory.Create(
            trace, TraceMetric.Cpu, measure, root, top, fold, symbols, format, strict);
        return RankingExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Rank allocation hotspots by bytes; the shortcut for 'rank --metric alloc'.
    /// </summary>
    /// <param name="trace">Path to a .nettrace EventPipe file captured with allocation sampling.</param>
    /// <param name="measure">-m, Which measure to report: self (the allocating site) or inclusive (its subtree).</param>
    /// <param name="root">Substring scoping the ranking to the subtree under a frame.</param>
    /// <param name="top">-n, Maximum number of rows to return.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <returns>A process exit code.</returns>
    /// <remarks>
    ///  Allocation frames resolve from the trace's own CLR rundown, so this verb has
    ///  no <c>--symbols</c> or <c>--strict</c> option: those resolve and gate native
    ///  frames, which the allocation view does not depend on.
    /// </remarks>
    [Command("alloc")]
    public int Alloc(
        [Argument] string trace,
        Measure measure = Measure.Self,
        string root = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string[]? fold = null,
        OutputFormat format = OutputFormat.Text)
    {
        RankRequest request = RankRequestFactory.Create(
            trace, TraceMetric.Allocations, measure, root, top, fold, symbols: null, format, strict: false);
        return RankingExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Rank exception throw sites by count; the shortcut for 'rank --metric exceptions'.
    /// </summary>
    /// <param name="trace">Path to a .nettrace EventPipe file carrying exception-throw events.</param>
    /// <param name="measure">-m, Which measure to report: self (the throw site) or inclusive (its subtree).</param>
    /// <param name="root">Substring scoping the ranking to the subtree under a frame.</param>
    /// <param name="top">-n, Maximum number of rows to return.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <returns>A process exit code.</returns>
    /// <remarks>
    ///  Throw-site frames resolve from the trace's own CLR rundown, so this verb has
    ///  no <c>--symbols</c> or <c>--strict</c> option: those resolve and gate native
    ///  frames, which the exception view does not depend on.
    /// </remarks>
    [Command("exceptions")]
    public int Exceptions(
        [Argument] string trace,
        Measure measure = Measure.Self,
        string root = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string[]? fold = null,
        OutputFormat format = OutputFormat.Text)
    {
        RankRequest request = RankRequestFactory.Create(
            trace, TraceMetric.Exceptions, measure, root, top, fold, symbols: null, format, strict: false);
        return RankingExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Rank where wall-clock time went - running and blocked - by elapsed
    ///  milliseconds; the shortcut for 'rank --metric threadtime'.
    /// </summary>
    /// <param name="trace">Path to an .etl ETW capture taken with the context-switch keywords.</param>
    /// <param name="measure">-m, Which measure to report: self (the leaf state) or inclusive (its subtree).</param>
    /// <param name="root">Substring scoping the ranking to the subtree under a frame.</param>
    /// <param name="top">-n, Maximum number of rows to return.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <returns>A process exit code.</returns>
    /// <remarks>
    ///  Unlike CPU sampling, thread time accounts for off-CPU (blocked) intervals, so
    ///  a stack's weight is elapsed time rather than busy time. Reading an <c>.etl</c>
    ///  requires the ETW conversion, which is available on Windows only. Frames
    ///  resolve from the capture itself, so this verb has no <c>--symbols</c> or
    ///  <c>--strict</c> option.
    /// </remarks>
    [Command("threadtime")]
    public int ThreadTime(
        [Argument] string trace,
        Measure measure = Measure.Self,
        string root = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string[]? fold = null,
        OutputFormat format = OutputFormat.Text)
    {
        RankRequest request = RankRequestFactory.Create(
            trace, TraceMetric.ThreadTime, measure, root, top, fold, symbols: null, format, strict: false);
        return RankingExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Report the immediate callers of a frame; the drill-down after 'rank' finds a hot frame.
    /// </summary>
    /// <param name="trace">Path to a .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="frame">Substring of the focus frame whose callers are reported.</param>
    /// <param name="root">Substring scoping the analysis to the subtree under a frame.</param>
    /// <param name="top">-n, Maximum number of caller rows to return.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <param name="strict">Exit 3 when symbol resolution is below the trusted threshold.</param>
    /// <returns>A process exit code.</returns>
    [Command("callers")]
    public int Callers(
        [Argument] string trace,
        [Argument] string frame,
        string root = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string? symbols = null,
        OutputFormat format = OutputFormat.Text,
        bool strict = false)
    {
        CallersRequest request = new(trace, frame, root, top, symbols, format, strict);
        return CallersExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Rank the hottest source lines of the scoped methods.
    /// </summary>
    /// <param name="trace">Path to a .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="method">Substring scoping the ranking to matching methods; omit for every method.</param>
    /// <param name="top">-n, Maximum number of rows to return.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <param name="strict">Exit 3 when symbol resolution is below the trusted threshold.</param>
    /// <returns>A process exit code.</returns>
    [Command("lines")]
    public int Lines(
        [Argument] string trace,
        string method = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string[]? fold = null,
        string? symbols = null,
        OutputFormat format = OutputFormat.Text,
        bool strict = false)
    {
        IReadOnlyList<string> foldPatterns = fold is { Length: > 0 } ? fold : FrameNames.DefaultFoldPatterns;
        LinesRequest request = new(trace, method, foldPatterns, top, symbols, format, strict);
        return LinesExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Build a per-line heat map for a source file.
    /// </summary>
    /// <param name="trace">Path to a .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="file">Source file to map; a full on-disk path also overlays the heat onto the source.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <param name="strict">Exit 3 when symbol resolution is below the trusted threshold.</param>
    /// <returns>A process exit code.</returns>
    [Command("heatmap")]
    public int Heatmap(
        [Argument] string trace,
        [Argument] string file,
        string[]? fold = null,
        string? symbols = null,
        OutputFormat format = OutputFormat.Text,
        bool strict = false)
    {
        IReadOnlyList<string> foldPatterns = fold is { Length: > 0 } ? fold : FrameNames.DefaultFoldPatterns;
        HeatmapRequest request = new(trace, file, foldPatterns, symbols, format, strict);
        return HeatmapExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Compare two traces and report what got slower or faster between them.
    /// </summary>
    /// <param name="before">Path to the baseline .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="after">Path to the current .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="measure">-m, Which measure to compare: self (leaf time, helpers folded) or inclusive.</param>
    /// <param name="root">Substring scoping both rankings to the subtree under a frame.</param>
    /// <param name="top">-n, Maximum number of changed rows to return.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <param name="strict">Exit 3 when either trace's symbol resolution is below the trusted threshold.</param>
    /// <returns>A process exit code.</returns>
    [Command("diff")]
    public int Diff(
        [Argument] string before,
        [Argument] string after,
        Measure measure = Measure.Self,
        string root = "",
        [Range(1, int.MaxValue)] int top = RankRequestFactory.DefaultTop,
        string[]? fold = null,
        string? symbols = null,
        OutputFormat format = OutputFormat.Text,
        bool strict = false)
    {
        IReadOnlyList<string> foldPatterns = fold is { Length: > 0 } ? fold : FrameNames.DefaultFoldPatterns;
        DiffRequest request = new(before, after, root, top, foldPatterns, measure, format, symbols, strict);
        return DiffExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Show the top-down call tree, following the hot path from the root into its callees.
    /// </summary>
    /// <param name="trace">Path to a .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="root">Substring scoping the tree to the subtree under a frame.</param>
    /// <param name="maxDepth">-d, Maximum number of frame levels to expand below the root.</param>
    /// <param name="minPct">Minimum share of the scoped total (percent) a node must have to appear; 0 shows all.</param>
    /// <param name="fold">Extra leaf-frame fold regexes (comma-separated); omit to use the built-in defaults.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="format">Render format: text or json.</param>
    /// <param name="strict">Exit 3 when symbol resolution is below the trusted threshold.</param>
    /// <returns>A process exit code.</returns>
    [Command("tree")]
    public int Tree(
        [Argument] string trace,
        string root = "",
        [Range(0, FoldingAggregator.MaxTreeDepth)] int maxDepth = TreeRequest.DefaultMaxDepth,
        [Range(0.0, 100.0)] double minPct = TreeRequest.DefaultMinPercent,
        string[]? fold = null,
        string? symbols = null,
        OutputFormat format = OutputFormat.Text,
        bool strict = false)
    {
        IReadOnlyList<string> foldPatterns = fold is { Length: > 0 } ? fold : FrameNames.DefaultFoldPatterns;
        TreeRequest request = new(trace, root, foldPatterns, maxDepth, minPct, symbols, format, strict);
        return TreeExecutor.Run(request, Console.Out, Console.Error);
    }

    /// <summary>
    ///  Export a trace to a flame-graph file for speedscope or chrome://tracing.
    /// </summary>
    /// <param name="trace">Path to a .speedscope.json, .nettrace, or .etl file.</param>
    /// <param name="format">Flame-graph format: speedscope or chromium.</param>
    /// <param name="output">-o, Output file path; omit to write to standard output.</param>
    /// <param name="symbols">-s, Build-output directory whose embedded PDBs resolve managed frames.</param>
    /// <param name="name">Profile name shown in the viewer.</param>
    /// <returns>A process exit code.</returns>
    [Command("export")]
    public int Export(
        [Argument] string trace,
        ExportFormat format = ExportFormat.Speedscope,
        string? output = null,
        string? symbols = null,
        string name = "traceq")
    {
        ExportRequest request = new(trace, format, output, symbols, name);
        return ExportExecutor.Run(request, Console.Out, Console.Error);
    }
}
