// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Mcp.Tracing;

/// <summary>
///  A single ranked frame in a self-time or inclusive-time report.
/// </summary>
/// <param name="Frame">The shortened frame name.</param>
/// <param name="Milliseconds">Time attributed to the frame, in milliseconds.</param>
/// <param name="PercentOfScope">Share of the scoped total, in percent.</param>
internal sealed record RankRow(string Frame, double Milliseconds, double PercentOfScope);

/// <summary>
///  A self-time or inclusive-time ranking over a scoped trace.
/// </summary>
/// <param name="ScopeMilliseconds">Total scoped wall-clock time, in milliseconds.</param>
/// <param name="RootFrame">The root frame the ranking was scoped to, or empty for the whole trace.</param>
/// <param name="Rows">The ranked frames, highest first.</param>
internal sealed record RankingResult(double ScopeMilliseconds, string RootFrame, IReadOnlyList<RankRow> Rows);

/// <summary>
///  A single immediate caller of a focus frame.
/// </summary>
/// <param name="Caller">The shortened caller frame name, or <c>&lt;root&gt;</c>.</param>
/// <param name="Milliseconds">Time this caller contributes to the focus frame, in milliseconds.</param>
/// <param name="PercentOfTarget">Share of the focus frame's total, in percent.</param>
internal sealed record CallerRow(string Caller, double Milliseconds, double PercentOfTarget);

/// <summary>
///  The immediate callers of a focus frame, with the time each contributes.
/// </summary>
/// <param name="Focus">The substring the focus frame was matched on.</param>
/// <param name="TargetMilliseconds">Total inclusive time spent in the focus frame, in milliseconds.</param>
/// <param name="PercentOfScope">The focus frame's share of the scoped total, in percent.</param>
/// <param name="ScopeMilliseconds">Total scoped wall-clock time, in milliseconds.</param>
/// <param name="Callers">The immediate callers, highest first.</param>
internal sealed record CallersResult(
    string Focus,
    double TargetMilliseconds,
    double PercentOfScope,
    double ScopeMilliseconds,
    IReadOnlyList<CallerRow> Callers);

/// <summary>
///  A single source line in a line-level self-time report.
/// </summary>
/// <param name="Method">The shortened method the line belongs to.</param>
/// <param name="Location">The source location (<c>file:line</c>), or <c>&lt;no source&gt;</c> when unresolved.</param>
/// <param name="Milliseconds">Time attributed to the line, in milliseconds.</param>
/// <param name="PercentOfScope">Share of the scoped total, in percent.</param>
internal sealed record LineRow(string Method, string Location, double Milliseconds, double PercentOfScope);

/// <summary>
///  A line-level self-time ranking: leaf samples attributed to the source line
///  executing when each was taken, scoped to the methods matching a filter.
/// </summary>
/// <param name="ScopeMilliseconds">Total scoped wall-clock time, in milliseconds.</param>
/// <param name="MethodFilter">The substring the methods were matched on, or empty for every method.</param>
/// <param name="Rows">The ranked source lines, highest first.</param>
internal sealed record LineRankingResult(double ScopeMilliseconds, string MethodFilter, IReadOnlyList<LineRow> Rows);

/// <summary>
///  Self-time attributed to a single source line of a file in a heat map.
/// </summary>
/// <param name="Line">The 1-based source line number.</param>
/// <param name="Milliseconds">Self-time attributed to the line, in milliseconds.</param>
/// <param name="PercentOfScope">Share of the whole-trace self-time, in percent.</param>
/// <param name="SampleCount">Number of leaf samples attributed to the line.</param>
/// <param name="Method">The shortened method that dominates the line's self-time.</param>
internal sealed record HeatLine(int Line, double Milliseconds, double PercentOfScope, int SampleCount, string Method);

/// <summary>
///  A per-line self-time heat map for a single source file: each leaf sample
///  (after folding JIT-helper leaves into their caller) is bucketed by the source
///  line that was executing, ordered by line number for overlaying onto the source.
/// </summary>
/// <remarks>
///  <para>
///   Only samples carrying per-frame source locations contribute; speedscope
///   inputs have none, so a heat map is meaningful only for <c>.nettrace</c> and
///   <c>.etl</c> traces read with local PDBs present.
///  </para>
/// </remarks>
/// <param name="ScopeMilliseconds">Total whole-trace time, in milliseconds (the percent denominator).</param>
/// <param name="File">The source file name the lines belong to (no directory).</param>
/// <param name="FileMilliseconds">Self-time attributed to the file across all its lines, in milliseconds.</param>
/// <param name="Lines">The hot lines of the file, ordered by line number.</param>
internal sealed record SourceHeatmapResult(
    double ScopeMilliseconds,
    string File,
    double FileMilliseconds,
    IReadOnlyList<HeatLine> Lines);
