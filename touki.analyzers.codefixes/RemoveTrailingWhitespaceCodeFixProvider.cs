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
///  Offers a "Remove trailing whitespace" fix for <c>TOUKI0023</c>.
/// </summary>
/// <remarks>
///  <para>
///   The diagnostic covers exactly the whitespace to delete, so the fix is a deletion of the reported span.
///   The analyzer has already excluded whitespace whose bytes are part of the program.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RemoveTrailingWhitespaceCodeFixProvider))]
[Shared]
public sealed class RemoveTrailingWhitespaceCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; this is a stable public contract.
    private const string TrailingWhitespaceId = "TOUKI0023";

    // Cached: the host reads this repeatedly, and a fresh array per access is pure waste.
    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [TrailingWhitespaceId];

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            TextSpan span = diagnostic.Location.SourceSpan;

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Remove trailing whitespace",
                    cancellationToken => RemoveAsync(context.Document, span, cancellationToken),
                    equivalenceKey: nameof(RemoveTrailingWhitespaceCodeFixProvider)),
                diagnostic);
        }

        return Task.CompletedTask;
    }

    private static async Task<Document> RemoveAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        SourceText text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(new TextChange(span, string.Empty)));
    }
}
