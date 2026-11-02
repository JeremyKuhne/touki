// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Requires consecutive statements in the same statement list to start on separate physical lines.
/// </summary>
/// <remarks>
///  <para>
///   Embedded statements, single-statement bodies, and semicolons in loop headers are not checked.
///   The rule ships disabled because source layout is a house style.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneStatementPerLineAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0042";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Use one statement per line",
        messageFormat: "Start this statement on a new line",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Consecutive statements in a block, switch section, or top-level program should not share a line.",
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
            AnalyzeStatementList,
            SyntaxKind.Block,
            SyntaxKind.SwitchSection,
            SyntaxKind.CompilationUnit);
    }

    private static void AnalyzeStatementList(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is BlockSyntax { Statements.Count: < 2 }
            or SwitchSectionSyntax { Statements.Count: < 2 }
            or CompilationUnitSyntax { Members.Count: < 2 })
        {
            return;
        }

        SourceText source = context.Node.SyntaxTree.GetText(context.CancellationToken);
        if (context.Node is CompilationUnitSyntax compilationUnit)
        {
            StatementSyntax? previous = null;
            foreach (MemberDeclarationSyntax member in compilationUnit.Members)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (member is GlobalStatementSyntax global)
                {
                    ReportIfSameLine(context, source, previous, global.Statement);
                    previous = global.Statement;
                }
                else
                {
                    previous = null;
                }
            }

            return;
        }

        SyntaxList<StatementSyntax> statements = context.Node is BlockSyntax block
            ? block.Statements
            : ((SwitchSectionSyntax)context.Node).Statements;
        for (int index = 1; index < statements.Count; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            ReportIfSameLine(context, source, statements[index - 1], statements[index]);
        }
    }

    private static void ReportIfSameLine(
        SyntaxNodeAnalysisContext context,
        SourceText source,
        StatementSyntax? previous,
        StatementSyntax current)
    {
        if (previous is null)
        {
            return;
        }

        SyntaxToken last = previous.GetLastToken(includeZeroWidth: true);
        SyntaxToken first = current.GetFirstToken(includeZeroWidth: true);
        if (last.IsMissing
            || first.IsMissing
            || last.Span.IsEmpty
            || first.Span.IsEmpty
            || source.Lines.GetLineFromPosition(last.Span.End - 1).LineNumber
                != source.Lines.GetLineFromPosition(first.SpanStart).LineNumber)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(s_rule, first.GetLocation()));
    }
}
