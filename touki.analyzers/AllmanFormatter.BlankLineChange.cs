// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

internal static partial class AllmanFormatter
{
    /// <summary>
    ///  Describes a blank-line edit and the line whose successor may move.
    /// </summary>
    private readonly struct BlankLineChange
    {
        public BlankLineChange(
            TextSpan span,
            TextLine line,
            bool movesSameLineSuccessor)
        {
            Span = span;
            Line = line;
            MovesSameLineSuccessor = movesSameLineSuccessor;
        }

        public TextSpan Span { get; }

        public TextLine Line { get; }

        public bool MovesSameLineSuccessor { get; }
    }
}