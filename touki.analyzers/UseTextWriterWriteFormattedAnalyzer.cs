// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Touki.Analyzers;

/// <summary>
///  Reports interpolated strings passed to <see cref="TextWriter.Write(string)"/>, where
///  <c>Touki.Io.TextWriterExtensions.WriteFormatted</c> can write the formatted content without an intermediate
///  string when the writer supports direct copying.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseTextWriterWriteFormattedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0031";

    /// <summary>
    ///  The CLR metadata name of <see cref="TextWriter"/>.
    /// </summary>
    public const string TextWriterMetadataName = "System.IO.TextWriter";

    /// <summary>
    ///  The CLR metadata name of the extension type that provides <c>WriteFormatted</c>.
    /// </summary>
    public const string TextWriterExtensionsMetadataName = "Touki.Io.TextWriterExtensions";

    /// <summary>
    ///  The CLR metadata name of the attribute that identifies an interpolated string handler.
    /// </summary>
    public const string InterpolatedStringHandlerAttributeMetadataName =
        "System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute";

    /// <summary>
    ///  The CLR metadata name of expression trees, which cannot contain interpolated string handler conversions.
    /// </summary>
    public const string ExpressionMetadataName = "System.Linq.Expressions.Expression`1";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Use WriteFormatted for interpolated strings",
        messageFormat: "Use 'WriteFormatted' to avoid an intermediate string on supported writers",
        category: "Performance",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "TextWriter.Write converts an interpolated string to a string before writing it. "
            + "Touki.Io.TextWriterExtensions.WriteFormatted uses an interpolated string handler and writes "
            + "directly to supported framework writers without that intermediate allocation.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (start.Compilation.GetTypeByMetadataName(TextWriterMetadataName) is not { } textWriter
                || start.Compilation.GetTypeByMetadataName("System.IO.StringWriter") is not { } stringWriter
                || start.Compilation.GetTypeByMetadataName("System.IO.StreamWriter") is not { } streamWriter
                || start.Compilation.GetTypeByMetadataName(TextWriterExtensionsMetadataName) is not { } extensions
                || start.Compilation.GetTypeByMetadataName(InterpolatedStringHandlerAttributeMetadataName)
                    is not { } handlerAttribute
                || !HasWriteFormattedExtension(extensions, textWriter, handlerAttribute))
            {
                return;
            }

            INamedTypeSymbol? expression = start.Compilation.GetTypeByMetadataName(ExpressionMetadataName);
            start.RegisterOperationAction(
                operationContext => AnalyzeInvocation(
                    operationContext,
                    textWriter,
                    stringWriter,
                    streamWriter,
                    expression),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol textWriter,
        INamedTypeSymbol stringWriter,
        INamedTypeSymbol streamWriter,
        INamedTypeSymbol? expression)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        if (invocation.Syntax is not InvocationExpressionSyntax syntax
            || syntax.ArgumentList.Arguments.Count != 1
            || GetInterpolatedString(syntax.ArgumentList.Arguments[0].Expression) is not { } interpolatedString
            || syntax.SyntaxTree.Options is not CSharpParseOptions parseOptions
            || parseOptions.LanguageVersion < LanguageVersion.CSharp10)
        {
            return;
        }

        SimpleNameSyntax? methodName = syntax.Expression switch
        {
            MemberAccessExpressionSyntax { Expression: not BaseExpressionSyntax } memberAccess => memberAccess.Name,
            _ => null
        };

        if (methodName?.Identifier.ValueText != nameof(TextWriter.Write)
            || invocation.Arguments.Length != 1
            || invocation.Arguments[0].Value.ConstantValue.HasValue
            || !CanUseOptimizedWriter(invocation.Instance?.Type, textWriter, stringWriter, streamWriter)
            || IsInsideExpressionTree(invocation, expression))
        {
            return;
        }

        IMethodSymbol method = invocation.TargetMethod;
        if (method.IsStatic
            || method.Name != nameof(TextWriter.Write)
            || method.Parameters.Length != 1
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String
            || !IsTextWriterWrite(method, textWriter))
        {
            return;
        }

        if (invocation.SemanticModel is not { } semanticModel
            || BindsToInstanceWriteFormatted(semanticModel, syntax, methodName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(s_rule, methodName.GetLocation()));
    }

    private static bool CanUseOptimizedWriter(
        ITypeSymbol? receiverType,
        INamedTypeSymbol textWriter,
        INamedTypeSymbol stringWriter,
        INamedTypeSymbol streamWriter)
    {
        if (SymbolEqualityComparer.Default.Equals(receiverType, textWriter)
            || SymbolEqualityComparer.Default.Equals(receiverType, stringWriter)
            || SymbolEqualityComparer.Default.Equals(receiverType, streamWriter))
        {
            return true;
        }

        if (receiverType is ITypeParameterSymbol typeParameter)
        {
            foreach (ITypeSymbol constraint in typeParameter.ConstraintTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(constraint, textWriter)
                    || SymbolEqualityComparer.Default.Equals(constraint, stringWriter)
                    || SymbolEqualityComparer.Default.Equals(constraint, streamWriter))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasWriteFormattedExtension(
        INamedTypeSymbol extensions,
        INamedTypeSymbol textWriter,
        INamedTypeSymbol handlerAttribute)
    {
        foreach (ISymbol member in extensions.GetMembers("WriteFormatted"))
        {
            if (member is not IMethodSymbol
                {
                    IsExtensionMethod: true,
                    IsStatic: true,
                    ReturnsVoid: true,
                    DeclaredAccessibility: Accessibility.Public,
                    Parameters.Length: 2
                } method
                || method.Parameters[0].RefKind != RefKind.None
                || !SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, textWriter)
                || method.Parameters[1].RefKind != RefKind.Ref)
            {
                continue;
            }

            foreach (AttributeData attribute in method.Parameters[1].Type.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, handlerAttribute))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsTextWriterWrite(IMethodSymbol method, INamedTypeSymbol textWriter)
    {
        for (IMethodSymbol? current = method; current is not null; current = current.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(current.ContainingType, textWriter))
            {
                return true;
            }
        }

        return false;
    }

    private static bool BindsToInstanceWriteFormatted(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        SimpleNameSyntax methodName)
    {
        SyntaxToken replacementIdentifier = SyntaxFactory.Identifier(
            methodName.Identifier.LeadingTrivia,
            "WriteFormatted",
            methodName.Identifier.TrailingTrivia);
        InvocationExpressionSyntax candidate = invocation.ReplaceToken(
            methodName.Identifier,
            replacementIdentifier);

        ArgumentSyntax argument = candidate.ArgumentList.Arguments[0];
        if (argument.NameColon is { } nameColon)
        {
            IdentifierNameSyntax builderName = SyntaxFactory.IdentifierName(
                SyntaxFactory.Identifier(
                    nameColon.Name.Identifier.LeadingTrivia,
                    "builder",
                    nameColon.Name.Identifier.TrailingTrivia));
            candidate = candidate.ReplaceNode(argument, argument.WithNameColon(nameColon.WithName(builderName)));
        }

        SymbolInfo symbolInfo = semanticModel.GetSpeculativeSymbolInfo(
            invocation.SpanStart,
            candidate,
            SpeculativeBindingOption.BindAsExpression);
        return symbolInfo.Symbol is IMethodSymbol { IsStatic: false, ReducedFrom: null };
    }

    private static InterpolatedStringExpressionSyntax? GetInterpolatedString(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression as InterpolatedStringExpressionSyntax;
    }

    private static bool IsInsideExpressionTree(IOperation operation, INamedTypeSymbol? expression)
    {
        if (expression is null)
        {
            return false;
        }

        for (IOperation? current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IConversionOperation { Type: INamedTypeSymbol convertedType }
                && SymbolEqualityComparer.Default.Equals(convertedType.OriginalDefinition, expression))
            {
                return true;
            }
        }

        return false;
    }
}