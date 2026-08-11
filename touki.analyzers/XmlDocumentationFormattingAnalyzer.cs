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
///  Reports single-line XML documentation comments that do not follow the configured nested XML layout.
/// </summary>
/// <remarks>
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
///   <code>dotnet_diagnostic.TOUKI0024.severity = warning</code>.
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
    public const string IndentSizeOption = "dotnet_code_quality.TOUKI0024.indent_size";

    /// <summary>
    ///  The <c>.editorconfig</c> key that overrides the maximum physical line length.
    /// </summary>
    public const string MaxLineLengthOption = "dotnet_code_quality.TOUKI0024.max_line_length";

    /// <summary>
    ///  The indentation added for each nested XML level when <see cref="IndentSizeOption"/> is not configured.
    /// </summary>
    public const int DefaultIndentSize = 1;

    /// <summary>
    ///  The largest accepted XML indentation step. Larger configured values use
    ///  <see cref="DefaultIndentSize"/>.
    /// </summary>
    public const int MaximumIndentSize = 16;

    /// <summary>
    ///  The maximum physical line length when neither <see cref="MaxLineLengthOption"/> nor the standard
    ///  <c>max_line_length</c> key supplies one.
    /// </summary>
    public const int DefaultMaxLineLength = 120;

    /// <summary>
    ///  The diagnostic property carrying the complete replacement documentation comment.
    /// </summary>
    internal const string ReplacementProperty = "Replacement";

    private const string StandardMaxLineLengthOption = "max_line_length";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Format XML documentation as nested XML",
        messageFormat: "Format XML documentation as nested XML",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "XML documentation should use a consistent nested layout while preserving intentional prose line breaks.",
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
        int indentSize = options.TryGetPositiveInteger(IndentSizeOption, out int configuredIndentSize)
            && configuredIndentSize <= MaximumIndentSize
                ? configuredIndentSize
                : DefaultIndentSize;
        int maxLineLength = options.TryGetPositiveInteger(MaxLineLengthOption, out int configuredMaxLineLength)
            || options.TryGetPositiveInteger(StandardMaxLineLengthOption, out configuredMaxLineLength)
                ? configuredMaxLineLength
                : DefaultMaxLineLength;
        SyntaxNode root = context.Tree.GetRoot(context.CancellationToken);

        foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (!trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.GetStructure() is not DocumentationCommentTriviaSyntax documentation
                || documentation.ContainsDiagnostics
                || !TryGetCommentSpan(source, trivia, context.CancellationToken, out TextSpan span))
            {
                continue;
            }

            TextLine firstLine = source.Lines.GetLineFromPosition(span.Start);
            string lineBreak = firstLine.EndIncludingLineBreak > firstLine.End
                ? source.ToString(TextSpan.FromBounds(firstLine.End, firstLine.EndIncludingLineBreak))
                : "\n";

            if (!XmlDocumentationCommentFormatter.TryFormat(
                source,
                documentation,
                span,
                lineBreak,
                indentSize,
                maxLineLength,
                context.CancellationToken,
                out string replacement))
            {
                continue;
            }

            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(ReplacementProperty, replacement);

            context.ReportDiagnostic(
                Diagnostic.Create(s_rule, Location.Create(context.Tree, span), properties));
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
