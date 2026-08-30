// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

internal static partial class AllmanFormatter
{
    /// <summary>
    ///  Classifies preprocessor directive and disabled-text lines.
    /// </summary>
    private sealed partial class PreprocessorLineMap
    {
        private readonly Dictionary<int, PreprocessorLineKind> _directiveLines = [];
        private readonly List<LineRange> _disabledRanges = [];

        public PreprocessorLineMap(
            SourceText source,
            SyntaxNode root,
            CancellationToken cancellationToken)
        {
            foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (trivia.GetStructure() is DirectiveTriviaSyntax directive)
                {
                    int lineNumber = source.Lines.GetLineFromPosition(directive.SpanStart).LineNumber;
                    _directiveLines[lineNumber] = directive.Kind() is
                        SyntaxKind.ElseDirectiveTrivia or SyntaxKind.ElifDirectiveTrivia
                            ? PreprocessorLineKind.AlternateBranch
                            : PreprocessorLineKind.Directive;
                    continue;
                }

                if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia) || trivia.Span.IsEmpty)
                {
                    continue;
                }

                int firstLine = source.Lines.GetLineFromPosition(trivia.SpanStart).LineNumber;
                int finalPosition = Math.Max(trivia.SpanStart, trivia.Span.End - 1);
                int finalLine = source.Lines.GetLineFromPosition(finalPosition).LineNumber;
                if (_disabledRanges.Count > 0)
                {
                    LineRange previous = _disabledRanges[_disabledRanges.Count - 1];
                    if (firstLine <= previous.FinalLine + 1)
                    {
                        _disabledRanges[_disabledRanges.Count - 1] = new(
                            previous.FirstLine,
                            Math.Max(previous.FinalLine, finalLine));
                        continue;
                    }
                }

                _disabledRanges.Add(new(firstLine, finalLine));
            }
        }

        public PreprocessorLineKind GetKind(int lineNumber)
        {
            if (_directiveLines.TryGetValue(lineNumber, out PreprocessorLineKind kind))
            {
                return kind;
            }

            int lower = 0;
            int upper = _disabledRanges.Count - 1;
            while (lower <= upper)
            {
                int middle = lower + ((upper - lower) / 2);
                LineRange range = _disabledRanges[middle];
                if (lineNumber < range.FirstLine)
                {
                    upper = middle - 1;
                }
                else if (lineNumber > range.FinalLine)
                {
                    lower = middle + 1;
                }
                else
                {
                    return PreprocessorLineKind.DisabledText;
                }
            }

            return PreprocessorLineKind.None;
        }
    }
}