// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Format XML documentation comment" fix for <c>TOUKI0024</c>.
/// </summary>
/// <remarks>
///  <para>
///   The analyzer computes the complete replacement because it owns the per-file indentation and line-length
///   configuration. The fix applies that replacement without reinterpreting the documentation XML.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FormatXmlDocumentationCodeFixProvider))]
[Shared]
public sealed class FormatXmlDocumentationCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; these are stable public contracts.
    private const string XmlDocumentationFormattingId = "TOUKI0024";
    private const string ReplacementProperty = "Replacement";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [XmlDocumentationFormattingId];
    private static readonly FixAllProvider s_fixAllProvider = FixAllProvider.Create(
        FixAllAsync,
        [
            FixAllScope.Document,
            FixAllScope.Project,
            FixAllScope.Solution,
            FixAllScope.ContainingMember,
            FixAllScope.ContainingType
        ]);

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

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

            TextChangeCodeFix.Register(
                context,
                diagnostic,
                "Format XML documentation comment",
                replacement,
                nameof(FormatXmlDocumentationCodeFixProvider));
        }

        return Task.CompletedTask;
    }

    private static async Task<Document?> FixAllAsync(
        FixAllContext context,
        Document document,
        ImmutableArray<Diagnostic> diagnostics)
    {
        List<TextChange> changes = new(diagnostics.Length);
        foreach (Diagnostic diagnostic in diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (diagnostic.Id == XmlDocumentationFormattingId
                && diagnostic.Properties.TryGetValue(ReplacementProperty, out string? replacement)
                && replacement is not null)
            {
                changes.Add(new(diagnostic.Location.SourceSpan, replacement));
            }
        }

        if (changes.Count == 0)
        {
            return null;
        }

        changes.Sort(static (left, right) => left.Span.Start.CompareTo(right.Span.Start));
        SourceText text = await document.GetTextAsync(context.CancellationToken).ConfigureAwait(false);
        return document.WithText(text.WithChanges(changes));
    }
}
