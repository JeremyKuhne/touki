// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Stands in for the built-in naming rule by reporting <c>IDE1006</c> on every field, at the same location
///  the real rule uses: the field's identifier.
/// </summary>
/// <remarks>
///  <para>
///   The real rule lives in the IDE code-style analyzers, which this project does not reference. What
///   <see cref="ThreadStaticNamingSuppressor"/> needs from it is only the id and the location, both of which
///   this reproduces. The default severity is deliberately below <see cref="DiagnosticSeverity.Error"/>,
///   matching the real rule, because that is what makes a diagnostic suppressible at all.
///  </para>
///  <para>
///   The analyzer authoring rules that describe a shippable analyzer assembly (RS1036, RS1038, RS1041,
///   RS2008) are turned off for this project in its <c>.editorconfig</c>. This stub is handed to
///   <c>CompilationWithAnalyzers</c> directly and never packaged.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
internal sealed class NamingRuleStubAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = ThreadStaticNamingSuppressor.SuppressedDiagnosticId;

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Naming rule violation",
        messageFormat: "Naming rule violation: {0}",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeDeclarator, SyntaxKind.VariableDeclarator);
    }

    private static void AnalyzeDeclarator(SyntaxNodeAnalysisContext context)
    {
        VariableDeclaratorSyntax declarator = (VariableDeclaratorSyntax)context.Node;

        if (declarator.Parent?.Parent is not FieldDeclarationSyntax)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(s_rule, declarator.Identifier.GetLocation(), declarator.Identifier.ValueText));
    }
}
