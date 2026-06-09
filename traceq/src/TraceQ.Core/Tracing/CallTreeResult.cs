// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  A node in a call tree: a frame, the weight of the subtree rooted at it, that
///  subtree's share of the scoped total, and the frames it called.
/// </summary>
/// <remarks>
///  <para>
///   The tree is path-based: a method that appears at two different stack
///   positions is two nodes, and a recursive call shows as a node nested under
///   itself, so a node's <see cref="Children"/> are exactly the frames called at
///   that point on the stack. <see cref="Weight"/> is inclusive - it sums every
///   sample that passed through this node - so a node's weight is at least the sum
///   of its children's.
///  </para>
/// </remarks>
/// <param name="Frame">The shortened frame name, or <c>&lt;root&gt;</c> for the synthetic root.</param>
/// <param name="Weight">The subtree's inclusive weight, in the metric's unit (milliseconds for CPU, bytes for allocations).</param>
/// <param name="PercentOfScope">The subtree's share of the scoped total, in percent.</param>
/// <param name="Children">The called frames, highest weight first.</param>
public sealed record TreeNode(
    string Frame,
    double Weight,
    double PercentOfScope,
    IReadOnlyList<TreeNode> Children);

/// <summary>
///  A top-down call tree over a scoped trace: each node's children are the frames
///  it called, weighted by the metric spent in them, so an agent can follow the
///  hot path from the root down to the work that dominates it.
/// </summary>
/// <remarks>
///  <para>
///   The tree is rooted at a synthetic <c>&lt;root&gt;</c> node whose weight is the
///   scoped total; its children are the outermost frames of the scoped samples.
///   Folded frames (JIT helpers, the synthetic CPU marker) are skipped so the tree
///   shows only real methods, and the tree is bounded by a maximum depth and a
///   minimum per-node share so it stays readable and within an agent's token budget.
///  </para>
/// </remarks>
/// <param name="ScopeWeight">The scoped total, in the metric's unit (the percent denominator).</param>
/// <param name="RootFrame">The root frame the tree was scoped to, or empty for the whole trace.</param>
/// <param name="Root">The synthetic root node whose children are the scoped top-level frames.</param>
public sealed record CallTreeResult(
    double ScopeWeight,
    string RootFrame,
    TreeNode Root);
