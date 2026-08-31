// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Requires precedence-aware operator placement and indentation across broken expressions.
/// </summary>
/// <remarks>
///  <para>
///   The rule does not initiate wrapping for an otherwise single-line precedence group or collapse multiline
///   expressions. Once a precedence group is broken, every operator in that group is broken and aligned.
///  </para>
///  <para>
///   Binary, conditional, assignment, declaration, member-access, range, type-testing, relational-pattern, and
///   binary-pattern operators are checked. Assignment-family operators, <c>is</c>, and expression-body, lambda,
///   and switch-arm <c>=&gt;</c> tokens use trailing placement.
///  </para>
///  <para>
///   A direct collection expression or array initializer whose delimiters are each on their own line aligns its
///   opening delimiter like a block brace. A collection or initializer that stays on one line uses ordinary
///   continuation indentation.
///  </para>
///  <para>
///   Indentation follows the standard <c>indent_style</c>, <c>indent_size</c>, and <c>tab_width</c> options.
///   An occurrence is ignored when moving the operator would cross a comment, directive, or other non-whitespace
///   trivia.
///  </para>
///  <para>
///   Syntax nested through more than 256 ancestors and changes that would scan, replace, or generate more than
///   4,096 characters are ignored to keep live analysis bounded.
///  </para>
///  <para>
///   The rule ships disabled because operator placement is a house style.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StatementBreakFormattingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0028";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Format statement breaks",
        messageFormat: "Format the line break around '{0}'",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Operators on continuation lines should use the configured placement and indentation.",
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
            AnalyzeSyntaxNode,
            SyntaxKind.MultiplyExpression,
            SyntaxKind.DivideExpression,
            SyntaxKind.ModuloExpression,
            SyntaxKind.AddExpression,
            SyntaxKind.SubtractExpression,
            SyntaxKind.LeftShiftExpression,
            SyntaxKind.RightShiftExpression,
            SyntaxKind.UnsignedRightShiftExpression,
            SyntaxKind.LessThanExpression,
            SyntaxKind.GreaterThanExpression,
            SyntaxKind.LessThanOrEqualExpression,
            SyntaxKind.GreaterThanOrEqualExpression,
            SyntaxKind.EqualsExpression,
            SyntaxKind.NotEqualsExpression,
            SyntaxKind.BitwiseAndExpression,
            SyntaxKind.ExclusiveOrExpression,
            SyntaxKind.BitwiseOrExpression,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.CoalesceExpression,
            SyntaxKind.AsExpression,
            SyntaxKind.IsExpression,
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxKind.AddAssignmentExpression,
            SyntaxKind.SubtractAssignmentExpression,
            SyntaxKind.MultiplyAssignmentExpression,
            SyntaxKind.DivideAssignmentExpression,
            SyntaxKind.ModuloAssignmentExpression,
            SyntaxKind.AndAssignmentExpression,
            SyntaxKind.ExclusiveOrAssignmentExpression,
            SyntaxKind.OrAssignmentExpression,
            SyntaxKind.LeftShiftAssignmentExpression,
            SyntaxKind.RightShiftAssignmentExpression,
            SyntaxKind.UnsignedRightShiftAssignmentExpression,
            SyntaxKind.CoalesceAssignmentExpression,
            SyntaxKind.ConditionalExpression,
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxKind.PointerMemberAccessExpression,
            SyntaxKind.ConditionalAccessExpression,
            SyntaxKind.RangeExpression,
            SyntaxKind.IsPatternExpression,
            SyntaxKind.AndPattern,
            SyntaxKind.OrPattern,
            SyntaxKind.RelationalPattern,
            SyntaxKind.EqualsValueClause,
            SyntaxKind.LetClause,
            SyntaxKind.NameEquals,
            SyntaxKind.ArrowExpressionClause,
            SyntaxKind.SimpleLambdaExpression,
            SyntaxKind.ParenthesizedLambdaExpression,
            SyntaxKind.SwitchExpressionArm);
    }

    private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        SourceText source = context.Node.SyntaxTree.GetText(context.CancellationToken);
        string? indentationUnit = null;

        if (context.Node is BinaryExpressionSyntax binary)
        {
            if (!StatementBreakFormatting.IsPrecedenceGroupRoot(binary)
                || !StatementBreakFormatting.TryCollectPrecedenceGroup(
                    binary,
                    source,
                    context.CancellationToken,
                    out List<StatementBreakOperator> operations,
                    out bool hasBreak)
                || !hasBreak)
            {
                return;
            }

            foreach (StatementBreakOperator operation in operations)
            {
                AnalyzeOperation(
                    context,
                    source,
                    operation,
                    precedenceGroupHasBreak: true,
                    ref indentationUnit);
            }

            return;
        }

        if (context.Node is BinaryPatternSyntax pattern)
        {
            if (!StatementBreakFormatting.IsPrecedenceGroupRoot(pattern)
                || !StatementBreakFormatting.TryCollectPrecedenceGroup(
                    pattern,
                    source,
                    context.CancellationToken,
                    out List<StatementBreakOperator> operations,
                    out bool hasBreak)
                || !hasBreak)
            {
                return;
            }

            foreach (StatementBreakOperator operation in operations)
            {
                AnalyzeOperation(
                    context,
                    source,
                    operation,
                    precedenceGroupHasBreak: true,
                    ref indentationUnit);
            }

            return;
        }

        for (int index = 0; index < 2; index++)
        {
            if (!StatementBreakFormatting.TryGetOperator(
                context.Node,
                index,
                out StatementBreakOperator operation)
                || !StatementBreakFormatting.SpansMultipleLines(
                    operation,
                    source,
                    context.CancellationToken))
            {
                continue;
            }

            AnalyzeOperation(
                context,
                source,
                operation,
                precedenceGroupHasBreak: false,
                ref indentationUnit);
        }
    }

    private static void AnalyzeOperation(
        SyntaxNodeAnalysisContext context,
        SourceText source,
        StatementBreakOperator operation,
        bool precedenceGroupHasBreak,
        ref string? indentationUnit)
    {
        if (indentationUnit is null)
        {
            AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(
                context.Node.SyntaxTree);
            indentationUnit = StatementBreakFormattingOptions.GetIndentationUnit(options);
        }

        bool hasChange = precedenceGroupHasBreak
            ? StatementBreakFormatting.TryGetPrecedenceGroupChange(
                operation,
                source,
                indentationUnit,
                context.CancellationToken,
                out StatementBreakChangeKind changeKind,
                out TextSpan replacementSpan,
                out StatementBreakIndentation indentation)
            : StatementBreakFormatting.TryGetChange(
                operation,
                source,
                indentationUnit,
                context.CancellationToken,
                out changeKind,
                out replacementSpan,
                out indentation);
        if (!hasChange)
        {
            return;
        }

        ImmutableDictionary<string, string?> properties = StatementBreakDiagnosticData.CreateProperties(
            changeKind,
            operation.OperatorText,
            operation.SpaceAfter,
            indentation);
        context.ReportDiagnostic(
            Diagnostic.Create(
                s_rule,
                Location.Create(context.Node.SyntaxTree, operation.OperatorSpan),
                additionalLocations:
                [
                    Location.Create(context.Node.SyntaxTree, replacementSpan),
                    Location.Create(context.Node.SyntaxTree, indentation.BaseSpan)
                ],
                properties,
                operation.OperatorText));
    }
}