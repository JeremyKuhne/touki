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
///  Reports single-line XML documentation comments that do not follow the configured spacing and nested XML layout.
/// </summary>
/// <remarks>
///  <para>
///   Documentation comments are separated from preceding code by a blank line unless they immediately follow an
///   opening brace that starts a block or a preprocessor directive.
///  </para>
///  <para>
///   The rule formats paired XML elements as blocks, with one configurable indentation step for every XML
///   nesting level. A top-level element other than <c>summary</c> may stay on one line when it fits within the
///   configured maximum line length. A three-line element with exactly one content line is compacted when it
///   fits; elements with two or more content lines retain those line breaks.
///  </para>
///  <para>
///   Indentation is evaluated per contiguous logical block. A correctly indented first line preserves the
///   block's relative indentation; otherwise every line in the block is shifted by the same amount.
///  </para>
///  <para>
///   Unicode whitespace is documentation content rather than layout, and effective
///   <c>xml:space="preserve"</c> regions are not rewritten.
///  </para>
///  <para>
///   The rule ships disabled because documentation layout is a house style. Enable it with
///   <c>dotnet_diagnostic.TOUKI0024.severity = warning</c>.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XmlDocumentationFormattingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0024";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls the spaces added for each nested XML level.
    /// </summary>
    public const string IndentSizeOption = XmlDocumentationFormattingOptions.IndentSizeOption;

    /// <summary>
    ///  The <c>.editorconfig</c> key that overrides the maximum physical line length.
    /// </summary>
    public const string MaxLineLengthOption = XmlDocumentationFormattingOptions.MaxLineLengthOption;

    /// <summary>
    ///  The indentation added for each nested XML level when <see cref="IndentSizeOption"/> is not configured.
    /// </summary>
    public const int DefaultIndentSize = XmlDocumentationFormattingOptions.DefaultIndentSize;

    /// <summary>
    ///  The largest accepted XML indentation step. Larger configured values use
    ///  <see cref="DefaultIndentSize"/>.
    /// </summary>
    public const int MaximumIndentSize = XmlDocumentationFormattingOptions.MaximumIndentSize;

    /// <summary>
    ///  The maximum physical line length when neither <see cref="MaxLineLengthOption"/> nor the standard
    ///  <c>max_line_length</c> key supplies one.
    /// </summary>
    public const int DefaultMaxLineLength = XmlDocumentationFormattingOptions.DefaultMaxLineLength;

    /// <summary>
    ///  The diagnostic property carrying the complete replacement documentation comment.
    /// </summary>
    internal const string ReplacementProperty = "Replacement";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Format XML documentation",
        messageFormat: "Format XML documentation",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "XML documentation should be separated from preceding code and use a consistent nested layout "
            + "while preserving intentional prose line breaks.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        SourceText source = context.Tree.GetText(context.CancellationToken);
        if (!ContainsDocumentationCommentPrefix(source, context.CancellationToken))
        {
            return;
        }

        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);
        (int indentSize, int maxLineLength) = XmlDocumentationFormattingOptions.GetOptions(options);
        SyntaxNode root = context.Tree.GetRoot(context.CancellationToken);
        int latestPreprocessorLineNumber = -1;

        foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

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
                context.CancellationToken);
            bool hasCommentSpan = TryGetCommentSpan(
                source,
                trivia,
                context.CancellationToken,
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
                    context.CancellationToken,
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

            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(ReplacementProperty, replacement);

            context.ReportDiagnostic(
                Diagnostic.Create(s_rule, Location.Create(context.Tree, commentSpan), properties));
        }
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
