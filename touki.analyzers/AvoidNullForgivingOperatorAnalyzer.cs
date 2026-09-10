// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports uses of the null-forgiving operator.
/// </summary>
/// <remarks>
///  <para>
///   The null-forgiving operator suppresses nullable warnings without adding a run-time check.
///   Code should instead prove that the value is not <see langword="null"/>.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AvoidNullForgivingOperatorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0005";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Avoid the null-forgiving operator",
        messageFormat: "Avoid the null-forgiving operator",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Nullability warnings should be resolved without the null-forgiving operator.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = ImmutableArray.Create(s_rule);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSuppression, SyntaxKind.SuppressNullableWarningExpression);
    }

    private static void AnalyzeSuppression(SyntaxNodeAnalysisContext context)
    {
        PostfixUnaryExpressionSyntax suppression = (PostfixUnaryExpressionSyntax)context.Node;
        context.ReportDiagnostic(Diagnostic.Create(s_rule, suppression.OperatorToken.GetLocation()));
    }
}
