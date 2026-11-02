// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Separates consecutive statements for <c>TOUKI0042</c> without reformatting surrounding code.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(OneStatementPerLineCodeFixProvider))]
[Shared]
public sealed class OneStatementPerLineCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "TOUKI0042";
    private const string Title = "Move statement to a new line";
    private const string EquivalenceKey = nameof(OneStatementPerLineCodeFixProvider);
    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [DiagnosticId];
    private static readonly FixAllProvider s_fixAllProvider = FixAllProvider.Create(
        static async (context, document, diagnostics) =>
            await FixDocumentAsync(document, diagnostics, context.CancellationToken).ConfigureAwait(false),
        [FixAllScope.Document, FixAllScope.Project, FixAllScope.Solution]);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            Document changed = await FixDocumentAsync(
                context.Document,
                [diagnostic],
                context.CancellationToken).ConfigureAwait(false);
            if (changed == context.Document)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(Title, _ => Task.FromResult(changed), EquivalenceKey),
                diagnostic);
        }
    }

    private static async Task<Document> FixDocumentAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (DocumentFileUtilities.HasSharedFilePath(document.Project.Solution, document, cancellationToken))
        {
            return document;
        }

        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        SourceText source = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        AnalyzerConfigOptions options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider
            .GetOptions(root.SyntaxTree);
        string unit = StatementBreakFormattingOptions.GetIndentationUnit(options);
        SortedDictionary<int, TextChange> changes = [];
        foreach (Diagnostic diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id != DiagnosticId
                || diagnostic.Location.SourceSpan is not { IsEmpty: false } span
                || span.End > source.Length)
            {
                continue;
            }

            SyntaxToken token = root.FindToken(span.Start);
            StatementSyntax? statement = token.Parent?.FirstAncestorOrSelf<StatementSyntax>();
            if (token.Span != span
                || token.IsMissing
                || statement is null
                || statement.GetFirstToken() != token
                || statement.FirstAncestorOrSelf<InterpolatedStringExpressionSyntax>() is not null)
            {
                continue;
            }

            SyntaxToken previous = token.GetPreviousToken();
            TextLine line = source.Lines.GetLineFromPosition(span.Start);
            if (previous.RawKind == 0
                || previous.Span.End > span.Start
                || source.Lines.GetLineFromPosition(previous.Span.End - 1).LineNumber != line.LineNumber)
            {
                continue;
            }

            int start = span.Start;
            while (start > previous.Span.End && source[start - 1] is ' ' or '\t')
            {
                start--;
            }

            string indentation = GetIndentation(source, statement, unit, cancellationToken);
            string newLine = GetNewLine(source, line, options);
            changes[start] = new(TextSpan.FromBounds(start, span.Start), newLine + indentation);
        }

        return changes.Count == 0 ? document : document.WithText(source.WithChanges(changes.Values));
    }

    private static string GetIndentation(
        SourceText source,
        StatementSyntax statement,
        string unit,
        CancellationToken cancellationToken)
    {
        StatementSyntax first = statement.Parent switch
        {
            BlockSyntax block => block.Statements[0],
            SwitchSectionSyntax section => section.Statements[0],
            _ => statement
        };
        TextLine line = source.Lines.GetLineFromPosition(first.SpanStart);
        int indentationEnd = line.Start;
        while (indentationEnd < first.SpanStart && source[indentationEnd] is ' ' or '\t')
        {
            indentationEnd++;
        }

        string indentation = source.ToString(TextSpan.FromBounds(line.Start, indentationEnd));
        if (indentationEnd == first.SpanStart)
        {
            return indentation;
        }

        // Compact outer bodies may share this line too; count each containing indentation level once.
        int levels = 0;
        for (SyntaxNode? parent = first.Parent; parent is not null; parent = parent.Parent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxToken openBrace = parent switch
            {
                BlockSyntax block => block.OpenBraceToken,
                BaseTypeDeclarationSyntax type => type.OpenBraceToken,
                NamespaceDeclarationSyntax declaration => declaration.OpenBraceToken,
                AccessorListSyntax accessors => accessors.OpenBraceToken,
                SwitchStatementSyntax @switch => @switch.OpenBraceToken,
                _ => default
            };
            if ((openBrace.RawKind != 0 && openBrace.SpanStart >= line.Start)
                || (parent is SwitchSectionSyntax && parent.SpanStart >= line.Start))
            {
                levels++;
            }
        }

        return indentation + new string(unit[0], checked(unit.Length * levels));
    }

    private static string GetNewLine(SourceText source, TextLine line, AnalyzerConfigOptions options)
    {
        if (options.TryGetValue("end_of_line", out string? configured))
        {
            switch (configured)
            {
                case "crlf":
                    return "\r\n";
                case "cr":
                    return "\r";
                case "lf":
                    return "\n";
            }
        }

        if (line.EndIncludingLineBreak > line.End)
        {
            return source.ToString(TextSpan.FromBounds(line.End, line.EndIncludingLineBreak));
        }

        foreach (TextLine candidate in source.Lines)
        {
            if (candidate.EndIncludingLineBreak > candidate.End)
            {
                return source.ToString(TextSpan.FromBounds(candidate.End, candidate.EndIncludingLineBreak));
            }
        }

        return "\n";
    }
}
