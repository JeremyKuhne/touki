// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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
public sealed partial class FormatAllmanCodeFixProvider : CodeFixProvider
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
            relatedDocumentIds = DocumentFileUtilities.GetRelatedDocumentIds(document, cancellationToken);
        }

        if (relatedDocumentIds.IsDefaultOrEmpty)
        {
            return null;
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

}