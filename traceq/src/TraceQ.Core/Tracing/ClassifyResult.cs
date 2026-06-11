// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  One runtime work category in a classification: its name, the self-time weight
///  attributed to it, and that weight's share of the scoped total.
/// </summary>
/// <param name="Category">
///  The category name (see <see cref="FrameCategories"/>): zeroing, copying,
///  write-barrier, gc, jit, or other.
/// </param>
/// <param name="Weight">The summed self-time weight, in the metric's unit (milliseconds for CPU).</param>
/// <param name="PercentOfScope">The category's share of the scoped total, in percent.</param>
public sealed record CategoryRow(
    string Category,
    double Weight,
    double PercentOfScope);

/// <summary>
///  A CPU profile summarized by runtime work category, answering "where did the time
///  go - zeroing memory? copying strings? in the GC?" - the complement to a per-method
///  ranking.
/// </summary>
/// <remarks>
///  <para>
///   Each sample's self-time leaf is bucketed by <see cref="FrameCategories.Classify"/>
///   and the categories are ranked by weight. The classification only distinguishes the
///   runtime work once native symbols are resolved; without them the native leaves are
///   the unresolved <c>?</c> frame and fall in <see cref="FrameCategories.Other"/>, so
///   this view pairs naturally with the <c>--native-symbols</c> option. Folding is
///   marker-only (the synthetic <c>CPU_TIME</c> markers), so the JIT-helper thunks the
///   categories classify are not folded away.
///  </para>
/// </remarks>
/// <param name="ScopeWeight">The scoped total, in the metric's unit (the percent denominator).</param>
/// <param name="RootFrame">The root frame the classification was scoped to, or empty for the whole trace.</param>
/// <param name="Categories">The categories, highest weight first.</param>
public sealed record ClassifyResult(
    double ScopeWeight,
    string RootFrame,
    IReadOnlyList<CategoryRow> Categories);
