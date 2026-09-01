// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Format XML documentation comment" fix for <c>TOUKI0024</c>.
/// </summary>
/// <remarks>
///  <para>
///   The analyzer computes the complete replacement because it owns the per-file indentation and line-length
///   configuration. The fix applies that replacement without reinterpreting the documentation XML.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormatXmlDocumentationCodeFixProvider))]
[Shared]
public sealed partial class FormatXmlDocumentationCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; these are stable public contracts.
    private const string XmlDocumentationFormattingId = "TOUKI0024";
    private const string ReplacementProperty = "Replacement";
    private const string Title = "Format XML documentation comment";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [XmlDocumentationFormattingId];
    private static readonly FixAllProvider s_documentFixAllProvider = FixAllProvider.Create(
        FixAllAsync,
        [
            FixAllScope.Document,
            FixAllScope.Project,
            FixAllScope.Solution,
            FixAllScope.ContainingMember,
            FixAllScope.ContainingType
        ]);
    private static readonly FixAllProvider s_fixAllProvider = new NonEmptyFixAllProvider();

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SourceText source = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        ImmutableArray<DocumentId> documentIds = await GetCompatibleDocumentIdsAsync(
            context.Document,
            context.CancellationToken).ConfigureAwait(false);
        if (documentIds.IsDefault)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(ReplacementProperty, out string? replacement)
                || replacement is null)
            {
                continue;
            }

            TextSpan span = diagnostic.Location.SourceSpan;
            if (string.Equals(source.ToString(span), replacement, StringComparison.Ordinal))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    cancellationToken => ApplyChangeAsync(
                        context.Document.Project.Solution,
                        documentIds,
                        span,
                        replacement,
                        cancellationToken),
                    nameof(FormatXmlDocumentationCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<ImmutableArray<DocumentId>> GetCompatibleDocumentIdsAsync(
        Document document,
        CancellationToken cancellationToken)
    {
        ImmutableArray<DocumentId> documentIds = DocumentFileUtilities.GetRelatedDocumentIds(
            document,
            cancellationToken);
        if (documentIds.IsDefaultOrEmpty)
        {
            return default;
        }

        if (documentIds.Length == 1)
        {
            return documentIds;
        }

        SourceText expectedSource = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode? expectedRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (expectedRoot is null)
        {
            return default;
        }

        (int IndentSize, int MaxLineLength) expectedOptions = GetFormattingOptions(
            document,
            expectedRoot.SyntaxTree);
        foreach (DocumentId documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? candidate = document.Project.Solution.GetDocument(documentId);
            if (candidate is null)
            {
                return default;
            }

            SourceText source = await candidate.GetTextAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode? root = await candidate.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null
                || !expectedSource.ContentEquals(source)
                || !expectedRoot.SyntaxTree.Options.Equals(root.SyntaxTree.Options)
                || GetFormattingOptions(candidate, root.SyntaxTree) != expectedOptions)
            {
                return default;
            }
        }

        return documentIds;
    }

    private static (int IndentSize, int MaxLineLength) GetFormattingOptions(
        Document document,
        SyntaxTree syntaxTree)
    {
        AnalyzerConfigOptions options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider
            .GetOptions(syntaxTree);
        return XmlDocumentationFormattingOptions.GetOptions(options);
    }

    private static async Task<Solution> ApplyChangeAsync(
        Solution solution,
        ImmutableArray<DocumentId> documentIds,
        TextSpan span,
        string replacement,
        CancellationToken cancellationToken)
    {
        TextChange change = new(span, replacement);
        foreach (DocumentId documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? document = solution.GetDocument(documentId);
            if (document is null)
            {
                continue;
            }

            SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            solution = solution.WithDocumentText(documentId, text.WithChanges(change));
        }

        return solution;
    }

    private static async Task<Document?> FixAllAsync(
        FixAllContext context,
        Document document,
        ImmutableArray<Diagnostic> diagnostics)
    {
        if (!TryCreateChanges(
            diagnostics,
            context.CancellationToken,
            out List<TextChange> changes))
        {
            return null;
        }

        ImmutableArray<DocumentId> relatedDocumentIds = DocumentFileUtilities.GetRelatedDocumentIds(
            document,
            context.CancellationToken);
        if (relatedDocumentIds.IsDefaultOrEmpty || relatedDocumentIds.Length != 1)
        {
            return null;
        }

        SourceText text = await document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        SourceText changedText = text.WithChanges(changes);
        return text.ContentEquals(changedText) ? null : document.WithText(changedText);
    }

    private static bool TryCreateChanges(
        IEnumerable<Diagnostic> diagnostics,
        CancellationToken cancellationToken,
        out List<TextChange> changes)
    {
        changes = [];
        foreach (Diagnostic diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id == XmlDocumentationFormattingId
                && diagnostic.Properties.TryGetValue(ReplacementProperty, out string? replacement)
                && replacement is not null)
            {
                changes.Add(new(diagnostic.Location.SourceSpan, replacement));
            }
        }

        if (changes.Count == 0)
        {
            return false;
        }

        Comparison<TextChange> comparison = (left, right) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            int result = left.Span.Start.CompareTo(right.Span.Start);
            return result != 0 ? result : left.Span.Length.CompareTo(right.Span.Length);
        };
        try
        {
            changes.Sort(comparison);
        }
        catch (InvalidOperationException exception)
            when (exception.InnerException is OperationCanceledException
                && cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }

        for (int index = changes.Count - 1; index > 0; index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TextChange current = changes[index];
            TextChange previous = changes[index - 1];
            if (current.Span != previous.Span)
            {
                if (previous.Span.End > current.Span.Start)
                {
                    return false;
                }

                continue;
            }

            if (!string.Equals(current.NewText, previous.NewText, StringComparison.Ordinal))
            {
                return false;
            }

            changes.RemoveAt(index);
        }

        return true;
    }

    private static bool ChangesAreEquivalent(
        List<TextChange> expected,
        List<TextChange> candidate,
        CancellationToken cancellationToken)
    {
        if (expected.Count != candidate.Count)
        {
            return false;
        }

        for (int index = 0; index < expected.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expected[index].Span != candidate[index].Span
                || !string.Equals(expected[index].NewText, candidate[index].NewText, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

}
