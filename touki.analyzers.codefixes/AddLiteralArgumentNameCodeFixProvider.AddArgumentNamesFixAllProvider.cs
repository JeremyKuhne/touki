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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

public sealed partial class AddLiteralArgumentNameCodeFixProvider
{
    private sealed class AddArgumentNamesFixAllProvider : FixAllProvider
    {
        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            if (fixAllContext.CodeActionEquivalenceKey != nameof(AddLiteralArgumentNameCodeFixProvider))
            {
                return null;
            }

            ImmutableArray<Diagnostic> diagnostics = await GetDiagnosticsAsync(fixAllContext).ConfigureAwait(false);
            if (diagnostics.IsEmpty)
            {
                return null;
            }

            HashSet<DocumentId> sharedDocuments = IndexSharedDocuments(
                fixAllContext.Solution,
                fixAllContext.CancellationToken);
            Dictionary<DocumentId, List<Diagnostic>> diagnosticsByDocument = [];
            foreach (Diagnostic diagnostic in diagnostics)
            {
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                if (diagnostic.Id != RequireNamedArgumentsForLiteralsId
                    || diagnostic.Location.SourceTree is null
                    || fixAllContext.Solution.GetDocument(diagnostic.Location.SourceTree) is not { } document
                    || sharedDocuments.Contains(document.Id))
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

            Solution solution = fixAllContext.Solution;
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

                List<ArgumentNameCandidate> candidates = new(pair.Value.Count);
                HashSet<int> insertionPoints = [];
                foreach (Diagnostic diagnostic in pair.Value)
                {
                    fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                    if (TryCreateCandidate(
                        diagnostic,
                        root,
                        semanticModel,
                        fixAllContext.CancellationToken,
                        out ArgumentNameCandidate candidate)
                        && insertionPoints.Add(candidate.Change.Span.Start))
                    {
                        candidates.Add(candidate);
                    }
                }

                HashSet<int> eligibleInsertionPoints = GetEligibleInsertionPoints(
                    candidates,
                    insertionPoints,
                    fixAllContext.CancellationToken);
                List<TextChange> changes = new(eligibleInsertionPoints.Count);
                foreach (ArgumentNameCandidate candidate in candidates)
                {
                    fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                    if (eligibleInsertionPoints.Contains(candidate.Change.Span.Start))
                    {
                        changes.Add(candidate.Change);
                    }
                }

                if (changes.Count == 0)
                {
                    continue;
                }

                Comparison<TextChange> comparison = (left, right) =>
                {
                    fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                    return left.Span.Start.CompareTo(right.Span.Start);
                };
                try
                {
                    changes.Sort(comparison);
                }
                catch (InvalidOperationException exception)
                    when (exception.InnerException is OperationCanceledException
                        && fixAllContext.CancellationToken.IsCancellationRequested)
                {
                    fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                    throw;
                }

                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                SourceText text = await document.GetTextAsync(fixAllContext.CancellationToken).ConfigureAwait(false);
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
                solution = solution.WithDocumentText(document.Id, text.WithChanges(changes));
                fixAllContext.CancellationToken.ThrowIfCancellationRequested();
            }

            if (solution == fixAllContext.Solution)
            {
                return null;
            }

            return CodeAction.Create(
                "Add argument names",
                _ => Task.FromResult(solution),
                nameof(AddLiteralArgumentNameCodeFixProvider));
        }

        private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(FixAllContext context)
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
                        context.CancellationToken.ThrowIfCancellationRequested();
                        diagnostics.AddRange(await context.GetAllDiagnosticsAsync(project).ConfigureAwait(false));
                    }

                    break;
            }

            return diagnostics.ToImmutable();
        }

        private static HashSet<int> GetEligibleInsertionPoints(
            List<ArgumentNameCandidate> candidates,
            HashSet<int> insertionPoints,
            CancellationToken cancellationToken)
        {
            HashSet<int> eligible = [];
            if (candidates.Count == 0)
            {
                return eligible;
            }

            if (candidates[0].Argument.SyntaxTree.Options is not CSharpParseOptions parseOptions
                || parseOptions.LanguageVersion >= LanguageVersion.CSharp7_2)
            {
                foreach (int insertionPoint in insertionPoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    eligible.Add(insertionPoint);
                }

                return eligible;
            }

            HashSet<SyntaxNode> argumentLists = [];
            foreach (ArgumentNameCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidate.Argument.Parent is { } argumentList)
                {
                    argumentLists.Add(argumentList);
                }
            }

            foreach (SyntaxNode argumentList in argumentLists)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (argumentList)
                {
                    case BaseArgumentListSyntax ordinary:
                        AddEligibleOrdinaryArguments(ordinary, insertionPoints, eligible, cancellationToken);
                        break;
                    case AttributeArgumentListSyntax attribute:
                        AddEligibleAttributeArguments(attribute, insertionPoints, eligible, cancellationToken);
                        break;
                }
            }

            return eligible;
        }

        private static void AddEligibleOrdinaryArguments(
            BaseArgumentListSyntax argumentList,
            HashSet<int> insertionPoints,
            HashSet<int> eligible,
            CancellationToken cancellationToken)
        {
            bool canName = true;
            for (int index = argumentList.Arguments.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ArgumentSyntax argument = argumentList.Arguments[index];
                if (argument.NameColon is not null)
                {
                    continue;
                }

                int insertionPoint = argument.Expression.SpanStart;
                if (canName && insertionPoints.Contains(insertionPoint))
                {
                    eligible.Add(insertionPoint);
                }
                else
                {
                    canName = false;
                }
            }
        }

        private static void AddEligibleAttributeArguments(
            AttributeArgumentListSyntax argumentList,
            HashSet<int> insertionPoints,
            HashSet<int> eligible,
            CancellationToken cancellationToken)
        {
            bool canName = true;
            for (int index = argumentList.Arguments.Count - 1; index >= 0; index--)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AttributeArgumentSyntax argument = argumentList.Arguments[index];
                if (argument.NameColon is not null || argument.NameEquals is not null)
                {
                    continue;
                }

                int insertionPoint = argument.Expression.SpanStart;
                if (canName && insertionPoints.Contains(insertionPoint))
                {
                    eligible.Add(insertionPoint);
                }
                else
                {
                    canName = false;
                }
            }
        }

        private static HashSet<DocumentId> IndexSharedDocuments(Solution solution, CancellationToken cancellationToken)
            => DocumentFileUtilities.IndexSharedDocuments(
                solution,
                cancellationToken);
    }
}