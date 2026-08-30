// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class XmlDocumentationCommentFormatter
{
    /// <summary>
    ///  Describes one logical documentation block boundary and its nesting depth.
    /// </summary>
    private sealed class BreakBoundary(int start, int end, int depth, bool includeBlankLine)
    {
        public int Start { get; } = start;

        public int End { get; set; } = end;

        public int Depth { get; set; } = depth;

        public bool IncludeBlankLine { get; set; } = includeBlankLine;
    }
}