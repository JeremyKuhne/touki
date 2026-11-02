// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Wraps parameter and argument lists for <c>TOUKI0043</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormatParameterListsCodeFixProvider))]
[Shared]
public sealed class FormatParameterListsCodeFixProvider : CodeFixProvider
{
    private const string DiagnosticId = "TOUKI0043";
    private const string Title = "Wrap and indent every parameter or argument";
    private const string EquivalenceKey = nameof(FormatParameterListsCodeFixProvider);
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
        ImmutableArray<DocumentId> related = DocumentFileUtilities.GetRelatedDocumentIds(document, cancellationToken);
        if (related.IsDefaultOrEmpty)
        {
            return document;
        }

        if (related.Length == 1)
        {
            return await FixDocumentCoreAsync(document, diagnostics, cancellationToken).ConfigureAwait(false);
        }

        SourceText original = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        SourceText? compatible = null;
        int? maxLineLength = null;
        string? indentation = null;
        foreach (DocumentId id in related)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? candidate = document.Project.Solution.GetDocument(id);
            if (candidate is null)
            {
                return document;
            }

            SourceText source = await candidate.GetTextAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode? root = await candidate.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null || !source.ContentEquals(original))
            {
                return document;
            }

            AnalyzerConfigOptions config =
                candidate.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(root.SyntaxTree);
            int candidateLimit = ParameterListFormatting.GetMaxLineLength(config);
            string candidateIndentation = StatementBreakFormattingOptions.GetIndentationUnit(config);
            if ((maxLineLength is not null && candidateLimit != maxLineLength)
                || (indentation is not null && candidateIndentation != indentation))
            {
                return document;
            }

            maxLineLength = candidateLimit;
            indentation = candidateIndentation;
            Document changed = await FixDocumentCoreAsync(
                candidate,
                diagnostics,
                cancellationToken).ConfigureAwait(
                    false);
            SourceText text = await changed.GetTextAsync(cancellationToken).ConfigureAwait(false);
            if (text.ContentEquals(original) || (compatible is not null && !compatible.ContentEquals(text)))
            {
                return document;
            }

            compatible = text;
        }

        if (compatible is null)
        {
            return document;
        }

        Solution solution = document.Project.Solution;
        foreach (DocumentId id in related)
        {
            solution = solution.WithDocumentText(id, compatible);
        }

        return solution.GetDocument(document.Id)
            ?? throw new InvalidOperationException("The formatted document is missing from the solution.");
    }

    private static async Task<Document> FixDocumentCoreAsync(
        Document document,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        SourceText source = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        SemanticModel? initialModel = null;
        HashSet<SyntaxNode> lists = [];
        string? indentation = null;
        int maxLineLength = 0;
        foreach (Diagnostic diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id != DiagnosticId
                || !diagnostic.Properties.TryGetValue("MaxLineLength", out string? lengthText)
                || !int.TryParse(lengthText, NumberStyles.None, CultureInfo.InvariantCulture, out int limit)
                || limit <= 0
                || !diagnostic.Properties.TryGetValue("Indentation", out string? unit)
                || !IsIndentationUnit(unit)
                || diagnostic.Location.SourceSpan is not { Length: 1 } span
                || span.End > source.Length)
            {
                continue;
            }

            SyntaxToken open = root.FindToken(span.Start);
            SyntaxNode? list = open.Parent;
            if (open.Span != span
                || list is null
                || !ParameterListFormatting.IsList(list)
                || list.GetFirstToken() != open
                || !ParameterListFormatting.IsViolation(list, source, limit, unit, cancellationToken)
                || !CanRewrite(list, cancellationToken))
            {
                continue;
            }

            if (ParameterListFormatting.IsNameofCandidate(list))
            {
                initialModel ??= await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                if (initialModel is null || ParameterListFormatting.IsNameofOperator(list, initialModel, cancellationToken))
                {
                    continue;
                }
            }

            if (indentation is not null && (indentation != unit || maxLineLength != limit))
            {
                return document;
            }

            indentation = unit;
            maxLineLength = limit;
            lists.Add(list);
        }

        if (lists.Count == 0 || indentation is null)
        {
            return document;
        }

        HashSet<SyntaxNode> affected = [.. lists];
        foreach (SyntaxNode list in lists)
        {
            foreach (SyntaxNode descendant in list.DescendantNodes())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ParameterListFormatting.IsList(descendant))
                {
                    affected.Add(descendant);
                }
            }
        }

        SyntaxAnnotation tracking = new();
        SyntaxAnnotation formatting = new();
        root = root.ReplaceNodes(affected, (_, rewritten) => rewritten.WithAdditionalAnnotations(tracking));
        Document changed = document.WithSyntaxRoot(root);
        OptionSet options = await document.GetOptionsAsync(cancellationToken).ConfigureAwait(false);
        string newLine = GetNewLine(document, root, source);
        int tabWidth = options.GetOption(FormattingOptions.TabSize, LanguageNames.CSharp);
        if (tabWidth is < 1 or > StatementBreakFormattingOptions.MaximumIndentSize)
        {
            tabWidth = StatementBreakFormattingOptions.DefaultIndentSize;
        }

        options = options
            .WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, indentation == "\t")
            .WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, tabWidth)
            .WithChangedOption(
                FormattingOptions.IndentationSize,
                LanguageNames.CSharp,
                indentation == "\t" ? tabWidth : indentation.Length)
            .WithChangedOption(FormattingOptions.NewLine, LanguageNames.CSharp, newLine);

        // Wrapping an outer list can move the indentation anchor of an already-formatted inner list.
        // Each pass settles another nesting level, rather than formatting once per diagnostic.
        for (int pass = 0; pass <= 256; pass++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            root = await changed.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            source = await changed.GetTextAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            List<SyntaxNode> violations = [];
            SemanticModel? model = null;
            foreach (SyntaxNode list in root.GetAnnotatedNodes(tracking))
            {
                if (!ParameterListFormatting.IsViolation(list, source, maxLineLength, indentation, cancellationToken))
                {
                    continue;
                }

                if (ParameterListFormatting.IsNameofCandidate(list))
                {
                    model ??= await changed.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
                    if (model is null)
                    {
                        return document;
                    }

                    if (ParameterListFormatting.IsNameofOperator(list, model, cancellationToken))
                    {
                        continue;
                    }
                }

                if (!CanRewrite(list, cancellationToken))
                {
                    return document;
                }

                violations.Add(list);
            }

            if (violations.Count == 0)
            {
                return changed;
            }

            SyntaxNode changedRoot = root.ReplaceNodes(
                violations,
                (original, rewritten) => RewriteList(
                    rewritten,
                    GetIndentation(source, original) + indentation,
                    newLine).WithAdditionalAnnotations(formatting));
            changed = await Formatter.FormatAsync(
                changed.WithSyntaxRoot(changedRoot),
                formatting,
                options,
                cancellationToken).ConfigureAwait(false);
        }

        return document;
    }

    private static bool IsIndentationUnit([NotNullWhen(true)] string? unit)
    {
        if (unit is null || unit.Length is < 1 or > StatementBreakFormattingOptions.MaximumIndentSize)
        {
            return false;
        }

        if (unit == "\t")
        {
            return true;
        }

        foreach (char character in unit)
        {
            if (character != ' ')
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanRewrite(SyntaxNode list, CancellationToken cancellationToken)
    {
        int depth = 0;
        for (SyntaxNode? parent = list; parent is not null; parent = parent.Parent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (++depth > 256 || parent is InterpolatedStringExpressionSyntax)
            {
                return false;
            }
        }

        SyntaxToken previous = list.GetFirstToken();
        foreach (SyntaxNodeOrToken child in list.ChildNodesAndTokens())
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNode? item = child.AsNode();
            SyntaxToken first = item?.GetFirstToken() ?? child.AsToken();
            if (first == previous)
            {
                continue;
            }

            if (!IsWhitespace(previous.TrailingTrivia) || !IsWhitespace(first.LeadingTrivia))
            {
                return false;
            }

            previous = item?.GetLastToken() ?? first;
        }

        return true;
    }

    private static bool IsWhitespace(SyntaxTriviaList trivia)
    {
        foreach (SyntaxTrivia item in trivia)
        {
            if (!item.IsKind(SyntaxKind.WhitespaceTrivia) && !item.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetIndentation(SourceText source, SyntaxNode list)
    {
        TextLine line = source.Lines.GetLineFromPosition(list.SpanStart);
        int end = line.Start;
        while (end < list.SpanStart && source[end] is ' ' or '\t')
        {
            end++;
        }

        return source.ToString(TextSpan.FromBounds(line.Start, end));
    }

    private static SyntaxNode RewriteList(SyntaxNode list, string indentation, string newLine)
    {
        Dictionary<SyntaxToken, SyntaxToken> replacements = [];
        ChildSyntaxList children = list.ChildNodesAndTokens();
        foreach (SyntaxNodeOrToken child in children)
        {
            if (child.AsNode() is { } item)
            {
                SetTrivia(
                    item.GetFirstToken(),
                    leading: true,
                    SyntaxFactory.TriviaList(SyntaxFactory.EndOfLine(newLine), SyntaxFactory.Whitespace(indentation)));
                SetTrivia(item.GetLastToken(), leading: false, default);
            }
            else
            {
                SyntaxToken token = child.AsToken();
                if (token != list.GetFirstToken())
                {
                    SetTrivia(token, leading: true, default);
                }

                if (token != list.GetLastToken())
                {
                    SetTrivia(token, leading: false, default);
                }
            }
        }

        return list.ReplaceTokens(replacements.Keys, (original, _) => replacements[original]);

        void SetTrivia(SyntaxToken token, bool leading, SyntaxTriviaList trivia)
        {
            SyntaxToken current = replacements.TryGetValue(token, out SyntaxToken existing) ? existing : token;
            replacements[token] = leading ? current.WithLeadingTrivia(trivia) : current.WithTrailingTrivia(trivia);
        }
    }

    private static string GetNewLine(Document document, SyntaxNode root, SourceText source)
    {
        if (document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(root.SyntaxTree)
            .TryGetValue("end_of_line", out string? configured))
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

        TextLine first = source.Lines[0];
        return first.EndIncludingLineBreak > first.End
            ? source.ToString(TextSpan.FromBounds(first.End, first.EndIncludingLineBreak))
            : "\n";
    }
}
