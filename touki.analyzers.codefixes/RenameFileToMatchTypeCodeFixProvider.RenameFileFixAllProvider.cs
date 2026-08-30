// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Touki.Analyzers;

public sealed partial class RenameFileToMatchTypeCodeFixProvider
{
    /// <summary>
    ///  Renames all eligible source documents in the requested Fix All scope.
    /// </summary>
    private sealed class RenameFileFixAllProvider : FixAllProvider
    {
        public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (fixAllContext.Solution.Workspace.Kind == WorkspaceKind.MSBuild)
            {
                return Task.FromResult<CodeAction?>(null);
            }

            CodeAction action = CodeAction.Create(
                "Rename files to match their types",
                cancellationToken => RenameAllAsync(fixAllContext, cancellationToken),
                EquivalenceKey);
            return Task.FromResult<CodeAction?>(action);
        }

        private static async Task<Solution> RenameAllAsync(
            FixAllContext context,
            CancellationToken cancellationToken)
        {
            ImmutableArray<Diagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            switch (context.Scope)
            {
                case FixAllScope.Document when context.Document is not null:
                    diagnostics.AddRange(await context.GetDocumentDiagnosticsAsync(context.Document).ConfigureAwait(false));
                    break;
                case FixAllScope.Project:
                    diagnostics.AddRange(await context.GetAllDiagnosticsAsync(context.Project).ConfigureAwait(false));
                    break;
                case FixAllScope.Solution:
                    foreach (Project project in context.Solution.Projects)
                    {
                        diagnostics.AddRange(await context.GetAllDiagnosticsAsync(project).ConfigureAwait(false));
                    }

                    break;
            }

            List<RenameRequest> requests = [];
            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Location.SourceTree is null
                    || !TryGetSuggestion(
                        diagnostic,
                        out string suggestedFileName,
                        out char detailSeparator))
                {
                    continue;
                }

                Document? document = context.Solution.GetDocument(diagnostic.Location.SourceTree);
                if (document?.FilePath is null)
                {
                    continue;
                }

                requests.Add(new(document.Id, document.FilePath, suggestedFileName, detailSeparator));
            }

            requests.Sort(static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.OriginalFilePath, right.OriginalFilePath));

            Solution solution = context.Solution;
            foreach (RenameRequest request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Document? document = solution.GetDocument(request.DocumentId);
                if (document is null || !CanRename(document))
                {
                    continue;
                }

                string availableFileName = GetAvailableFileName(
                    solution,
                    document,
                    request.SuggestedFileName,
                    request.DetailSeparator);
                solution = await RenameDocumentAsync(
                    solution,
                    document,
                    availableFileName,
                    cancellationToken).ConfigureAwait(false);
            }

            return solution;
        }
    }
}