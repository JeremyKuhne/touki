// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Use WriteFormatted" fix for <c>TOUKI0031</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UseTextWriterWriteFormattedCodeFixProvider))]
[Shared]
public sealed partial class UseTextWriterWriteFormattedCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; this is a stable public contract.
    private const string UseTextWriterWriteFormattedId = "TOUKI0031";
    private const string ExtensionNamespace = "Touki.Io";
    private const string TextWriterMetadataName = "System.IO.TextWriter";
    private const string TextWriterExtensionsMetadataName = "Touki.Io.TextWriterExtensions";
    private const string InterpolatedStringHandlerAttributeMetadataName =
        "System.Runtime.CompilerServices.InterpolatedStringHandlerAttribute";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [UseTextWriterWriteFormattedId];

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root =
            await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null
            || root.SyntaxTree.Options is not CSharpParseOptions parseOptions
            || parseOptions.LanguageVersion < LanguageVersion.CSharp10)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            SimpleNameSyntax? methodName = node as SimpleNameSyntax ?? node.FirstAncestorOrSelf<SimpleNameSyntax>();
            InvocationExpressionSyntax? invocation = methodName?.Parent switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Parent as InvocationExpressionSyntax,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Parent as InvocationExpressionSyntax,
                _ => null
            };

            if (invocation is null)
            {
                continue;
            }

            Document? changedDocument = await TryUseWriteFormattedAsync(
                context.Document,
                invocation.Span,
                context.CancellationToken).ConfigureAwait(false);
            if (changedDocument is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use WriteFormatted",
                    createChangedDocument: _ => Task.FromResult(changedDocument),
                    equivalenceKey: nameof(UseTextWriterWriteFormattedCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<Document?> TryUseWriteFormattedAsync(
        Document document,
        TextSpan invocationSpan,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return null;
        }

        SemanticModel? originalSemanticModel =
            await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (originalSemanticModel is null)
        {
            return null;
        }

        SyntaxNode currentNode = compilationUnit.FindNode(invocationSpan, getInnermostNodeForTie: true);
        InvocationExpressionSyntax? invocation = currentNode as InvocationExpressionSyntax
            ?? currentNode.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null || invocation.Span != invocationSpan)
        {
            return null;
        }

        if (!IsEligibleInvocation(invocation, originalSemanticModel, cancellationToken))
        {
            return null;
        }

        SimpleNameSyntax? originalMethodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null
        };
        SimpleNameSyntax? originalArgumentName = invocation.ArgumentList.Arguments[0].NameColon?.Name;
        if (originalMethodName is null)
        {
            return null;
        }

        List<BindingSnapshot> bindingSnapshots = [];
        Dictionary<SyntaxNode, SyntaxAnnotation> bindingAnnotations = [];
        foreach (SyntaxNode descendant in compilationUnit.DescendantNodes())
        {
            if (descendant is not SimpleNameSyntax and not InvocationExpressionSyntax
                || ReferenceEquals(descendant, invocation)
                || ReferenceEquals(descendant, originalMethodName)
                || ReferenceEquals(descendant, originalArgumentName))
            {
                continue;
            }

            ISymbol? symbol = originalSemanticModel.GetSymbolInfo(descendant, cancellationToken).Symbol;
            if (symbol is null)
            {
                continue;
            }

            SyntaxAnnotation annotation = new();
            bindingAnnotations.Add(descendant, annotation);
            bindingSnapshots.Add(new(annotation, GetSymbolIdentity(symbol)));
        }

        compilationUnit = compilationUnit.ReplaceNodes(
            bindingAnnotations.Keys,
            (original, rewritten) => rewritten.WithAdditionalAnnotations(bindingAnnotations[original]));
        currentNode = compilationUnit.FindNode(invocationSpan, getInnermostNodeForTie: true);
        invocation = currentNode as InvocationExpressionSyntax
            ?? currentNode.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null || invocation.Span != invocationSpan)
        {
            return null;
        }

        SimpleNameSyntax? methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null
        };
        if (methodName is null)
        {
            return null;
        }

        IdentifierNameSyntax replacementName = SyntaxFactory.IdentifierName(
            SyntaxFactory.Identifier(
                methodName.Identifier.LeadingTrivia,
                "WriteFormatted",
                methodName.Identifier.TrailingTrivia));
        SyntaxAnnotation invocationAnnotation = new();
        InvocationExpressionSyntax replacement = invocation
            .ReplaceNode(methodName, replacementName)
            .WithAdditionalAnnotations(invocationAnnotation);

        ArgumentSyntax argument = replacement.ArgumentList.Arguments[0];
        if (argument.NameColon is { } nameColon)
        {
            IdentifierNameSyntax builderName = SyntaxFactory.IdentifierName(
                SyntaxFactory.Identifier(
                    nameColon.Name.Identifier.LeadingTrivia,
                    "builder",
                    nameColon.Name.Identifier.TrailingTrivia));
            replacement = replacement.ReplaceNode(argument, argument.WithNameColon(nameColon.WithName(builderName)));
        }

        CompilationUnitSyntax updatedRoot = compilationUnit.ReplaceNode(invocation, replacement);
        Document changedDocument = document.WithSyntaxRoot(updatedRoot);
        if (await BindsToTextWriterExtensionAsync(
            changedDocument,
            invocationAnnotation,
            cancellationToken).ConfigureAwait(false)
            && await ReplacementPreservesDocumentAsync(
                originalSemanticModel,
                changedDocument,
                bindingSnapshots,
                cancellationToken).ConfigureAwait(false))
        {
            return changedDocument;
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(ExtensionNamespace))
            .WithAdditionalAnnotations(Formatter.Annotation);
        changedDocument = document.WithSyntaxRoot(updatedRoot.AddUsings(usingDirective));
        if (!await BindsToTextWriterExtensionAsync(
            changedDocument,
            invocationAnnotation,
            cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        if (!await ReplacementPreservesDocumentAsync(
            originalSemanticModel,
            changedDocument,
            bindingSnapshots,
            cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return await Formatter.FormatAsync(
            changedDocument,
            Formatter.Annotation,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static bool IsEligibleInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Expression: not BaseExpressionSyntax,
                Name.Identifier.ValueText: "Write"
            }
            || invocation.ArgumentList.Arguments.Count != 1
            || GetInterpolatedString(invocation.ArgumentList.Arguments[0].Expression) is null
            || semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation
            || operation.Arguments.Length != 1
            || operation.Arguments[0].Value.ConstantValue.HasValue
            || operation.TargetMethod.IsStatic
            || operation.TargetMethod.Name != "Write"
            || operation.TargetMethod.Parameters.Length != 1
            || operation.TargetMethod.Parameters[0].Type.SpecialType != SpecialType.System_String)
        {
            return false;
        }

        Compilation compilation = semanticModel.Compilation;
        if (compilation.GetTypeByMetadataName(TextWriterMetadataName) is not { } textWriter
            || compilation.GetTypeByMetadataName("System.IO.StringWriter") is not { } stringWriter
            || compilation.GetTypeByMetadataName("System.IO.StreamWriter") is not { } streamWriter
            || !IsTextWriterWrite(operation.TargetMethod, textWriter)
            || !CanUseOptimizedWriter(operation.Instance?.Type, textWriter, stringWriter, streamWriter))
        {
            return false;
        }

        INamedTypeSymbol? expression = compilation.GetTypeByMetadataName("System.Linq.Expressions.Expression`1");
        return !IsInsideExpressionTree(operation, expression);
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

    private static async Task<bool> BindsToTextWriterExtensionAsync(
        Document document,
        SyntaxAnnotation invocationAnnotation,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return false;
        }

        InvocationExpressionSyntax? invocation = null;
        foreach (SyntaxNode node in root.GetAnnotatedNodes(invocationAnnotation))
        {
            if (node is InvocationExpressionSyntax candidate)
            {
                invocation = candidate;
                break;
            }
        }

        if (invocation is null)
        {
            return false;
        }

        SymbolInfo invocationSymbol = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        SymbolInfo expressionSymbol = semanticModel.GetSymbolInfo(invocation.Expression, cancellationToken);
        IOperation? invocationOperation = semanticModel.GetOperation(invocation, cancellationToken);
        IMethodSymbol? method = invocationSymbol.Symbol as IMethodSymbol;
        method ??= expressionSymbol.Symbol as IMethodSymbol;
        if (method is null
            && invocationOperation is IInvocationOperation operation)
        {
            method = operation.TargetMethod;
        }

        if (method is null)
        {
            return false;
        }

        IMethodSymbol definition = method.ReducedFrom ?? method;
        Compilation compilation = semanticModel.Compilation;
        return compilation.GetTypeByMetadataName(TextWriterMetadataName) is { } textWriter
            && compilation.GetTypeByMetadataName(TextWriterExtensionsMetadataName) is { } extensions
            && compilation.GetTypeByMetadataName(InterpolatedStringHandlerAttributeMetadataName)
                is { } handlerAttribute
            && SymbolEqualityComparer.Default.Equals(definition.ContainingType, extensions)
            && definition.IsExtensionMethod
            && definition.IsStatic
            && definition.ReturnsVoid
            && definition.Parameters.Length == 2
            && definition.Parameters[0].RefKind == RefKind.None
            && SymbolEqualityComparer.Default.Equals(definition.Parameters[0].Type, textWriter)
            && definition.Parameters[1].RefKind == RefKind.Ref
            && HasAttribute(definition.Parameters[1].Type, handlerAttribute);
    }

    private static async Task<bool> ReplacementPreservesDocumentAsync(
        SemanticModel originalSemanticModel,
        Document changedDocument,
        List<BindingSnapshot> bindingSnapshots,
        CancellationToken cancellationToken)
    {
        SemanticModel? changedSemanticModel =
            await changedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        SyntaxNode? changedRoot = await changedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (changedSemanticModel is null || changedRoot is null)
        {
            return false;
        }

        Dictionary<string, int> originalErrors = [];
        foreach (Diagnostic diagnostic in originalSemanticModel.GetDiagnostics(cancellationToken: cancellationToken))
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error)
            {
                string key = GetDiagnosticKey(diagnostic);
                originalErrors.TryGetValue(key, out int count);
                originalErrors[key] = count + 1;
            }
        }

        foreach (Diagnostic diagnostic in changedSemanticModel.GetDiagnostics(cancellationToken: cancellationToken))
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error)
            {
                continue;
            }

            string key = GetDiagnosticKey(diagnostic);
            if (!originalErrors.TryGetValue(key, out int count) || count == 0)
            {
                return false;
            }

            originalErrors[key] = count - 1;
        }

        foreach (BindingSnapshot snapshot in bindingSnapshots)
        {
            SyntaxNode? changedNode = null;
            foreach (SyntaxNode annotatedNode in changedRoot.GetAnnotatedNodes(snapshot.Annotation))
            {
                changedNode = annotatedNode;
                break;
            }

            ISymbol? changedSymbol = changedNode is null
                ? null
                : changedSemanticModel.GetSymbolInfo(changedNode, cancellationToken).Symbol;
            if (changedSymbol is null || GetSymbolIdentity(changedSymbol) != snapshot.SymbolIdentity)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetDiagnosticKey(Diagnostic diagnostic) =>
        $"{diagnostic.Id}\0{diagnostic.GetMessage()}";

    private static string GetSymbolIdentity(ISymbol symbol)
    {
        if (symbol is IAliasSymbol alias)
        {
            return $"Alias|{alias.Name}|{GetSymbolIdentity(alias.Target)}";
        }

        return $"{symbol.Kind}|{symbol.ContainingAssembly?.Identity}|"
            + symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool HasAttribute(ITypeSymbol type, INamedTypeSymbol attributeType)
    {
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
            {
                return true;
            }
        }

        return false;
    }

}