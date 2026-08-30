// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis;

namespace Touki.Analyzers;

internal static partial class AllmanFormatter
{
    /// <summary>
    ///  Describes an opening and closing brace and the containing brace position.
    /// </summary>
    private readonly struct BracePair
    {
        public BracePair(
            SyntaxToken openBrace,
            SyntaxToken closeBrace,
            int parentOpenBracePosition)
        {
            OpenBrace = openBrace;
            CloseBrace = closeBrace;
            ParentOpenBracePosition = parentOpenBracePosition;
        }

        public SyntaxToken OpenBrace { get; }

        public SyntaxToken CloseBrace { get; }

        public int ParentOpenBracePosition { get; }
    }
}