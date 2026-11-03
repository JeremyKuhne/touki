// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Computes XML documentation edits from a syntax tree and its per-file formatting options.
/// </summary>
internal static class XmlDocumentationFormatting
{
    /// <summary>
    ///  Computes replacements only for documentation active in this compilation context.
    /// </summary>
    /// <param name="tree">The context-specific syntax tree.</param>
    /// <param name="options">The per-file analyzer configuration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Nonoverlapping replacement edits in source order.</returns>
    public static ImmutableArray<TextChange> GetChanges(
        SyntaxTree tree,
        AnalyzerConfigOptions options,
        CancellationToken cancellationToken)
    {
        SourceText source = tree.GetText(cancellationToken);
        if (!ContainsDocumentationCommentPrefix(source, cancellationToken))
        {
            return [];
        }

        (int indentSize, int maxLineLength) = XmlDocumentationFormattingOptions.GetOptions(options);
        SyntaxNode root = tree.GetRoot(cancellationToken);
        ImmutableArray<TextChange>.Builder? changes = null;
        int latestPreprocessorLineNumber = -1;

        foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (trivia.HasStructure
                && trivia.GetStructure() is DirectiveTriviaSyntax directive)
            {
                latestPreprocessorLineNumber = source.Lines.GetLineFromPosition(directive.SpanStart).LineNumber;
                continue;
            }

            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentation)
            {
                continue;
            }

            TextLine firstLine = source.Lines.GetLineFromPosition(trivia.FullSpan.Start);
            SyntaxToken previousToken = trivia.Token.GetPreviousToken(
                includeZeroWidth: false,
                includeSkipped: false,
                includeDirectives: false,
                includeDocumentationComments: false);
            int prefixLength = trivia.FullSpan.Start - firstLine.Start;
            if ((previousToken.RawKind != 0 && previousToken.Span.End > firstLine.Start)
                || prefixLength > XmlDocumentationCommentFormatter.MaximumCommentLength
                || !XmlDocumentationCommentFormatter.TryGetExteriorIndex(source, firstLine, out _))
            {
                continue;
            }

            bool requiresLeadingBlankLine = RequiresLeadingBlankLine(
                source,
                firstLine.LineNumber,
                latestPreprocessorLineNumber,
                previousToken,
                cancellationToken);
            bool hasCommentSpan = TryGetCommentSpan(
                source,
                trivia,
                cancellationToken,
                out TextSpan commentSpan);
            string lineBreak = GetLineBreak(source, firstLine);
            string? replacement = null;
            if (hasCommentSpan && !documentation.ContainsDiagnostics)
            {
                if (XmlDocumentationCommentFormatter.TryFormat(
                    source,
                    documentation,
                    commentSpan,
                    lineBreak,
                    indentSize,
                    maxLineLength,
                    cancellationToken,
                    out string formattedComment))
                {
                    replacement = formattedComment;
                }
            }

            if (replacement is null && !requiresLeadingBlankLine)
            {
                continue;
            }

            if (replacement is not null)
            {
                if (requiresLeadingBlankLine)
                {
                    if (replacement.Length <= XmlDocumentationCommentFormatter.MaximumReplacementLength - lineBreak.Length)
                    {
                        replacement = string.Concat(lineBreak, replacement);
                    }
                    else
                    {
                        commentSpan = new(firstLine.Start, 0);
                        replacement = lineBreak;
                    }
                }
            }
            else
            {
                commentSpan = new(firstLine.Start, 0);
                replacement = lineBreak;
            }

            changes ??= ImmutableArray.CreateBuilder<TextChange>();
            changes.Add(new(commentSpan, replacement));
        }

        return changes?.ToImmutable() ?? [];
    }

    private static bool ContainsDocumentationCommentPrefix(
        SourceText source,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index <= source.Length - 3; index++)
        {
            if ((index & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (source[index] == '/' && source[index + 1] == '/' && source[index + 2] == '/')
            {
                return true;
            }
        }

        return false;
    }

    private static bool RequiresLeadingBlankLine(
        SourceText source,
        int firstLineNumber,
        int latestPreprocessorLineNumber,
        SyntaxToken previousToken,
        CancellationToken cancellationToken)
    {
        if (firstLineNumber == 0)
        {
            return false;
        }

        int previousLineNumber = firstLineNumber - 1;
        if (latestPreprocessorLineNumber == previousLineNumber)
        {
            return false;
        }

        TextLine previousLine = source.Lines[previousLineNumber];
        int index = previousLine.Start;
        while (index < previousLine.End && char.IsWhiteSpace(source[index]))
        {
            if (((index - previousLine.Start) & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            index++;
        }

        if (index == previousLine.End)
        {
            return false;
        }

        if (previousToken.SpanStart < previousLine.Start
            || previousToken.Span.End > previousLine.End
            || !previousToken.IsKind(SyntaxKind.OpenBraceToken))
        {
            return true;
        }

        return previousToken.Parent is not BlockSyntax
            and not BaseNamespaceDeclarationSyntax
            and not BaseTypeDeclarationSyntax
            and not AccessorListSyntax
            and not SwitchStatementSyntax;
    }

    private static string GetLineBreak(SourceText source, TextLine line)
    {
        if (line.EndIncludingLineBreak > line.End)
        {
            return source.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
        }

        if (line.LineNumber > 0)
        {
            TextLine previousLine = source.Lines[line.LineNumber - 1];
            if (previousLine.EndIncludingLineBreak > previousLine.End)
            {
                return source.ToString(TextSpan.FromBounds(
                    previousLine.End,
                    previousLine.EndIncludingLineBreak));
            }
        }

        return "\n";
    }

    private static bool TryGetCommentSpan(
        SourceText source,
        SyntaxTrivia trivia,
        CancellationToken cancellationToken,
        out TextSpan span)
    {
        int firstLineNumber = source.Lines.GetLinePosition(trivia.SpanStart).Line;
        int firstLineStart = source.Lines[firstLineNumber].Start;
        int lastLineNumber = firstLineNumber - 1;

        for (int lineNumber = firstLineNumber; lineNumber < source.Lines.Count; lineNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextLine line = source.Lines[lineNumber];
            if (line.Start >= trivia.FullSpan.End)
            {
                break;
            }

            if (line.End - firstLineStart > XmlDocumentationCommentFormatter.MaximumCommentLength)
            {
                span = default;
                return false;
            }

            if (!XmlDocumentationCommentFormatter.TryGetExteriorIndex(source, line, out _))
            {
                break;
            }

            lastLineNumber = lineNumber;
        }

        if (lastLineNumber < firstLineNumber)
        {
            span = default;
            return false;
        }

        span = TextSpan.FromBounds(
            source.Lines[firstLineNumber].Start,
            source.Lines[lastLineNumber].End);

        return true;
    }
}
