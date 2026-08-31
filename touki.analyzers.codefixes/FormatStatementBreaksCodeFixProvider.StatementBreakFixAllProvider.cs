// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class FormatStatementBreaksCodeFixProvider
{
    /// <summary>
    ///  Applies statement-break fixes across the requested Fix All scope.
    /// </summary>
    private sealed class StatementBreakFixAllProvider : FixAllProvider
    {
        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            StatementBreakFixAllBudget budget = default;
            switch (fixAllContext.Scope)
            {
                case FixAllScope.Document when fixAllContext.Document is not null:
                    if (!TryAddDiagnostics(
                        diagnostics,
                        await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document).ConfigureAwait(false),
                        ref budget,
                        fixAllContext.CancellationToken))
                    {
                        return null;
                    }

                    break;
                case FixAllScope.Project:
                    if (!TryAddDiagnostics(
                        diagnostics,
                        await fixAllContext.GetAllDiagnosticsAsync(fixAllContext.Project).ConfigureAwait(false),
                        ref budget,
                        fixAllContext.CancellationToken))
                    {
                        return null;
                    }

                    break;
                case FixAllScope.Solution:
                    foreach (Project project in fixAllContext.Solution.Projects)
                    {
                        if (!TryAddDiagnostics(
                            diagnostics,
                            await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false),
                            ref budget,
                            fixAllContext.CancellationToken))
                        {
                            return null;
                        }
                    }

                    break;
            }

            Solution solution = fixAllContext.Solution;
            Dictionary<DocumentId, ImmutableArray<DocumentId>> relatedDocuments =
                IndexRelatedDocuments(solution, fixAllContext.CancellationToken);
            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument =
                IndexDocumentDiagnostics(solution, diagnostics.ToImmutable());
            HashSet<DocumentId> processedDocuments = [];
            bool changed = false;

            foreach (Diagnostic diagnostic in diagnostics)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                if (diagnostic.Id != StatementBreakFormattingId
                    || diagnostic.Location.SourceTree is null
                    || fixAllContext.Solution.GetDocument(diagnostic.Location.SourceTree) is not { } document
                    || processedDocuments.Contains(document.Id))
                {
                    continue;
                }

                ImmutableArray<DocumentId> documentIds = relatedDocuments[document.Id];
                foreach (DocumentId documentId in documentIds)
                {
                    processedDocuments.Add(documentId);
                }

                SourceText documentSource = await document.GetTextAsync(fixAllContext.CancellationToken)
                    .ConfigureAwait(false);
                ImmutableArray<DocumentId> compatibleDocumentIds = await TryGetCompatibleDocumentIdsAsync(
                    document,
                    documentSource,
                    documentIds,
                    fixAllContext.CancellationToken).ConfigureAwait(false);
                if (compatibleDocumentIds.IsDefault)
                {
                    continue;
                }

                SourceText? original = null;
                SourceText? compatible = null;
                foreach (DocumentId documentId in documentIds)
                {
                    Document? candidate = fixAllContext.Solution.GetDocument(documentId);
                    if (candidate is null)
                    {
                        compatible = null;
                        break;
                    }

                    SourceText source = await candidate.GetTextAsync(fixAllContext.CancellationToken)
                        .ConfigureAwait(false);
                    SyntaxNode? root = await candidate.GetSyntaxRootAsync(fixAllContext.CancellationToken)
                        .ConfigureAwait(false);
                    if (root is null)
                    {
                        compatible = null;
                        break;
                    }

                    if (original is not null && !original.ContentEquals(source))
                    {
                        compatible = null;
                        break;
                    }

                    original ??= source;
                    if (!diagnosticsByDocument.TryGetValue(documentId, out List<Diagnostic>? candidateDiagnostics))
                    {
                        IEnumerable<Diagnostic> fetchedDiagnostics =
                            await fixAllContext.GetDocumentDiagnosticsAsync(candidate).ConfigureAwait(false);
                        if (!TryCollectDiagnostics(
                            fetchedDiagnostics,
                            ref budget,
                            fixAllContext.CancellationToken,
                            out candidateDiagnostics))
                        {
                            return null;
                        }

                        diagnosticsByDocument.Add(documentId, candidateDiagnostics);
                    }

                    AnalyzerConfigOptions config = candidate.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider
                        .GetOptions(root.SyntaxTree);
                    string indentationUnit = StatementBreakFormattingOptions.GetIndentationUnit(config);

                    if (!TryApplyDiagnostics(
                        root,
                        source,
                        indentationUnit,
                        candidateDiagnostics,
                        fixAllContext.CancellationToken,
                        ref budget,
                        out SourceText formatted,
                        out bool budgetExceeded))
                    {
                        if (budgetExceeded)
                        {
                            return null;
                        }

                        compatible = null;
                        break;
                    }

                    if (compatible is not null && !compatible.ContentEquals(formatted))
                    {
                        compatible = null;
                        break;
                    }

                    compatible ??= formatted;
                }

                if (original is null || compatible is null || original.ContentEquals(compatible))
                {
                    continue;
                }

                foreach (DocumentId documentId in documentIds)
                {
                    solution = solution.WithDocumentText(documentId, compatible);
                }

                changed = true;
            }

            if (!changed)
            {
                return null;
            }

            return CodeAction.Create(
                Title,
                _ => Task.FromResult(solution),
                nameof(FormatStatementBreaksCodeFixProvider));
        }
    }
}