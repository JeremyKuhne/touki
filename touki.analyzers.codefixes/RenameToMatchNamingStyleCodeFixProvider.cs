// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Rename;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Rename '{name}' to '{suggestion}'" fix for the naming diagnostic (<c>TOUKI0041</c>), replacing
///  the rename fix that IDE1006 provides.
/// </summary>
/// <remarks>
///  <para>
///   The suggested name is computed by the analyzer, which is the only place that knows which naming rule the
///   symbol violated, and travels on the diagnostic as a property.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RenameToMatchNamingStyleCodeFixProvider))]
[Shared]
public sealed class RenameToMatchNamingStyleCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; these are a stable public contract.
    private const string NamingStyleId = "TOUKI0041";
    private const string SuggestedNameProperty = "SuggestedName";

    // Cached: the host reads this repeatedly, and a fresh array per access is pure waste.
    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [NamingStyleId];

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <summary>
    ///  Returns <see langword="null"/>. A rename rewrites every reference across the solution, so applying
    ///  several at once would have them fight over the same documents.
    /// </summary>
    /// <returns><see langword="null"/>.</returns>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        if (await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false)
            is not { } semanticModel)
        {
            return;
        }

        if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false) is not { } root)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(SuggestedNameProperty, out string? suggestedName)
                || suggestedName is not { Length: > 0 })
            {
                continue;
            }

            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            if (semanticModel.GetDeclaredSymbol(node, context.CancellationToken) is not { } symbol
                || symbol.Name == suggestedName)
            {
                continue;
            }

            string title = $"Rename '{symbol.Name}' to '{suggestedName}'";

            context.RegisterCodeFix(
                CodeAction.Create(
                    title,
                    cancellationToken => RenameAsync(
                        context.Document,
                        symbol,
                        suggestedName,
                        cancellationToken),
                    equivalenceKey: $"{nameof(RenameToMatchNamingStyleCodeFixProvider)}:{suggestedName}"),
                diagnostic);
        }
    }

    private static async Task<Solution> RenameAsync(
        Document document,
        ISymbol symbol,
        string newName,
        CancellationToken cancellationToken)
    {
        Solution solution = document.Project.Solution;
        return await Renamer.RenameSymbolAsync(
            solution,
            symbol,
            new SymbolRenameOptions(),
            newName,
            cancellationToken).ConfigureAwait(false);
    }
}
