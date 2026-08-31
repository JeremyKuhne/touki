// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Touki.Analyzers;

/// <summary>
///  Requires selected literal arguments to include the corresponding parameter name.
/// </summary>
/// <remarks>
///  <para>
///   By default, the analyzer reports positional boolean, <see langword="null"/>, and
///   <see langword="default"/> arguments. Configure the comma-separated literal kinds with
///   <c>dotnet_code_quality.TOUKI0029.literals</c>.
///  </para>
///  <para>
///   Accepted values are <c>integer</c>, <c>floating_point</c>, <c>character</c>, <c>string</c>,
///   <c>boolean</c>, <c>null</c>, and <c>default</c>. The <c>boolean</c> value includes both
///   <see langword="true"/> and <see langword="false"/>.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class RequireNamedArgumentsForLiteralsAnalyzer : DiagnosticAnalyzer
{
    private const string ParameterNameProperty = "ParameterName";

    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0029";

    /// <summary>
    ///  The <c>.editorconfig</c> key that selects the literal kinds that require named arguments.
    /// </summary>
    public const string LiteralsOption = "dotnet_code_quality.TOUKI0029.literals";

    private const LiteralKinds DefaultLiteralKinds =
        LiteralKinds.Boolean | LiteralKinds.Null | LiteralKinds.Default;

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Name literal arguments",
        messageFormat: "Use the parameter name '{0}:' for this literal argument",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Literal arguments should include parameter names so their meaning is clear at the call site.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(startContext =>
        {
            ConcurrentDictionary<SyntaxTree, ConfiguredLiteralKinds> configuredKinds = new();
            ConcurrentDictionary<SyntaxTree, CompilerErrorCache> compilerErrors = new();
            startContext.RegisterOperationAction(
                operationContext => AnalyzeArgument(
                    operationContext,
                    configuredKinds,
                    compilerErrors),
                OperationKind.Argument);
        });
    }

    private static void AnalyzeArgument(
        OperationAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, ConfiguredLiteralKinds> configuredKinds,
        ConcurrentDictionary<SyntaxTree, CompilerErrorCache> compilerErrors)
    {
        IArgumentOperation operation = (IArgumentOperation)context.Operation;

        if (operation.ArgumentKind != ArgumentKind.Explicit
            || operation.Parameter is not { CanBeReferencedByName: true } parameter
            || !operation.InConversion.Exists)
        {
            return;
        }

        if (!TryGetSourceExpression(operation, out ExpressionSyntax expression, out bool isAttributeArgument)
            || (operation.Parent?.IsImplicit == true && !isAttributeArgument))
        {
            return;
        }

        LiteralKinds literalKind = GetLiteralKind(expression);
        if (literalKind == LiteralKinds.None
            || !IsConfigured(context, configuredKinds, literalKind))
        {
            return;
        }

        if (HasCompilerError(context, expression, compilerErrors))
        {
            return;
        }

        string parameterDisplayName = SyntaxFacts.GetKeywordKind(parameter.Name) == SyntaxKind.None
            ? parameter.Name
            : $"@{parameter.Name}";
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(ParameterNameProperty, parameter.Name);

        context.ReportDiagnostic(
            Diagnostic.Create(
                s_rule,
                expression.GetLocation(),
                properties,
                parameterDisplayName));
    }

    private static bool HasCompilerError(
        OperationAnalysisContext context,
        ExpressionSyntax expression,
        ConcurrentDictionary<SyntaxTree, CompilerErrorCache> compilerErrors)
    {
        SemanticModel? semanticModel = context.Operation.SemanticModel;
        if (semanticModel is null)
        {
            return true;
        }

        CompilerErrorCache cache = compilerErrors.GetOrAdd(expression.SyntaxTree, static _ => new());
        return cache.Overlaps(semanticModel, expression.Span, context.CancellationToken);
    }

    private static bool TryGetSourceExpression(
        IArgumentOperation operation,
        out ExpressionSyntax expression,
        out bool isAttributeArgument)
    {
        SyntaxNode syntax = operation.Syntax;

        while (true)
        {
            switch (syntax)
            {
                case ArgumentSyntax { NameColon: null } argument:
                    expression = argument.Expression;
                    isAttributeArgument = false;
                    return true;

                case AttributeArgumentSyntax { NameColon: null } argument:
                    expression = argument.Expression;
                    isAttributeArgument = true;
                    return true;

                case ExpressionSyntax current
                    when current.Parent is ArgumentSyntax argument
                        && ReferenceEquals(argument.Expression, current):
                    syntax = argument;
                    continue;

                case ExpressionSyntax current
                    when current.Parent is AttributeArgumentSyntax argument
                        && ReferenceEquals(argument.Expression, current):
                    syntax = argument;
                    continue;

                case ExpressionSyntax current when TryGetTransparentParent(current, out ExpressionSyntax parent):
                    syntax = parent;
                    continue;

                default:
                    expression = null!;
                    isAttributeArgument = false;
                    return false;
            }
        }
    }

    private static bool TryGetTransparentParent(ExpressionSyntax expression, out ExpressionSyntax parent)
    {
        ExpressionSyntax? candidateParent = expression.Parent switch
        {
            ParenthesizedExpressionSyntax candidate when ReferenceEquals(candidate.Expression, expression) => candidate,
            CastExpressionSyntax candidate when ReferenceEquals(candidate.Expression, expression) => candidate,
            CheckedExpressionSyntax candidate when ReferenceEquals(candidate.Expression, expression) => candidate,
            PrefixUnaryExpressionSyntax candidate
                when ReferenceEquals(candidate.Operand, expression)
                    && (candidate.IsKind(SyntaxKind.UnaryMinusExpression)
                        || candidate.IsKind(SyntaxKind.UnaryPlusExpression)) => candidate,
            PostfixUnaryExpressionSyntax candidate
                when ReferenceEquals(candidate.Operand, expression)
                    && candidate.IsKind(SyntaxKind.SuppressNullableWarningExpression) => candidate,
            _ => null
        };

        parent = candidateParent!;
        return candidateParent is not null;
    }

    private static bool IsConfigured(
        OperationAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, ConfiguredLiteralKinds> configuredKinds,
        LiteralKinds literalKind)
    {
        SyntaxTree tree = context.Operation.Syntax.SyntaxTree;
        ConfiguredLiteralKinds configured = configuredKinds.GetOrAdd(tree, static _ => new());
        LiteralKinds kinds = configured.Get(
            context.Options.AnalyzerConfigOptionsProvider,
            tree,
            context.CancellationToken);

        return (kinds & literalKind) != 0;
    }

    private static bool TryParseLiteralKinds(
        string configured,
        CancellationToken cancellationToken,
        out LiteralKinds literalKinds)
    {
        literalKinds = LiteralKinds.None;
        int tokenStart = 0;
        for (int index = 0; index <= configured.Length; index++)
        {
            if ((index & 0xFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (index < configured.Length && configured[index] != ',')
            {
                continue;
            }

            int start = tokenStart;
            while (start < index && char.IsWhiteSpace(configured[start]))
            {
                if ((start & 0xFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                start++;
            }

            int end = index;
            while (end > start && char.IsWhiteSpace(configured[end - 1]))
            {
                if ((end & 0xFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                end--;
            }

            if (!TryParseLiteralKind(configured.AsSpan(start, end - start), out LiteralKinds literalKind))
            {
                literalKinds = LiteralKinds.None;
                return false;
            }

            literalKinds |= literalKind;
            tokenStart = index + 1;
        }

        return literalKinds != LiteralKinds.None;
    }

    private static bool TryParseLiteralKind(ReadOnlySpan<char> value, out LiteralKinds literalKind)
    {
        if (value.Equals("integer".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            literalKind = LiteralKinds.Integer;
        }
        else if (value.Equals("floating_point".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            literalKind = LiteralKinds.FloatingPoint;
        }
        else if (value.Equals("character".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            literalKind = LiteralKinds.Character;
        }
        else if (value.Equals("string".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            literalKind = LiteralKinds.String;
        }
        else if (value.Equals("boolean".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            literalKind = LiteralKinds.Boolean;
        }
        else if (value.Equals("null".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            literalKind = LiteralKinds.Null;
        }
        else if (value.Equals("default".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            literalKind = LiteralKinds.Default;
        }
        else
        {
            literalKind = LiteralKinds.None;
            return false;
        }

        return true;
    }

    private static LiteralKinds GetLiteralKind(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;

                case CastExpressionSyntax cast:
                    expression = cast.Expression;
                    continue;

                case CheckedExpressionSyntax checkedExpression:
                    expression = checkedExpression.Expression;
                    continue;

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    continue;

                case PrefixUnaryExpressionSyntax prefix
                    when prefix.IsKind(SyntaxKind.UnaryMinusExpression)
                        || prefix.IsKind(SyntaxKind.UnaryPlusExpression):
                    expression = prefix.Operand;
                    continue;
            }

            break;
        }

        return expression.Kind() switch
        {
            SyntaxKind.NumericLiteralExpression => GetNumericLiteralKind((LiteralExpressionSyntax)expression),
            SyntaxKind.CharacterLiteralExpression => LiteralKinds.Character,
            SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression => LiteralKinds.String,
            SyntaxKind.InterpolatedStringExpression => LiteralKinds.String,
            SyntaxKind.TrueLiteralExpression or SyntaxKind.FalseLiteralExpression => LiteralKinds.Boolean,
            SyntaxKind.NullLiteralExpression => LiteralKinds.Null,
            SyntaxKind.DefaultLiteralExpression or SyntaxKind.DefaultExpression => LiteralKinds.Default,
            _ => LiteralKinds.None
        };
    }

    private static LiteralKinds GetNumericLiteralKind(LiteralExpressionSyntax literal) =>
        literal.Token.Value is float or double or decimal
            ? LiteralKinds.FloatingPoint
            : LiteralKinds.Integer;

}
