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
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Replace tabs with spaces" fix for <c>TOUKI0022</c>.
/// </summary>
/// <remarks>
///  <para>
///   The spaces to substitute are computed by the analyzer, which is the only place that knows the
///   configured tab width, and travel on the diagnostic as a property. Each report is independent: a tab
///   expands to the same visual width whether or not the tabs before it on the line have been expanded yet,
///   so several fixes on one line compose without having to be applied in order.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReplaceTabsWithSpacesCodeFixProvider))]
[Shared]
public sealed class ReplaceTabsWithSpacesCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; these are a stable public contract.
    private const string NoTabsId = "TOUKI0022";
    private const string ReplacementProperty = "Replacement";

    // Cached: the host reads this repeatedly, and a fresh array per access is pure waste.
    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [NoTabsId];

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue(ReplacementProperty, out string? replacement)
                || replacement is null)
            {
                continue;
            }

            TextSpan span = diagnostic.Location.SourceSpan;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Replace tabs with spaces",
                    cancellationToken => ReplaceAsync(context.Document, span, replacement, cancellationToken),
                    equivalenceKey: nameof(ReplaceTabsWithSpacesCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        TextSpan span,
        string replacement,
        CancellationToken cancellationToken)
    {
        SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(new TextChange(span, replacement)));
    }
}
