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
///  Offers a whole-document Allman formatting fix for <c>TOUKI0027</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormatAllmanCodeFixProvider))]
[Shared]
public sealed class FormatAllmanCodeFixProvider : CodeFixProvider
{
    private const string AllmanFormattingId = "TOUKI0027";
    private const string Title = "Format Allman style";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [AllmanFormattingId];
    private static readonly FixAllProvider s_fixAllProvider = new AllmanFixAllProvider();

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!AllmanFormattingOptions.TryGetDiagnosticOptions(
                diagnostic.Properties,
                out AllmanFormattingOptions options,
                out bool fixAvailable)
                || !fixAvailable)
            {
                continue;
            }

            CompatibleFormatting? formatting = await TryGetCompatibleFormattingAsync(
                context.Document,
                options,
                context.CancellationToken).ConfigureAwait(false);
            if (formatting is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    cancellationToken => Task.FromResult(ApplyFormatting(
                        context.Document.Project.Solution,
                        formatting.Value,
                        cancellationToken)),
                    nameof(FormatAllmanCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<CompatibleFormatting?> TryGetCompatibleFormattingAsync(
        Document document,
        AllmanFormattingOptions options,
        CancellationToken cancellationToken,
        ImmutableArray<DocumentId> relatedDocumentIds = default,
        IReadOnlyDictionary<DocumentId, AllmanFormattingOptions>? knownOptions = null)
    {
        if (relatedDocumentIds.IsDefault)
        {
            relatedDocumentIds = GetRelatedDocumentIds(document, cancellationToken);
        }

        SourceText? original = null;
        SourceText? compatible = null;
        foreach (DocumentId documentId in relatedDocumentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? candidate = document.Project.Solution.GetDocument(documentId);
            if (candidate is null)
            {
                return null;
            }

            SourceText source = await candidate.GetTextAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode? root = await candidate.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null || original is not null && !original.ContentEquals(source))
            {
                return null;
            }

            original ??= source;
            AllmanFormattingOptions candidateOptions;
            if (knownOptions is not null
                && knownOptions.TryGetValue(candidate.Id, out AllmanFormattingOptions knownOption))
            {
                candidateOptions = knownOption;
            }
            else if (candidate.Id == document.Id)
            {
                candidateOptions = options;
            }
            else
            {
                AnalyzerConfigOptions config = candidate.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider
                    .GetOptions(root.SyntaxTree);
                candidateOptions = AllmanFormattingOptions.GetOptions(config);
            }

            SourceText formatted;
            AllmanFormatter.TryFormat(
                source,
                root,
                candidateOptions,
                cancellationToken,
                out formatted,
                out _);
            if (compatible is not null && !compatible.ContentEquals(formatted))
            {
                return null;
            }

            compatible ??= formatted;
        }

        if (original is null || compatible is null || original.ContentEquals(compatible))
        {
            return null;
        }

        return new(
            compatible,
            relatedDocumentIds);
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
                        && string.Equals(candidate.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
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
        Dictionary<string, List<DocumentId>> documentsByPath = new(StringComparer.OrdinalIgnoreCase);
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

    private static Solution ApplyFormatting(
        Solution solution,
        CompatibleFormatting formatting,
        CancellationToken cancellationToken)
    {
        foreach (DocumentId documentId in formatting.DocumentIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            solution = solution.WithDocumentText(documentId, formatting.Text);
        }

        return solution;
    }

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

    private readonly struct CompatibleFormatting
    {
        public CompatibleFormatting(SourceText text, ImmutableArray<DocumentId> documentIds)
        {
            Text = text;
            DocumentIds = documentIds;
        }

        public SourceText Text { get; }

        public ImmutableArray<DocumentId> DocumentIds { get; }
    }
}