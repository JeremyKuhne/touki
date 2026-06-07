// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics.CodeAnalysis;

namespace TraceQ.Output;

/// <summary>
///  The output token budget: estimates how many tokens a serialized result will
///  cost an agent and decides when it exceeds the ceiling, so a verb can narrow
///  the query rather than flood the context window.
/// </summary>
/// <remarks>
///  <para>
///   Token counts are model-specific, so the estimate uses the widely-cited
///   heuristic of roughly four characters per token - deliberately rough but
///   stable and dependency-free. It is a guardrail, not exact accounting: the
///   ceiling sits well below a typical context window, so the approximation's
///   error is harmless.
///  </para>
///  <para>
///   This type only measures and warns. Actually truncating an over-budget
///   result - dropping rows or tightening the scope - is a per-verb concern that
///   lands with the CLI head; <see cref="TryGetBudgetWarning"/> is the building
///   block it consumes, mirroring how <c>SymbolGate</c> exposes its predicate.
///  </para>
/// </remarks>
public static class OutputBudget
{
    /// <summary>
    ///  The default ceiling, in estimated tokens, above which a result is considered
    ///  too large for an agent's context budget.
    /// </summary>
    public const int DefaultCeilingTokens = 25_000;

    /// <summary>
    ///  Estimates the token cost of <paramref name="text"/> using the rough
    ///  four-characters-per-token heuristic, rounded up.
    /// </summary>
    /// <param name="text">The serialized output to estimate.</param>
    /// <returns>The estimated token count.</returns>
    public static int EstimateTokens(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Round up: ceil(length / 4).
        return (text.Length + 3) / 4;
    }

    /// <summary>
    ///  Determines whether <paramref name="text"/> exceeds the token ceiling.
    /// </summary>
    /// <param name="text">The serialized output to measure.</param>
    /// <param name="ceilingTokens">The token ceiling.</param>
    /// <returns><see langword="true"/> when the estimate exceeds the ceiling.</returns>
    public static bool IsOverBudget(string text, int ceilingTokens = DefaultCeilingTokens) =>
        EstimateTokens(text) > ceilingTokens;

    /// <summary>
    ///  Produces a budget warning, with remediation, when <paramref name="text"/>
    ///  exceeds the token ceiling.
    /// </summary>
    /// <param name="text">The serialized output to measure.</param>
    /// <param name="ceilingTokens">The token ceiling.</param>
    /// <param name="warning">The warning text when over budget, otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when a warning was produced.</returns>
    public static bool TryGetBudgetWarning(string text, int ceilingTokens, [NotNullWhen(true)] out string? warning)
    {
        int tokens = EstimateTokens(text);
        if (tokens <= ceilingTokens)
        {
            warning = null;
            return false;
        }

        warning =
            $"Output is about {tokens} tokens, over the {ceilingTokens}-token budget. "
            + "Narrow the query (a smaller --top, a --root scope, or a tighter filter) to reduce it.";
        return true;
    }
}
