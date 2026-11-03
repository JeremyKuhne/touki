// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Reports single-line XML documentation comments that do not follow the configured spacing and nested XML layout.
/// </summary>
/// <remarks>
///  <para>
///   Documentation comments are separated from preceding code by a blank line unless they immediately follow an
///   opening brace that starts a block or a preprocessor directive.
///  </para>
///  <para>
///   The rule formats paired XML elements as blocks, with one configurable indentation step for every XML
///   nesting level. A top-level element other than <c>summary</c> may stay on one line when it fits within the
///   configured maximum line length. A three-line element with exactly one content line is compacted when it
///   fits; elements with two or more content lines retain those line breaks.
///  </para>
///  <para>
///   Indentation is evaluated per contiguous logical block. A correctly indented first line preserves the
///   block's relative indentation; otherwise every line in the block is shifted by the same amount.
///  </para>
///  <para>
///   Unicode whitespace is documentation content rather than layout, and effective
///   <c>xml:space="preserve"</c> regions are not rewritten.
///  </para>
///  <para>
///   The rule ships disabled because documentation layout is a house style. Enable it with
///   <c>dotnet_diagnostic.TOUKI0024.severity = warning</c>.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class XmlDocumentationFormattingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0024";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls the spaces added for each nested XML level.
    /// </summary>
    public const string IndentSizeOption = XmlDocumentationFormattingOptions.IndentSizeOption;

    /// <summary>
    ///  The <c>.editorconfig</c> key that overrides the maximum physical line length.
    /// </summary>
    public const string MaxLineLengthOption = XmlDocumentationFormattingOptions.MaxLineLengthOption;

    /// <summary>
    ///  The indentation added for each nested XML level when <see cref="IndentSizeOption"/> is not configured.
    /// </summary>
    public const int DefaultIndentSize = XmlDocumentationFormattingOptions.DefaultIndentSize;

    /// <summary>
    ///  The largest accepted XML indentation step. Larger configured values use
    ///  <see cref="DefaultIndentSize"/>.
    /// </summary>
    public const int MaximumIndentSize = XmlDocumentationFormattingOptions.MaximumIndentSize;

    /// <summary>
    ///  The maximum physical line length when neither <see cref="MaxLineLengthOption"/> nor the standard
    ///  <c>max_line_length</c> key supplies one.
    /// </summary>
    public const int DefaultMaxLineLength = XmlDocumentationFormattingOptions.DefaultMaxLineLength;

    /// <summary>
    ///  The diagnostic property carrying the complete replacement documentation comment.
    /// </summary>
    internal const string ReplacementProperty = "Replacement";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Format XML documentation",
        messageFormat: "Format XML documentation",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "XML documentation should be separated from preceding code and use a consistent nested layout "
            + "while preserving intentional prose line breaks.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);
        foreach (TextChange change in XmlDocumentationFormatting.GetChanges(context.Tree, options, context.CancellationToken))
        {
            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(ReplacementProperty, change.NewText);
            context.ReportDiagnostic(Diagnostic.Create(s_rule, Location.Create(context.Tree, change.Span), properties));
        }
    }
}
