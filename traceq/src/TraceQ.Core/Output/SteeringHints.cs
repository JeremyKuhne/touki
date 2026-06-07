// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Output;

/// <summary>
///  The steering-hint taxonomy: the canonical next-step nudges a verb attaches to
///  its <see cref="AnalysisResult{T}"/> so an agent mid-investigation is pointed
///  at the smallest useful follow-up rather than left to guess.
/// </summary>
/// <remarks>
///  <para>
///   The output contract reserves a hints channel; this is what fills it for the
///   ranking-family verbs. Each helper turns a verb's result into the one drill
///   that most naturally continues the investigation: a ranking points at the
///   hottest frame's callers, a callers report points further up the stack, and a
///   diff points at the frame that changed most. The nudges name the engine verb
///   and the frame to pass it, matching the hint pinned by the output-contract
///   golden.
///  </para>
///  <para>
///   The hints are advisory text, not commands; the CLI and MCP heads render them
///   verbatim. When a result is empty the nudge steers toward widening the scope
///   instead of drilling, because there is nothing to drill into.
///  </para>
/// </remarks>
internal static class SteeringHints
{
    /// <summary>
    ///  The root pseudo-frame, whose presence as the dominant caller means the
    ///  focus frame is a top-level entry point.
    /// </summary>
    private const string RootFrame = "<root>";

    /// <summary>
    ///  The nudge emitted when a verb's scope contains no frames to drill into.
    /// </summary>
    private const string EmptyScope = "no frames in scope; widen the filter or check symbol resolution";

    /// <summary>
    ///  The next-step hints for a self-time or inclusive-time ranking: drill into
    ///  the hottest frame's callers.
    /// </summary>
    /// <param name="ranking">The ranking the hints steer from.</param>
    /// <returns>The steering hints, never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ranking"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> ForRanking(RankingResult ranking)
    {
        ArgumentNullException.ThrowIfNull(ranking);

        if (ranking.Rows.Count == 0)
        {
            return [EmptyScope];
        }

        return [$"drill into the hot frame with: callers {ranking.Rows[0].Frame}"];
    }

    /// <summary>
    ///  The next-step hints for a callers report: continue up the stack toward the
    ///  dominant caller, or note that the focus frame is a top-level entry point.
    /// </summary>
    /// <param name="callers">The callers report the hints steer from.</param>
    /// <returns>The steering hints, never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callers"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> ForCallers(CallersResult callers)
    {
        ArgumentNullException.ThrowIfNull(callers);

        if (callers.Callers.Count == 0)
        {
            return [EmptyScope];
        }

        string top = callers.Callers[0].Caller;
        if (string.Equals(top, RootFrame, StringComparison.Ordinal))
        {
            return ["the focus frame is called directly from the root; it is a top-level entry point"];
        }

        return [$"continue up the stack with: callers {top}"];
    }

    /// <summary>
    ///  The next-step hints for a ranking diff: drill into the frame whose weight
    ///  changed most between the two runs.
    /// </summary>
    /// <param name="diff">The ranking diff the hints steer from.</param>
    /// <returns>The steering hints, never <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="diff"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> ForDiff(RankingDiffResult diff)
    {
        ArgumentNullException.ThrowIfNull(diff);

        if (diff.Rows.Count == 0)
        {
            return ["the two rankings match in scope; no frames changed"];
        }

        string top = diff.Rows[0].Frame;
        return [$"the largest change is {top}; drill into it with: callers {top}"];
    }
}
