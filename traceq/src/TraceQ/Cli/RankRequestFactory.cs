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
///   Only the CPU metric is wired today. <see cref="IsCpuMetric"/> is the policy the
///   <c>rank</c> verb consumes to reject any other selector; the provider families
///   (thread-time, allocation, ...) become selectable here as they are wired into the
///   loader.
///  </para>
/// </remarks>
internal static class RankRequestFactory
{
    /// <summary>
    ///  The single metric the ranking verbs support today.
    /// </summary>
    public const string CpuMetric = "cpu";

    /// <summary>
    ///  The default maximum number of ranked rows.
    /// </summary>
    public const int DefaultTop = 25;

    /// <summary>
    ///  Determines whether <paramref name="metric"/> selects the CPU provider, the
    ///  only one available in this build.
    /// </summary>
    /// <param name="metric">The requested provider metric.</param>
    /// <returns><see langword="true"/> when the CPU provider is selected.</returns>
    public static bool IsCpuMetric(string metric) =>
        string.Equals(metric, CpuMetric, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///  Builds a ranking request from the verb parameters, applying the built-in fold
    ///  defaults when none are supplied.
    /// </summary>
    /// <param name="trace">The trace file path.</param>
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
        Measure measure,
        string root,
        int top,
        IReadOnlyList<string>? fold,
        string? symbols,
        OutputFormat format,
        bool strict)
    {
        IReadOnlyList<string> foldPatterns = fold is { Count: > 0 } ? fold : FrameNames.DefaultFoldPatterns;
        return new RankRequest(trace, root, top, foldPatterns, measure, format, symbols, strict);
    }
}
