// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Touki.Analyzers;

public sealed partial class FormatAllmanCodeFixProvider
{
    /// <summary>
    ///  Applies Allman formatting fixes across the requested Fix All scope.
    /// </summary>
    private sealed class AllmanFixAllProvider : FixAllProvider
    {
        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            switch (fixAllContext.Scope)
            {
                case FixAllScope.Document when fixAllContext.Document is not null:
                    diagnostics.AddRange(
                        await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document).ConfigureAwait(false));
                    break;
                case FixAllScope.Project:
                    diagnostics.AddRange(
                        await fixAllContext.GetAllDiagnosticsAsync(fixAllContext.Project).ConfigureAwait(false));
                    break;
                case FixAllScope.Solution:
                    foreach (Project project in fixAllContext.Solution.Projects)
                    {
                        diagnostics.AddRange(
                            await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false));
                    }

                    break;
            }

            Solution solution = fixAllContext.Solution;
            Dictionary<DocumentId, AllmanFormattingOptions> knownOptions = [];
            foreach (Diagnostic diagnostic in diagnostics)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                if (diagnostic.Id == AllmanFormattingId
                    && diagnostic.Location.SourceTree is not null
                    && AllmanFormattingOptions.TryGetDiagnosticOptions(
                        diagnostic.Properties,
                        out AllmanFormattingOptions options,
                        out _)
                    && fixAllContext.Solution.GetDocument(diagnostic.Location.SourceTree) is { } document)
                {
                    knownOptions[document.Id] = options;
                }
            }

            Dictionary<DocumentId, ImmutableArray<DocumentId>> relatedDocuments =
                IndexRelatedDocuments(fixAllContext.Solution, fixAllContext.CancellationToken);
            HashSet<DocumentId> formattedDocuments = [];
            foreach (Diagnostic diagnostic in diagnostics)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                if (diagnostic.Id != AllmanFormattingId
                    || diagnostic.Location.SourceTree is null
                    || !AllmanFormattingOptions.TryGetDiagnosticOptions(
                        diagnostic.Properties,
                        out AllmanFormattingOptions options,
                        out bool fixAvailable)
                    || !fixAvailable
                    || fixAllContext.Solution.GetDocument(diagnostic.Location.SourceTree) is not { } originalDocument
                    || !formattedDocuments.Add(originalDocument.Id)
                    || solution.GetDocument(originalDocument.Id) is not { } document)
                {
                    continue;
                }

                CompatibleFormatting? formatting = await TryGetCompatibleFormattingAsync(
                    document,
                    options,
                    fixAllContext.CancellationToken,
                    relatedDocuments[document.Id],
                    knownOptions).ConfigureAwait(false);
                if (formatting is null)
                {
                    continue;
                }

                foreach (DocumentId documentId in formatting.Value.DocumentIds)
                {
                    formattedDocuments.Add(documentId);
                }

                solution = ApplyFormatting(solution, formatting.Value, fixAllContext.CancellationToken);
            }

            if (solution == fixAllContext.Solution)
            {
                return null;
            }

            return CodeAction.Create(
                Title,
                _ => Task.FromResult(solution),
                nameof(FormatAllmanCodeFixProvider));
        }
    }
}