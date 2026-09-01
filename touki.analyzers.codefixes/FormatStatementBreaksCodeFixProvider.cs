// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Format statement break" fix for <c>TOUKI0028</c>.
/// </summary>
/// <remarks>
///  <para>
///   The analyzer records compact placement and indentation metadata. The fixer verifies the replacement and
///   base-indentation spans against the diagnostic syntax tree before materializing and applying replacement text.
///  </para>
///  <para>
///   Documents linked into several projects are changed together only when their source and indentation options
///   agree. Fix All additionally requires every linked analyzer context to produce the same complete output.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormatStatementBreaksCodeFixProvider))]
[Shared]
public sealed partial class FormatStatementBreaksCodeFixProvider : CodeFixProvider
{
    private const string StatementBreakFormattingId = "TOUKI0028";
    private const string Title = "Format statement break";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [StatementBreakFormattingId];
    private static readonly FixAllProvider s_fixAllProvider = new StatementBreakFixAllProvider();
    private static readonly StringComparer s_pathComparer =
        StatementBreakFormattingOptions.GetPathComparer(Path.DirectorySeparatorChar);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SourceText source = await context.Document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        AnalyzerConfigOptions config = context.Document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider
            .GetOptions(root.SyntaxTree);
        string indentationUnit = StatementBreakFormattingOptions.GetIndentationUnit(config);
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!TryGetChanges(
                diagnostic,
                root,
                source,
                indentationUnit,
                context.CancellationToken,
                out ImmutableArray<TextChange> changes,
                out _))
            {
                continue;
            }

            ImmutableArray<DocumentId> relatedDocumentIds = GetRelatedDocumentIds(
                context.Document,
                context.CancellationToken);
            ImmutableArray<DocumentId> documentIds = await TryGetCompatibleDocumentIdsAsync(
                context.Document,
                root,
                source,
                relatedDocumentIds,
                diagnostic,
                changes,
                context.CancellationToken).ConfigureAwait(false);
            if (documentIds.IsDefault)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    cancellationToken => ApplyChangesAsync(
                        context.Document.Project.Solution,
                        documentIds,
                        changes,
                        cancellationToken),
                    nameof(FormatStatementBreaksCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<Solution> ApplyChangesAsync(
        Solution solution,
        ImmutableArray<DocumentId> documentIds,
        ImmutableArray<TextChange> changes,
        CancellationToken cancellationToken)
    {
        foreach (DocumentId documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? document = solution.GetDocument(documentId);
            if (document is null)
            {
                continue;
            }

            SourceText source = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
            solution = solution.WithDocumentText(documentId, source.WithChanges(changes));
        }

        return solution;
    }

    private static async Task<ImmutableArray<DocumentId>> TryGetCompatibleDocumentIdsAsync(
        Document document,
        SyntaxNode documentRoot,
        SourceText source,
        ImmutableArray<DocumentId> documentIds,
        Diagnostic? directDiagnostic,
        ImmutableArray<TextChange> directChanges,
        CancellationToken cancellationToken)
    {
        if (documentIds.Length == 1)
        {
            return documentIds;
        }

        string? indentationUnit = null;
        ParseOptions? parseOptions = null;
        foreach (DocumentId documentId in documentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? candidate = document.Project.Solution.GetDocument(documentId);
            if (candidate is null)
            {
                return default;
            }

            SourceText candidateSource = await candidate.GetTextAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode? root = await candidate.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null
                || !StatementBreakFormatting.ContentEquals(source, candidateSource, cancellationToken))
            {
                return default;
            }

            if (parseOptions is not null
                && !StatementBreakFormatting.AreParseOptionsCompatible(
                    parseOptions,
                    root.SyntaxTree.Options))
            {
                return default;
            }

            parseOptions ??= root.SyntaxTree.Options;

            AnalyzerConfigOptions config = candidate.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider
                .GetOptions(root.SyntaxTree);
            string candidateIndentation = StatementBreakFormattingOptions.GetIndentationUnit(config);
            if (indentationUnit is not null
                && !string.Equals(indentationUnit, candidateIndentation, StringComparison.Ordinal))
            {
                return default;
            }

            indentationUnit = candidateIndentation;
            if (directDiagnostic is not null
                && !documentRoot.SyntaxTree.Options.Equals(root.SyntaxTree.Options)
                && !IsDifferenceInDisabledText(
                    root,
                    source,
                    source.WithChanges(directChanges),
                    cancellationToken)
                && (!TryGetChanges(
                    directDiagnostic,
                    root,
                    source,
                    candidateIndentation,
                    cancellationToken,
                    out ImmutableArray<TextChange> candidateChanges,
                    out _)
                    || !ChangesAreEquivalent(directChanges, candidateChanges, cancellationToken)))
            {
                return default;
            }
        }

        return documentIds;
    }

    private static bool ChangesAreEquivalent(
        ImmutableArray<TextChange> first,
        ImmutableArray<TextChange> second,
        CancellationToken cancellationToken)
    {
        if (first.Length != second.Length)
        {
            return false;
        }

        for (int index = 0; index < first.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (first[index].Span != second[index].Span
                || !string.Equals(first[index].NewText, second[index].NewText, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsDifferenceInDisabledText(
        SyntaxNode root,
        SourceText original,
        SourceText formatted,
        CancellationToken cancellationToken)
    {
        int commonLength = Math.Min(original.Length, formatted.Length);
        int start = 0;
        while (start < commonLength && original[start] == formatted[start])
        {
            if ((start & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            start++;
        }

        if (start == original.Length && start == formatted.Length)
        {
            return true;
        }

        int originalEnd = original.Length;
        int formattedEnd = formatted.Length;
        while (originalEnd > start
            && formattedEnd > start
            && original[originalEnd - 1] == formatted[formattedEnd - 1])
        {
            if (((original.Length - originalEnd) & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            originalEnd--;
            formattedEnd--;
        }

        TextSpan difference = TextSpan.FromBounds(start, originalEnd);
        SyntaxTrivia trivia = root.FindTrivia(start, findInsideTrivia: true);
        return trivia.IsKind(SyntaxKind.DisabledTextTrivia)
            && trivia.FullSpan.Contains(difference);
    }

    private static ImmutableArray<DocumentId> GetRelatedDocumentIds(
        Document document,
        CancellationToken cancellationToken)
    {
        ImmutableArray<DocumentId>.Builder documentIds = ImmutableArray.CreateBuilder<DocumentId>();
        string? filePath = document.FilePath;
        foreach (Project project in document.Project.Solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (project.Language != LanguageNames.CSharp)
            {
                continue;
            }

            foreach (Document candidate in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate.Id == document.Id
                    || filePath is not null
                        && s_pathComparer.Equals(candidate.FilePath, filePath))
                {
                    documentIds.Add(candidate.Id);
                }
            }
        }

        return documentIds.ToImmutable();
    }

    private static Dictionary<DocumentId, ImmutableArray<DocumentId>> IndexRelatedDocuments(
        Solution solution,
        CancellationToken cancellationToken)
    {
        Dictionary<string, List<DocumentId>> documentsByPath = new(s_pathComparer);
        Dictionary<DocumentId, ImmutableArray<DocumentId>> relatedDocuments = [];
        foreach (Project project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (project.Language != LanguageNames.CSharp)
            {
                continue;
            }

            foreach (Document document in project.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (document.FilePath is null)
                {
                    relatedDocuments.Add(document.Id, [document.Id]);
                    continue;
                }

                if (!documentsByPath.TryGetValue(document.FilePath, out List<DocumentId>? documentIds))
                {
                    documentIds = [];
                    documentsByPath.Add(document.FilePath, documentIds);
                }

                documentIds.Add(document.Id);
            }
        }

        foreach (List<DocumentId> documentIds in documentsByPath.Values)
        {
            ImmutableArray<DocumentId> group = [.. documentIds];
            foreach (DocumentId documentId in group)
            {
                relatedDocuments.Add(documentId, group);
            }
        }

        return relatedDocuments;
    }

    private static bool TryApplyDiagnostics(
        SyntaxNode root,
        SourceText source,
        string indentationUnit,
        IEnumerable<Diagnostic> diagnostics,
        CancellationToken cancellationToken,
        ref StatementBreakFixAllBudget budget,
        out SourceText formatted,
        out bool budgetExceeded)
    {
        budgetExceeded = false;
        List<TextChange> primaryChanges = [];
        List<TextChange> dependentChanges = [];
        foreach (Diagnostic diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id != StatementBreakFormattingId)
            {
                continue;
            }

            if (!TryGetChanges(
                diagnostic,
                root,
                source,
                indentationUnit,
                cancellationToken,
                out ImmutableArray<TextChange> diagnosticChanges,
                out bool intentionalNoFix))
            {
                if (intentionalNoFix)
                {
                    continue;
                }

                formatted = source;
                return false;
            }

            for (int index = 0; index < diagnosticChanges.Length; index++)
            {
                TextChange change = diagnosticChanges[index];
                if (!budget.TryReserveReplacementCharacters(change.NewText?.Length ?? 0))
                {
                    formatted = source;
                    budgetExceeded = true;
                    return false;
                }

                (index == 0 ? primaryChanges : dependentChanges).Add(change);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!TryNormalizeChanges(primaryChanges, cancellationToken, out List<TextChange> normalizedPrimary))
        {
            formatted = source;
            return false;
        }

        dependentChanges.RemoveAll(dependent => HasOverlap(normalizedPrimary, dependent));
        if (!TryNormalizeChanges(dependentChanges, cancellationToken, out List<TextChange> normalizedDependent))
        {
            formatted = source;
            return false;
        }

        normalizedPrimary.AddRange(normalizedDependent);
        if (!TryNormalizeChanges(normalizedPrimary, cancellationToken, out List<TextChange> normalized))
        {
            formatted = source;
            return false;
        }

        formatted = normalized.Count == 0 ? source : source.WithChanges(normalized);
        return true;
    }

    private static bool ChangesOverlap(TextChange first, TextChange second) =>
        first.Span == second.Span
        || first.Span.Contains(second.Span)
        || second.Span.Contains(first.Span)
        || first.Span.Start < second.Span.End && second.Span.Start < first.Span.End;

    private static bool HasOverlap(List<TextChange> sortedChanges, TextChange candidate)
    {
        int lower = 0;
        int upper = sortedChanges.Count;
        while (lower < upper)
        {
            int middle = lower + ((upper - lower) / 2);
            if (sortedChanges[middle].Span.Start < candidate.Span.Start)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower < sortedChanges.Count && ChangesOverlap(sortedChanges[lower], candidate)
            || lower > 0 && ChangesOverlap(sortedChanges[lower - 1], candidate);
    }

    private static bool TryNormalizeChanges(
        List<TextChange> changes,
        CancellationToken cancellationToken,
        out List<TextChange> normalized)
    {
        changes.Sort(static (left, right) =>
        {
            int startComparison = left.Span.Start.CompareTo(right.Span.Start);
            return startComparison != 0
                ? startComparison
                : right.Span.Length.CompareTo(left.Span.Length);
        });
        normalized = new(changes.Count);
        foreach (TextChange change in changes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (normalized.Count == 0)
            {
                normalized.Add(change);
                continue;
            }

            TextChange previous = normalized[normalized.Count - 1];
            if (previous.Span == change.Span)
            {
                if (!string.Equals(previous.NewText, change.NewText, StringComparison.Ordinal))
                {
                    return false;
                }

                continue;
            }

            if (previous.Span.Contains(change.Span))
            {
                continue;
            }

            if (previous.Span.End > change.Span.Start)
            {
                return false;
            }

            normalized.Add(change);
        }

        return true;
    }

    private static Dictionary<DocumentId, List<Diagnostic>> IndexDocumentDiagnostics(
        Solution solution,
        ImmutableArray<Diagnostic> diagnostics)
    {
        Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument = [];
        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Location.SourceTree is null
                || solution.GetDocument(diagnostic.Location.SourceTree) is not { } document)
            {
                continue;
            }

            if (!diagnosticsByDocument.TryGetValue(document.Id, out List<Diagnostic>? documentDiagnostics))
            {
                documentDiagnostics = [];
                diagnosticsByDocument.Add(document.Id, documentDiagnostics);
            }

            documentDiagnostics.Add(diagnostic);
        }

        return diagnosticsByDocument;
    }

    private static bool TryCollectDiagnostics(
        IEnumerable<Diagnostic> source,
        ref StatementBreakFixAllBudget budget,
        CancellationToken cancellationToken,
        out List<Diagnostic> diagnostics)
    {
        diagnostics = [];
        foreach (Diagnostic diagnostic in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!budget.TryReserveDiagnostics(1))
            {
                diagnostics = [];
                return false;
            }

            diagnostics.Add(diagnostic);
        }

        return true;
    }

    private static bool TryGetChanges(
        Diagnostic diagnostic,
        SyntaxNode root,
        SourceText source,
        string indentationUnit,
        CancellationToken cancellationToken,
        out ImmutableArray<TextChange> changes,
        out bool intentionalNoFix) =>
        StatementBreakDiagnosticData.TryCreateTextChanges(
            diagnostic,
            root,
            source,
            indentationUnit,
            cancellationToken,
            out changes,
            out intentionalNoFix);

    private static bool TryAddDiagnostics(
        ImmutableArray<Diagnostic>.Builder builder,
        IEnumerable<Diagnostic> diagnostics,
        ref StatementBreakFixAllBudget budget,
        CancellationToken cancellationToken)
    {
        foreach (Diagnostic diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!budget.TryReserveDiagnostics(1))
            {
                return false;
            }

            builder.Add(diagnostic);
        }

        return true;
    }

}