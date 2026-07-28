// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Reports tab characters in source, so that a file reads the same width in every editor.
/// </summary>
/// <remarks>
///  <para>
///   A tab renders at whatever width the reader's editor is configured for, so a file mixing tabs and spaces
///   lines up for its author and not for anyone else. This rule ships disabled, because indentation is a
///   house style; a project asks for it:
///   <code>dotnet_diagnostic.TOUKI0022.severity = warning</code>
///  </para>
///  <para>
///   The width a tab expands to is read per path, so a repository can set one width for most files and
///   another for C#. The first of these that is present and parses as a positive integer wins:
///   <see cref="SpacesPerTabOption"/>, then the standard <c>tab_width</c>, then the standard
///   <c>indent_size</c>, and finally <see cref="DefaultSpacesPerTab"/>.
///  </para>
///  <para>
///   Expansion is to the next tab stop rather than to a fixed count, so a tab used to align something
///   mid-line keeps its alignment. Text whose exact bytes are part of the program - string and character
///   literals, and conditionally excluded text - is never reported. See
///   <see cref="WhitespaceAnalysis.GetProtectedSpans"/>.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoTabsAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0022";

    /// <summary>
    ///  The <c>.editorconfig</c> key that overrides the width a tab expands to. Takes precedence over the
    ///  standard <c>tab_width</c> and <c>indent_size</c> keys.
    /// </summary>
    public const string SpacesPerTabOption = "dotnet_code_quality.TOUKI0022.spaces_per_tab";

    /// <summary>
    ///  The width a tab expands to when neither <see cref="SpacesPerTabOption"/> nor the standard
    ///  <c>tab_width</c> or <c>indent_size</c> keys supply one.
    /// </summary>
    public const int DefaultSpacesPerTab = 4;

    /// <summary>
    ///  The diagnostic property carrying the spaces the code fix should substitute for the reported tabs.
    /// </summary>
    internal const string ReplacementProperty = "Replacement";

    // The standard EditorConfig properties, in the order they are consulted. tab_width is the width of a
    // tab; indent_size is EditorConfig's documented fallback for it.
    private const string TabWidthOption = "tab_width";
    private const string IndentSizeOption = "indent_size";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Avoid tab characters",
        messageFormat: "Replace tabs with spaces",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "A tab renders at a width the reader chooses, so a file containing tabs does not line up the same way for everyone. Expand tabs to spaces.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    // Cache the supported-diagnostics array so the property does not allocate a new array on every access.
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // The rule is about the file's raw text, which is what a syntax tree action is handed.
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        SourceText text = context.Tree.GetText(context.CancellationToken);

        // Cheapest possible filter: most files contain no tab at all, and those cost one scan and nothing
        // else - no tree walk, no option lookup.
        List<TextSpan> runs = [];
        CollectTabRuns(text, context.CancellationToken, runs);

        if (runs.Count == 0)
        {
            return;
        }

        ImmutableArray<TextSpan> protectedSpans =
            WhitespaceAnalysis.GetProtectedSpans(context.Tree.GetRoot(context.CancellationToken), context.CancellationToken);

        int tabWidth = GetSpacesPerTab(context);

        foreach (TextSpan run in runs)
        {
            if (WhitespaceAnalysis.IsProtected(protectedSpans, run))
            {
                continue;
            }

            ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                .Add(ReplacementProperty, ExpandRun(text, run, tabWidth));

            context.ReportDiagnostic(
                Diagnostic.Create(s_rule, Location.Create(context.Tree, run), properties));
        }
    }

    /// <summary>
    ///  Adds the span of every maximal run of tab characters in <paramref name="text"/> to
    ///  <paramref name="runs"/>.
    /// </summary>
    private static void CollectTabRuns(SourceText text, CancellationToken cancellationToken, List<TextSpan> runs)
    {
        int length = text.Length;
        int index = 0;

        while (index < length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (text[index] != '\t')
            {
                index++;
                continue;
            }

            int start = index;
            while (index < length && text[index] == '\t')
            {
                index++;
            }

            runs.Add(TextSpan.FromBounds(start, index));
        }
    }

    /// <summary>
    ///  Returns the spaces that <paramref name="run"/> expands to, advancing to the next multiple of
    ///  <paramref name="tabWidth"/> for each tab.
    /// </summary>
    private static string ExpandRun(SourceText text, TextSpan run, int tabWidth)
    {
        TextLine line = text.Lines.GetLineFromPosition(run.Start);
        int column = WhitespaceAnalysis.GetVisualColumn(text, line, run.Start, tabWidth);

        int spaces = 0;
        for (int tab = 0; tab < run.Length; tab++)
        {
            int advance = tabWidth - (column % tabWidth);
            column += advance;
            spaces += advance;
        }

        return new string(' ', spaces);
    }

    private static int GetSpacesPerTab(SyntaxTreeAnalysisContext context)
    {
        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);

        // The rule-specific key wins so a project can decouple this rule from its editor settings; the
        // standard keys are consulted next so the common case needs no new configuration at all.
        return TryGetPositiveInteger(options, SpacesPerTabOption, out int width)
            || TryGetPositiveInteger(options, TabWidthOption, out width)
            || TryGetPositiveInteger(options, IndentSizeOption, out width)
            ? width
            : DefaultSpacesPerTab;
    }

    /// <summary>
    ///  Reads <paramref name="key"/> as a positive integer. A missing, unparsable, or non-positive value
    ///  yields <see langword="false"/> so the next source is consulted rather than failing the build over an
    ///  editor setting this rule does not own. <c>indent_size = tab</c> is the common non-numeric value.
    /// </summary>
    private static bool TryGetPositiveInteger(AnalyzerConfigOptions options, string key, out int value)
    {
        if (options.TryGetValue(key, out string? raw)
            && int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value > 0)
        {
            return true;
        }

        value = 0;
        return false;
    }
}
