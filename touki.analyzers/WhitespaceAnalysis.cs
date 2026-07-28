// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Shared support for the rules that rewrite raw whitespace (<c>TOUKI0022</c> and <c>TOUKI0023</c>).
/// </summary>
/// <remarks>
///  <para>
///   Both rules edit source text rather than syntax, so both need to know which regions of a file hold text
///   whose exact bytes are part of the program's meaning. Rewriting whitespace inside a multi-line raw string
///   literal changes the string's value; rewriting it inside conditionally excluded text changes whatever
///   that text becomes once its symbol is defined. Those regions are reported by
///   <see cref="GetProtectedSpans"/> and are never reported by either rule.
///  </para>
/// </remarks>
internal static class WhitespaceAnalysis
{
    /// <summary>
    ///  Returns the spans of <paramref name="root"/> whose exact text is significant, in ascending order and
    ///  without overlaps.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Covers every string and character literal - regular, verbatim, raw, UTF-8, and the text chunks of an
    ///   interpolated string - plus text excluded by conditional compilation, which the parser never
    ///   interprets and this analyzer therefore cannot reason about.
    ///  </para>
    /// </remarks>
    internal static ImmutableArray<TextSpan> GetProtectedSpans(SyntaxNode root, CancellationToken cancellationToken)
    {
        List<TextSpan> spans = [];

        foreach (SyntaxToken token in root.DescendantTokens(descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsTextSignificant(token))
            {
                spans.Add(token.Span);
            }
        }

        // Disabled text arrives as trivia rather than as a token, so it needs its own pass.
        foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            {
                spans.Add(trivia.Span);
            }
        }

        // Token spans and trivia spans are each ascending and the two never overlap, but the two passes
        // interleave, so the combined list has to be ordered before it can be binary searched.
        spans.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        return [.. spans];
    }

    /// <summary>
    ///  Returns <see langword="true"/> when <paramref name="span"/> overlaps any span in
    ///  <paramref name="protectedSpans"/>, which must be sorted ascending and non-overlapping.
    /// </summary>
    internal static bool IsProtected(ImmutableArray<TextSpan> protectedSpans, TextSpan span)
    {
        // Find the first protected span that could reach span.Start, then test only that one: the spans are
        // sorted and disjoint, so no earlier span can extend past it.
        int low = 0;
        int high = protectedSpans.Length - 1;
        int candidate = -1;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);

            if (protectedSpans[middle].End > span.Start)
            {
                candidate = middle;
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        return candidate >= 0 && protectedSpans[candidate].Start < span.End;
    }

    /// <summary>
    ///  Returns the visual column of <paramref name="position"/> within <paramref name="line"/>, counting a
    ///  tab as an advance to the next multiple of <paramref name="tabWidth"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Columns are counted in UTF-16 code units, which is what an editor's tab stops count. A surrogate
    ///   pair or a wide glyph therefore counts as it does in the editor's own column ruler rather than by
    ///   rendered width.
    ///  </para>
    /// </remarks>
    internal static int GetVisualColumn(SourceText text, TextLine line, int position, int tabWidth)
    {
        int column = 0;

        for (int index = line.Start; index < position; index++)
        {
            column = text[index] == '\t'
                ? column + tabWidth - (column % tabWidth)
                : column + 1;
        }

        return column;
    }

    private static bool IsTextSignificant(SyntaxToken token) => token.Kind() switch
    {
        // Covers both the regular and the verbatim (@"...") forms.
        SyntaxKind.StringLiteralToken
            or SyntaxKind.SingleLineRawStringLiteralToken
            or SyntaxKind.MultiLineRawStringLiteralToken
            or SyntaxKind.Utf8StringLiteralToken
            or SyntaxKind.Utf8SingleLineRawStringLiteralToken
            or SyntaxKind.Utf8MultiLineRawStringLiteralToken
            or SyntaxKind.CharacterLiteralToken
            or SyntaxKind.InterpolatedStringTextToken => true,
        _ => false
    };
}
