// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class FormatXmlDocumentationCodeFixProvider
{
    private sealed class NonEmptyFixAllProvider : FixAllProvider
    {
        public override IEnumerable<FixAllScope> GetSupportedFixAllScopes() =>
            s_documentFixAllProvider.GetSupportedFixAllScopes();

        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (fixAllContext.CodeActionEquivalenceKey != nameof(FormatXmlDocumentationCodeFixProvider))
            {
                return null;
            }

            if (fixAllContext.Scope == FixAllScope.Solution)
            {
                return await GetSolutionFixAsync(fixAllContext).ConfigureAwait(false);
            }

            CodeAction? action = await s_documentFixAllProvider.GetFixAsync(fixAllContext).ConfigureAwait(false);
            if (action is null)
            {
                return null;
            }

            ImmutableArray<CodeActionOperation> operations =
                await action.GetOperationsAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
            Solution? changedSolution = null;
            foreach (CodeActionOperation operation in operations)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                if (operation is ApplyChangesOperation applyChanges)
                {
                    changedSolution = applyChanges.ChangedSolution;
                    break;
                }
            }

            if (changedSolution is null || changedSolution == fixAllContext.Solution)
            {
                return null;
            }

            if (!await HasDocumentTextChangesAsync(
                fixAllContext.Solution,
                changedSolution,
                fixAllContext.CancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            return CodeAction.Create(
                Title,
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(changedSolution);
                },
                nameof(FormatXmlDocumentationCodeFixProvider));
        }

        private static async Task<CodeAction?> GetSolutionFixAsync(FixAllContext context)
        {
            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument = [];
            foreach (Project project in context.Solution.Projects)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                IEnumerable<Diagnostic> diagnostics = await context.GetAllDiagnosticsAsync(project)
                    .ConfigureAwait(false);
                foreach (Diagnostic diagnostic in diagnostics)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (diagnostic.Id != XmlDocumentationFormattingId
                        || diagnostic.Location.SourceTree is not { } tree
                        || project.GetDocument(tree) is not { } document)
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
            }

            Dictionary<DocumentId, ImmutableArray<DocumentId>> relatedDocuments =
                DocumentFileUtilities.IndexRelatedDocuments(
                    context.Solution,
                    LanguageNames.CSharp,
                    context.CancellationToken);
            HashSet<DocumentId> processedDocuments = [];
            Solution solution = context.Solution;
            foreach (KeyValuePair<DocumentId, List<Diagnostic>> pair in diagnosticsByDocument)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!processedDocuments.Add(pair.Key)
                    || !relatedDocuments.TryGetValue(pair.Key, out ImmutableArray<DocumentId> documentIds)
                    || context.Solution.GetDocument(pair.Key) is not { } document
                    || !TryCreateChanges(
                        pair.Value,
                        context.CancellationToken,
                        out List<TextChange> changes))
                {
                    continue;
                }

                if (documentIds.IsDefaultOrEmpty)
                {
                    continue;
                }

                foreach (DocumentId documentId in documentIds)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    processedDocuments.Add(documentId);
                }

                SourceText expectedSource = await document.GetTextAsync(context.CancellationToken)
                    .ConfigureAwait(false);
                bool compatible = true;
                foreach (DocumentId documentId in documentIds)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    Document? candidate = context.Solution.GetDocument(documentId);
                    if (candidate is null)
                    {
                        compatible = false;
                        break;
                    }

                    SourceText source = await candidate.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
                    if (!expectedSource.ContentEquals(source)
                        || !diagnosticsByDocument.TryGetValue(documentId, out List<Diagnostic>? candidateDiagnostics)
                        || !TryCreateChanges(
                            candidateDiagnostics,
                            context.CancellationToken,
                            out List<TextChange> candidateChanges)
                        || !ChangesAreEquivalent(
                            changes,
                            candidateChanges,
                            context.CancellationToken))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (!compatible)
                {
                    continue;
                }

                SourceText changedText = expectedSource.WithChanges(changes);
                if (expectedSource.ContentEquals(changedText))
                {
                    continue;
                }

                foreach (DocumentId documentId in documentIds)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    solution = solution.WithDocumentText(documentId, changedText);
                }
            }

            if (solution == context.Solution)
            {
                return null;
            }

            return CodeAction.Create(
                Title,
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(solution);
                },
                nameof(FormatXmlDocumentationCodeFixProvider));
        }

        private static async Task<bool> HasDocumentTextChangesAsync(
            Solution original,
            Solution changed,
            CancellationToken cancellationToken)
        {
            foreach (Project project in original.Projects)
            {
                foreach (Document document in project.Documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Document? changedDocument = changed.GetDocument(document.Id);
                    if (changedDocument is null)
                    {
                        return true;
                    }

                    SourceText originalText = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    SourceText changedText = await changedDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);
                    if (!originalText.ContentEquals(changedText))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}