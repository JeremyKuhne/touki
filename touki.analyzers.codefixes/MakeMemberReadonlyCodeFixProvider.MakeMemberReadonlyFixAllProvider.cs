// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class MakeMemberReadonlyCodeFixProvider
{
    private sealed class MakeMemberReadonlyFixAllProvider : FixAllProvider
    {
        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (fixAllContext.CodeActionEquivalenceKey != EquivalenceKey)
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
            Dictionary<DocumentId, HashSet<TextSpan>> declarationsByDocument = [];
            HashSet<ISymbol> processedReboundMembers = new(SymbolEqualityComparer.Default);
            HashSet<ISymbol> processedMembers = new(SymbolEqualityComparer.Default);
            foreach (KeyValuePair<DocumentId, List<Diagnostic>> pair in diagnosticsByDocument)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                Document? document = fixAllContext.Solution.GetDocument(pair.Key);
                if (document is null)
                {
                    continue;
                }

                SyntaxNode? root = await document.GetSyntaxRootAsync(fixAllContext.CancellationToken)
                    .ConfigureAwait(false);
                SemanticModel? semanticModel = await document.GetSemanticModelAsync(fixAllContext.CancellationToken)
                    .ConfigureAwait(false);
                if (root is null || semanticModel is null)
                {
                    continue;
                }

                foreach (Diagnostic diagnostic in pair.Value)
                {
                    fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                    SyntaxNode node = root.FindNode(
                        diagnostic.Location.SourceSpan,
                        getInnermostNodeForTie: true);
                    ISymbol? member = TryGetAccessedMember(
                        node,
                        diagnostic.Location.SourceSpan,
                        semanticModel,
                        fixAllContext.CancellationToken);
                    if (member is null
                        || !processedReboundMembers.Add(member.OriginalDefinition)
                        || await ResolveSourceMemberAsync(
                            fixAllContext.Solution,
                            member,
                            fixAllContext.CancellationToken).ConfigureAwait(false) is not { } sourceMember
                        || !processedMembers.Add(sourceMember))
                    {
                        continue;
                    }

                    TryCollectDeclarations(
                        fixAllContext.Solution,
                        sourceMember,
                        sharedDocuments,
                        declarationsByDocument,
                        fixAllContext.CancellationToken);
                }
            }

            if (declarationsByDocument.Count == 0)
            {
                return null;
            }

            Solution solution = await MakeMembersReadonlyAsync(
                fixAllContext.Solution,
                declarationsByDocument,
                fixAllContext.CancellationToken).ConfigureAwait(false);
            if (solution == fixAllContext.Solution)
            {
                return null;
            }

            return CodeAction.Create(
                "Make members readonly",
                _ => Task.FromResult(solution),
                EquivalenceKey);
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
            if (!s_fixableDiagnosticIds.Contains(diagnostic.Id))
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
    }
}
