// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Reports whitespace between the last non-whitespace character of a line and its line break.
/// </summary>
/// <remarks>
///  <para>
///   Trailing whitespace is invisible, so it survives review and then shows up as diff noise the next time
///   anyone touches the line. It is also what makes an otherwise identical file compare as changed.
///  </para>
///  <para>
///   A line consisting only of whitespace is reported in full: its whitespace is trailing whitespace on an
///   otherwise blank line.
///  </para>
///  <para>
///   Whitespace whose exact bytes are part of the program is never reported. Inside a verbatim, raw, or
///   interpolated string literal the whitespace at the end of a line belongs to the string's value, and
///   removing it would change what the program does; those spans are excluded, as is conditionally excluded
///   text, which the parser never interprets. See <see cref="WhitespaceAnalysis.GetProtectedSpans"/>.
///  </para>
///  <para>
///   A combining mark needs no special handling. The scan walks backwards from the end of the line and stops
///   at the first character that is not whitespace, and a combining mark is not whitespace - so a space that
///   carries a following mark is never the last character of the line and is never reported.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TrailingWhitespaceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0023";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Remove trailing whitespace",
        messageFormat: "Remove trailing whitespace",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Whitespace before a line break is invisible in review and turns into diff noise the next time the line is edited.",
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

        // Cheapest possible filter: a clean file costs one scan of the line table and nothing else - no tree
        // walk at all.
        List<TextSpan> runs = [];
        CollectTrailingRuns(text, context.CancellationToken, runs);

        if (runs.Count == 0)
        {
            return;
        }

        ImmutableArray<TextSpan> protectedSpans =
            WhitespaceAnalysis.GetProtectedSpans(context.Tree.GetRoot(context.CancellationToken), context.CancellationToken);

        foreach (TextSpan run in runs)
        {
            if (!WhitespaceAnalysis.IsProtected(protectedSpans, run))
            {
                context.ReportDiagnostic(Diagnostic.Create(s_rule, Location.Create(context.Tree, run)));
            }
        }
    }

    /// <summary>
    ///  Adds the span of each line's trailing whitespace to <paramref name="runs"/>.
    /// </summary>
    private static void CollectTrailingRuns(SourceText text, CancellationToken cancellationToken, List<TextSpan> runs)
    {
        foreach (TextLine line in text.Lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Span excludes the line break, so the last position it covers is the last character a reader
            // would see on the line.
            int end = line.End;
            int start = end;

            while (start > line.Start && char.IsWhiteSpace(text[start - 1]))
            {
                start--;
            }

            if (start < end)
            {
                runs.Add(TextSpan.FromBounds(start, end));
            }
        }
    }
}
