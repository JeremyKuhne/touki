// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Stores either a leaf session index or the edge range and policy metadata for a composite matcher node.
/// </summary>
internal readonly struct CompositeMatcherNode
{
    /// <summary>
    ///  Initializes a leaf node for a matcher session.
    /// </summary>
    /// <param name="sessionIndex">The index of the matcher session.</param>
    public CompositeMatcherNode(int sessionIndex)
    {
        Kind = CompositeMatcherNodeKind.Leaf;
        SessionIndex = sessionIndex;
        FirstEdge = 0;
        ChildCount = 0;
        IncludeCount = 0;
        IncludeUnmatched = false;
    }

    /// <summary>
    ///  Initializes a composite node over a range of child edges.
    /// </summary>
    /// <param name="kind">The policy used to combine child results.</param>
    /// <param name="firstEdge">The index of the first child edge.</param>
    /// <param name="childCount">The number of child edges.</param>
    /// <param name="includeCount">The number of leading include edges for an exclusion-wins node.</param>
    /// <param name="includeUnmatched">Whether an ordered node includes unmatched paths.</param>
    public CompositeMatcherNode(
        CompositeMatcherNodeKind kind,
        int firstEdge,
        int childCount,
        int includeCount,
        bool includeUnmatched)
    {
        Kind = kind;
        SessionIndex = -1;
        FirstEdge = firstEdge;
        ChildCount = childCount;
        IncludeCount = includeCount;
        IncludeUnmatched = includeUnmatched;
    }

    /// <summary>
    ///  Gets the node kind.
    /// </summary>
    public CompositeMatcherNodeKind Kind { get; }

    /// <summary>
    ///  Gets the matcher session index for a leaf node.
    /// </summary>
    public int SessionIndex { get; }

    /// <summary>
    ///  Gets the index of the first child edge for a composite node.
    /// </summary>
    public int FirstEdge { get; }

    /// <summary>
    ///  Gets the number of child edges for a composite node.
    /// </summary>
    public int ChildCount { get; }

    /// <summary>
    ///  Gets the number of leading include edges for an exclusion-wins node.
    /// </summary>
    public int IncludeCount { get; }

    /// <summary>
    ///  Gets whether an ordered node includes paths unmatched by its child rules.
    /// </summary>
    public bool IncludeUnmatched { get; }
}
