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
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Touki.Analyzers;

public sealed partial class MoveTypeToFileCodeFixProvider
{
    /// <summary>
    ///  Moves all eligible extra type declarations in the requested Fix All scope.
    /// </summary>
    private sealed class MoveTypeFixAllProvider : FixAllProvider
    {
        public override Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (fixAllContext.Solution.Workspace.Kind == WorkspaceKind.MSBuild)
            {
                return Task.FromResult<CodeAction?>(null);
            }

            CodeAction action = CodeAction.Create(
                "Move types to separate files",
                cancellationToken => MoveAllAsync(fixAllContext, cancellationToken),
                EquivalenceKey);
            return Task.FromResult<CodeAction?>(action);
        }

        private static async Task<Solution> MoveAllAsync(
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

            Dictionary<DocumentId, CompilationUnitSyntax> annotatedRoots = [];
            List<MoveRequest> requests = [];

            foreach (Diagnostic diagnostic in diagnostics)
            {
                if (diagnostic.Location.SourceTree is null)
                {
                    continue;
                }

                Document? document = context.Solution.GetDocument(diagnostic.Location.SourceTree);
                if (document?.FilePath is null)
                {
                    continue;
                }

                if (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
                    is not CompilationUnitSyntax documentRoot)
                {
                    continue;
                }

                MemberDeclarationSyntax? originalDeclaration =
                    FindDeclaration(documentRoot, diagnostic.Location.SourceSpan.Start);
                if (originalDeclaration is null
                    || !await CanMoveAsync(
                        document,
                        documentRoot,
                        originalDeclaration,
                        cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                CompilationUnitSyntax root = annotatedRoots.TryGetValue(document.Id, out CompilationUnitSyntax? annotatedRoot)
                    ? annotatedRoot
                    : documentRoot;
                MemberDeclarationSyntax? declaration = FindDeclaration(root, diagnostic.Location.SourceSpan.Start);
                if (declaration is null)
                {
                    continue;
                }

                SyntaxAnnotation annotation = new();
                root = root.ReplaceNode(declaration, declaration.WithAdditionalAnnotations(annotation));
                annotatedRoots[document.Id] = root;
                requests.Add(new(
                    document.Id,
                    document.FilePath,
                    annotation,
                    GetNestingDepth(originalDeclaration),
                    diagnostic.Location.SourceSpan.Start));
            }

            requests.Sort(static (left, right) =>
            {
                int pathComparison = StringComparer.OrdinalIgnoreCase.Compare(
                    left.OriginalFilePath,
                    right.OriginalFilePath);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }

                int depthComparison = right.NestingDepth.CompareTo(left.NestingDepth);
                return depthComparison != 0
                    ? depthComparison
                    : left.SourcePosition.CompareTo(right.SourcePosition);
            });

            Solution solution = context.Solution;
            foreach (KeyValuePair<DocumentId, CompilationUnitSyntax> annotatedRoot in annotatedRoots)
            {
                solution = solution.WithDocumentSyntaxRoot(annotatedRoot.Key, annotatedRoot.Value);
            }

            foreach (MoveRequest request in requests)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Document? document = solution.GetDocument(request.DocumentId);
                if (document is null
                    || await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
                        is not CompilationUnitSyntax root)
                {
                    continue;
                }

                MemberDeclarationSyntax? declaration = GetAnnotatedDeclaration(root, request.Annotation);
                if (declaration is null
                    || !await CanMoveAsync(
                        document,
                        root,
                        declaration,
                        cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                char detailSeparator = GetDetailSeparator(document, root.SyntaxTree);
                string fileName = GetAvailableFileName(solution, document, declaration, detailSeparator);
                solution = await MoveAsync(document, declaration, fileName, cancellationToken).ConfigureAwait(false);
            }

            return solution;
        }

        private static int GetNestingDepth(MemberDeclarationSyntax declaration)
        {
            int depth = 0;

            foreach (SyntaxNode ancestor in declaration.Ancestors())
            {
                if (ancestor is TypeDeclarationSyntax)
                {
                    depth++;
                }
            }

            return depth;
        }
    }
}