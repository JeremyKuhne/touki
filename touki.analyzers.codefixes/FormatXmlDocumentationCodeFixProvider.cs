// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;

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

            TextChangeCodeFix.Register(
                context,
                diagnostic,
                "Format XML documentation comment",
                replacement,
                nameof(FormatXmlDocumentationCodeFixProvider));
        }

        return Task.CompletedTask;
    }
}
