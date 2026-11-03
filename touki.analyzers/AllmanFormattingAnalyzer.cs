// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Requires Allman placement for brace-delimited C# constructs and configured blank-line separation.
/// </summary>
/// <remarks>
///  <para>
///   Every structural brace pair participates except interpolated-string delimiters and empty property patterns.
///   A complete construct may remain on one line when enabled and its header and body share a physical line
///   within the configured maximum. Declaration attributes do not count as part of the header.
///  </para>
///  <para>
///   Braced branches of an <c>if</c>/<c>else if</c>/<c>else</c> chain or a <c>try</c>/<c>catch</c>/<c>finally</c>
///   group expand together when any branch has a split header, is multiline, or exceeds the compact-line limit.
///  </para>
///  <para>
///   Before C# 11, interpolation holes are left unchanged because those language versions do not permit the
///   newlines that Allman formatting may introduce inside a non-verbatim interpolated string.
///  </para>
///  <para>
///   Optional spacing policies require a blank line after a brace-led closing line (including attached delimiters
///   and punctuation) only before content at the same indentation. Existing blank lines remain unchanged.
///   A separate policy spaces multiline, semicolon-terminated statements.
///  </para>
///  <para>
///   The <c>else</c>, <c>catch</c>, <c>finally</c>, and a <c>do</c> statement's <c>while</c> continuation
///   clauses remain adjacent to the preceding closing brace. Sibling accessor bodies also remain adjacent.
///   Preprocessor directives and inactive conditional text do not satisfy optional spacing.
///  </para>
///  <para>
///   Closing punctuation such as <c>};</c>, <c>},</c>, and <c>});</c> stays together before any required spacing.
///   Parameter/argument lists and initializer/collection entries are exempt from block-end spacing,
///   but executable statements within lambda arguments are not.
///  </para>
///  <para>
///   The rule ships disabled because source layout is a house style. Its formatting options default to enabled.
///  </para>
///  <para>
///   The diagnostic remains available when a brace rewrite would relocate a line containing <c>#</c>, or when
///   formatting is projected to add more than 4 MiB of replacement text, but the code fix is withheld.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AllmanFormattingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0027";

    /// <summary>
    ///  The option that spaces brace-led closing lines before content at the same indentation.
    /// </summary>
    public const string RequireBlankLineAfterClosingBraceOption =
        AllmanFormattingOptions.RequireBlankLineAfterClosingBraceOption;

    /// <summary>
    ///  The option that permits a complete brace-delimited construct to remain on one line when it fits.
    /// </summary>
    public const string AllowSingleLineBlocksOption =
        AllmanFormattingOptions.AllowSingleLineBlocksOption;

    /// <summary>
    ///  The option that requires a blank line after a semicolon-terminated statement spanning multiple lines.
    /// </summary>
    public const string RequireBlankLineAfterMultilineStatementOption =
        AllmanFormattingOptions.RequireBlankLineAfterMultilineStatementOption;

    /// <summary>
    ///  The option that overrides the maximum physical line length for an allowed single-line construct.
    /// </summary>
    public const string MaxLineLengthOption = AllmanFormattingOptions.MaxLineLengthOption;

    /// <summary>
    ///  The default maximum physical line length.
    /// </summary>
    public const int DefaultMaxLineLength = AllmanFormattingOptions.DefaultMaxLineLength;

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Use Allman formatting",
        messageFormat: "Use configured Allman formatting",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Brace-delimited constructs and surrounding statements should follow the configured Allman layout.",
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
        SourceText source = context.Tree.GetText(context.CancellationToken);
        SyntaxNode root = context.Tree.GetRoot(context.CancellationToken);
        AnalyzerConfigOptions config = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);
        AllmanFormattingOptions options = AllmanFormattingOptions.GetOptions(config);

        if (!AllmanFormatter.TryFindViolation(
            source,
            root,
            options,
            context.CancellationToken,
            out TextSpan firstViolation,
            out bool fixAvailable))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                s_rule,
                Location.Create(context.Tree, firstViolation),
                options.ToDiagnosticProperties(fixAvailable)));
    }

}