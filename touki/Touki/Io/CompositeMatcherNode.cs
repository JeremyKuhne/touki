// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Stores either a leaf session index or the edge range and policy metadata for a composite matcher node.
/// </summary>
internal readonly struct CompositeMatcherNode
{
    public CompositeMatcherNode(int sessionIndex)
    {
        Kind = CompositeMatcherNodeKind.Leaf;
        SessionIndex = sessionIndex;
        FirstEdge = 0;
        ChildCount = 0;
        IncludeCount = 0;
        IncludeUnmatched = false;
    }

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

    public CompositeMatcherNodeKind Kind { get; }

    public int SessionIndex { get; }

    public int FirstEdge { get; }

    public int ChildCount { get; }

    public int IncludeCount { get; }

    public bool IncludeUnmatched { get; }
}
