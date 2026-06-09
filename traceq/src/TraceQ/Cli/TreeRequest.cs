// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a call-tree run: which trace to load, how to scope and
///  fold it, how far and how finely to expand the tree, and how to render it.
/// </summary>
/// <param name="Path">The trace file path.</param>
/// <param name="Root">Substring scoping the tree to a subtree, or empty for the whole trace.</param>
/// <param name="Fold">The leaf-frame fold patterns; folded frames are skipped.</param>
/// <param name="MaxDepth">The maximum number of frame levels below the root to expand.</param>
/// <param name="MinPercent">The minimum share of the scoped total, in percent, a node must have to appear.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Format">The render format.</param>
/// <param name="Strict">Whether to trip the strict symbol-resolution exit gate.</param>
internal sealed record TreeRequest(
    string Path,
    string Root,
    IReadOnlyList<string> Fold,
    int MaxDepth,
    double MinPercent,
    string? Symbols,
    OutputFormat Format,
    bool Strict)
{
    /// <summary>
    ///  The default maximum number of frame levels expanded below the root.
    /// </summary>
    public const int DefaultMaxDepth = 10;

    /// <summary>
    ///  The default minimum share of the scoped total, in percent, for a node to
    ///  appear. A small nonzero default keeps the tree to the meaningful hot paths
    ///  and within an agent's token budget; pass <c>0</c> to show every frame.
    /// </summary>
    public const double DefaultMinPercent = 1.0;
}
