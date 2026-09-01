// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class UseTextWriterWriteFormattedCodeFixProvider
{
    private sealed class WriteFormattedFixAllProvider : FixAllProvider
    {
        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (fixAllContext.CodeActionEquivalenceKey != nameof(UseTextWriterWriteFormattedCodeFixProvider))
            {
                return null;
            }

            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument =
                await GetDiagnosticsByDocumentAsync(fixAllContext).ConfigureAwait(false);
            if (diagnosticsByDocument.Count == 0)
            {
                return null;
            }

            HashSet<DocumentId> sharedDocuments = IndexSharedDocuments(
                fixAllContext.Solution,
                fixAllContext.CancellationToken);
            Solution solution = fixAllContext.Solution;
            foreach (KeyValuePair<DocumentId, List<Diagnostic>> pair in diagnosticsByDocument)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                if (sharedDocuments.Contains(pair.Key)
                    || fixAllContext.Solution.GetDocument(pair.Key) is not { } document)
                {
                    continue;
                }

                SyntaxNode? root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken)
                    .ConfigureAwait(false);
                if (root is null
                    || root.SyntaxTree.Options is not CSharpParseOptions parseOptions
                    || parseOptions.LanguageVersion < LanguageVersion.CSharp10)
                {
                    continue;
                }

                HashSet<TextSpan> invocationSpans = [];
                foreach (Diagnostic diagnostic in pair.Value)
                {
                    fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                    if (diagnostic.Location.SourceTree == root.SyntaxTree
                        && FindInvocation(root, diagnostic.Location.SourceSpan) is { } invocation)
                    {
                        invocationSpans.Add(invocation.Span);
                    }
                }

                if (invocationSpans.Count == 0
                    || await TryUseWriteFormattedAsync(
                        document,
                        invocationSpans,
                        fixAllContext.CancellationToken).ConfigureAwait(false) is not { } changedDocument)
                {
                    continue;
                }

                SourceText changedText = await changedDocument.GetTextAsync(fixAllContext.CancellationToken)
                    .ConfigureAwait(false);
                solution = solution.WithDocumentText(document.Id, changedText);
            }

            if (solution == fixAllContext.Solution)
            {
                return null;
            }

            return CodeAction.Create(
                "Use WriteFormatted",
                cancellationToken =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return Task.FromResult(solution);
                },
                nameof(UseTextWriterWriteFormattedCodeFixProvider));
        }

        private static async Task<Dictionary<DocumentId, List<Diagnostic>>> GetDiagnosticsByDocumentAsync(
            FixAllContext context)
        {
            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument = [];
            switch (context.Scope)
            {
                case FixAllScope.Document when context.Document is not null:
                    AddDiagnostics(
                        context.Document,
                        await context.GetDocumentDiagnosticsAsync(context.Document).ConfigureAwait(false),
                        diagnosticsByDocument,
                        context.CancellationToken);
                    break;
                case FixAllScope.Project:
                    AddDiagnostics(
                        context.Project,
                        await context.GetAllDiagnosticsAsync(context.Project).ConfigureAwait(false),
                        diagnosticsByDocument,
                        context.CancellationToken);
                    break;
                case FixAllScope.Solution:
                    foreach (Project project in context.Solution.Projects)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();
                        AddDiagnostics(
                            project,
                            await context.GetAllDiagnosticsAsync(project).ConfigureAwait(false),
                            diagnosticsByDocument,
                            context.CancellationToken);
                    }

                    break;
            }

            return diagnosticsByDocument;
        }

        private static void AddDiagnostics(
            Document document,
            IEnumerable<Diagnostic> diagnostics,
            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument,
            CancellationToken cancellationToken)
        {
            foreach (Diagnostic diagnostic in diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddDiagnostic(document, diagnostic, diagnosticsByDocument);
            }
        }

        private static void AddDiagnostics(
            Project project,
            IEnumerable<Diagnostic> diagnostics,
            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument,
            CancellationToken cancellationToken)
        {
            foreach (Diagnostic diagnostic in diagnostics)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (diagnostic.Location.SourceTree is { } tree
                    && project.GetDocument(tree) is { } document)
                {
                    AddDiagnostic(document, diagnostic, diagnosticsByDocument);
                }
            }
        }

        private static void AddDiagnostic(
            Document document,
            Diagnostic diagnostic,
            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument)
        {
            if (diagnostic.Id != UseTextWriterWriteFormattedId)
            {
                return;
            }

            if (!diagnosticsByDocument.TryGetValue(document.Id, out List<Diagnostic>? documentDiagnostics))
            {
                documentDiagnostics = [];
                diagnosticsByDocument.Add(document.Id, documentDiagnostics);
            }

            documentDiagnostics.Add(diagnostic);
        }

        private static HashSet<DocumentId> IndexSharedDocuments(
            Solution solution,
            CancellationToken cancellationToken)
        {
            Dictionary<string, DocumentId> documentsByPath = new(StringComparer.OrdinalIgnoreCase);
            HashSet<DocumentId> sharedDocuments = [];
            foreach (Project project in solution.Projects)
            {
                foreach (Document document in project.Documents)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (document.FilePath is null)
                    {
                        continue;
                    }

                    if (documentsByPath.TryGetValue(document.FilePath, out DocumentId? relatedDocumentId))
                    {
                        sharedDocuments.Add(relatedDocumentId);
                        sharedDocuments.Add(document.Id);
                    }
                    else
                    {
                        documentsByPath.Add(document.FilePath, document.Id);
                    }
                }
            }

            return sharedDocuments;
        }
    }
}