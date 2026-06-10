// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics.CodeAnalysis;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Builds a <see cref="RankRequest"/> from the raw verb parameters and holds the
///  ranking verbs' shared constants and the provider-selector policy.
/// </summary>
/// <remarks>
///  <para>
///   <see cref="TryResolveMetric"/> maps the <c>--metric</c> selector string to the
///   <see cref="TraceMetric"/> the loader builds; the <c>rank</c> verb consumes it to
///   reject an unknown selector. The family shortcut verbs (<c>cpu</c>, <c>alloc</c>)
///   bypass the string and pass their <see cref="TraceMetric"/> directly.
///  </para>
/// </remarks>
internal static class RankRequestFactory
{
    /// <summary>
    ///  The default <c>--metric</c> selector: the CPU provider.
    /// </summary>
    public const string CpuMetric = "cpu";

    /// <summary>
    ///  The default maximum number of ranked rows.
    /// </summary>
    public const int DefaultTop = 25;

    /// <summary>
    ///  Resolves a <c>--metric</c> selector string to the provider view it names.
    /// </summary>
    /// <param name="metric">The requested provider metric (case-insensitive).</param>
    /// <param name="resolved">The resolved provider view when recognized.</param>
    /// <returns>
    ///  <see langword="true"/> when <paramref name="metric"/> names a wired provider;
    ///  otherwise <see langword="false"/>, and the caller should report a usage error.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   Delegates to <see cref="TraceMetricSelector.TryResolve"/> so the CLI and the
    ///   MCP <c>trace_rank</c> tool share one selector vocabulary.
    ///  </para>
    /// </remarks>
    public static bool TryResolveMetric(string metric, out TraceMetric resolved) =>
        TraceMetricSelector.TryResolve(metric, out resolved);

    /// <summary>
    ///  Builds a ranking request from the verb parameters, applying the built-in fold
    ///  defaults when none are supplied.
    /// </summary>
    /// <param name="trace">The trace file path.</param>
    /// <param name="metric">The provider view to rank.</param>
    /// <param name="measure">Which measure to report.</param>
    /// <param name="root">Substring scoping the ranking, or empty for the whole trace.</param>
    /// <param name="top">Maximum number of ranked rows.</param>
    /// <param name="fold">Extra fold patterns, or <see langword="null"/> for the built-in defaults.</param>
    /// <param name="symbols">Optional build-output directory for symbol resolution.</param>
    /// <param name="format">The render format.</param>
    /// <param name="strict">Whether to trip the strict symbol-resolution exit gate.</param>
    /// <param name="scope">The process scope, or <see langword="null"/> for the automatic default.</param>
    /// <returns>The assembled request.</returns>
    public static RankRequest Create(
        string trace,
        TraceMetric metric,
        Measure measure,
        string root,
        int top,
        IReadOnlyList<string>? fold,
        string? symbols,
        OutputFormat format,
        bool strict,
        ScopeRequest? scope = null)
    {
        IReadOnlyList<string> foldPatterns = fold is { Count: > 0 } ? fold : FrameNames.DefaultFoldPatterns;
        return new RankRequest(trace, metric, root, top, foldPatterns, measure, format, symbols, strict, scope);
    }

    /// <summary>
    ///  Builds the process-scope request from the two mutually exclusive verb options:
    ///  an explicit <c>--process</c> name and the <c>--all-processes</c> opt-out.
    /// </summary>
    /// <param name="process">The <c>--process</c> name substring, or empty when not given.</param>
    /// <param name="allProcesses">Whether <c>--all-processes</c> was set.</param>
    /// <param name="scope">
    ///  The resolved scope on success: an explicit process scope, the all-processes
    ///  opt-out, or the automatic default when neither option was given.
    /// </param>
    /// <param name="errorMessage">The usage error when both options were given.</param>
    /// <returns>
    ///  <see langword="true"/> when the options are valid; otherwise <see langword="false"/>,
    ///  and the caller should report <paramref name="errorMessage"/> as a usage error.
    /// </returns>
    public static bool TryResolveScope(
        string process,
        bool allProcesses,
        out ScopeRequest scope,
        [NotNullWhen(false)] out string? errorMessage)
    {
        bool hasProcess = !string.IsNullOrEmpty(process);
        if (hasProcess && allProcesses)
        {
            scope = ScopeRequest.Auto;
            errorMessage = "Specify only one of --process and --all-processes.";
            return false;
        }

        scope = hasProcess
            ? ScopeRequest.ForProcess(process)
            : allProcesses
                ? ScopeRequest.AllProcesses
                : ScopeRequest.Auto;
        errorMessage = null;
        return true;
    }

    /// <summary>
    ///  Resolves the root-frame scope from the explicit <c>--root</c> option and the
    ///  <c>--benchmark</c> preset, which scopes a BenchmarkDotNet capture to the
    ///  measured workload subtree.
    /// </summary>
    /// <param name="root">The explicit <c>--root</c> substring, or empty when not given.</param>
    /// <param name="benchmark">Whether <c>--benchmark</c> was set.</param>
    /// <param name="resolvedRoot">
    ///  The root scope to use: the BenchmarkDotNet workload frame when
    ///  <paramref name="benchmark"/> is set, otherwise <paramref name="root"/>.
    /// </param>
    /// <param name="errorMessage">The usage error when both options were given.</param>
    /// <returns>
    ///  <see langword="true"/> when the options are valid; otherwise <see langword="false"/>,
    ///  and the caller should report <paramref name="errorMessage"/> as a usage error.
    /// </returns>
    public static bool TryResolveRoot(
        string root,
        bool benchmark,
        out string resolvedRoot,
        [NotNullWhen(false)] out string? errorMessage)
    {
        if (benchmark && !string.IsNullOrEmpty(root))
        {
            // --benchmark is itself a root preset, so a second explicit root is ambiguous.
            // The return is false, so the out value is "don't care"; set it to empty to
            // make that explicit rather than propagate the ambiguous root.
            resolvedRoot = string.Empty;
            errorMessage = "Specify only one of --root and --benchmark.";
            return false;
        }

        resolvedRoot = benchmark ? FrameNames.BenchmarkWorkloadFrame : root;
        errorMessage = null;
        return true;
    }
}
