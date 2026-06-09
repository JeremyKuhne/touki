// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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
    public static bool TryResolveMetric(string metric, out TraceMetric resolved)
    {
        switch (metric.ToLowerInvariant())
        {
            case CpuMetric:
                resolved = TraceMetric.Cpu;
                return true;
            case "alloc":
            case "allocations":
                resolved = TraceMetric.Allocations;
                return true;
            case "exceptions":
                resolved = TraceMetric.Exceptions;
                return true;
            case "threadtime":
                resolved = TraceMetric.ThreadTime;
                return true;
            default:
                resolved = TraceMetric.Cpu;
                return false;
        }
    }

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
        bool strict)
    {
        IReadOnlyList<string> foldPatterns = fold is { Count: > 0 } ? fold : FrameNames.DefaultFoldPatterns;
        return new RankRequest(trace, metric, root, top, foldPatterns, measure, format, symbols, strict);
    }
}
