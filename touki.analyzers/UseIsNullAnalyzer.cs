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
///  Reports <c>==</c> and <c>!=</c> comparisons against the <see langword="null"/> literal and
///  suggests the <see langword="is"/> <see langword="null"/> and <see langword="is"/>
///  <see langword="not"/> <see langword="null"/> patterns instead.
/// </summary>
/// <remarks>
///  <para>
///   This mirrors the repository convention of using <c>is null</c> and <c>is not null</c> for
///   null checks rather than the equality operators.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseIsNullAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0001";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Use pattern matching for null checks",
        messageFormat: "Use '{0}' instead of '{1}'",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Comparisons against the null literal should use the 'is null' and 'is not null' patterns.",
        helpLinkUri: "https://github.com/JeremyKuhne/touki");

    // Cache the supported-diagnostics array so the property does not allocate a new array on every access.
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = ImmutableArray.Create(s_rule);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeComparison,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression);
    }

    private static void AnalyzeComparison(SyntaxNodeAnalysisContext context)
    {
        BinaryExpressionSyntax comparison = (BinaryExpressionSyntax)context.Node;

        bool comparesToNull =
            comparison.Left.IsKind(SyntaxKind.NullLiteralExpression)
            || comparison.Right.IsKind(SyntaxKind.NullLiteralExpression);

        if (!comparesToNull)
        {
            return;
        }

        bool isEquals = comparison.IsKind(SyntaxKind.EqualsExpression);
        string suggestion = isEquals ? "is null" : "is not null";
        string current = isEquals ? "== null" : "!= null";

        context.ReportDiagnostic(Diagnostic.Create(s_rule, comparison.GetLocation(), suggestion, current));
    }
}
