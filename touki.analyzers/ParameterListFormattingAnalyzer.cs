// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Requires parameter and argument lists to fit on one line or put every item on its own indented line.
/// </summary>
/// <remarks>
///  <para>
///   Line breaks within an item's contents do not by themselves require wrapping its containing list.
///   The rule ships disabled because source layout is a house style.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParameterListFormattingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0043";

    /// <summary>
    ///  The option overriding the maximum physical line length for a single-line list.
    /// </summary>
    public const string MaxLineLengthOption = ParameterListFormatting.MaxLineLengthOption;

    /// <summary>
    ///  The line-length limit used when neither supported option supplies a positive integer.
    /// </summary>
    public const int DefaultMaxLineLength = ParameterListFormatting.DefaultMaxLineLength;

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Format parameter and argument lists",
        messageFormat: "Put every parameter or argument on its own indented line",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Lists must fit on one physical line or place every item on its own indented line.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeList,
            SyntaxKind.ParameterList,
            SyntaxKind.BracketedParameterList,
            SyntaxKind.ArgumentList,
            SyntaxKind.BracketedArgumentList,
            SyntaxKind.AttributeArgumentList);
    }

    private static void AnalyzeList(SyntaxNodeAnalysisContext context)
    {
        if (context.Node.ChildNodesAndTokens().Count < 3)
        {
            return;
        }

        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(
            context.Node.SyntaxTree);
        int maxLineLength = ParameterListFormatting.GetMaxLineLength(options);
        string unit = StatementBreakFormattingOptions.GetIndentationUnit(options);
        SourceText source = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (!ParameterListFormatting.IsViolation(context.Node, source, maxLineLength, unit, context.CancellationToken))
        {
            return;
        }

        if (ParameterListFormatting.IsNameofCandidate(context.Node)
            && ParameterListFormatting.IsNameofOperator(
                context.Node,
                context.SemanticModel,
                context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            s_rule,
            context.Node.GetFirstToken().GetLocation(),
            properties: ImmutableDictionary<string, string?>.Empty
                .Add("MaxLineLength", maxLineLength.ToString(CultureInfo.InvariantCulture))
                .Add("Indentation", unit)));
    }
}
