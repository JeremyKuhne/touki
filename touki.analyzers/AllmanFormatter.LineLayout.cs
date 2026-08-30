// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class AllmanFormatter
{
    /// <summary>
    ///  Captures the significant text and indentation boundaries of one source line.
    /// </summary>
    private readonly struct LineLayout
    {
        public LineLayout(
            int firstNonWhitespace,
            int lastNonWhitespaceExclusive,
            int indentationEnd,
            bool containsHash)
        {
            FirstNonWhitespace = firstNonWhitespace;
            LastNonWhitespaceExclusive = lastNonWhitespaceExclusive;
            IndentationEnd = indentationEnd;
            ContainsHash = containsHash;
        }

        public int FirstNonWhitespace { get; }

        public int LastNonWhitespaceExclusive { get; }

        public int IndentationEnd { get; }

        public bool ContainsHash { get; }
    }
}