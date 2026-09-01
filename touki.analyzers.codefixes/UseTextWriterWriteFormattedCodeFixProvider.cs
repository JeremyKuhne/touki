// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Globalization;
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
    private const string InvocationAnnotationKind =
        "Touki.UseTextWriterWriteFormatted.Invocation";
    private const string BindingAnnotationKind =
        "Touki.UseTextWriterWriteFormatted.Binding";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [UseTextWriterWriteFormattedId];
    private static readonly FixAllProvider s_fixAllProvider = new WriteFormattedFixAllProvider();

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root =
            await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null
            || root.SyntaxTree.Options is not CSharpParseOptions parseOptions
            || parseOptions.LanguageVersion < LanguageVersion.CSharp10
            || DocumentFileUtilities.HasSharedFilePath(
                context.Document.Project.Solution,
                context.Document,
                context.CancellationToken))
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (FindInvocation(root, diagnostic.Location.SourceSpan) is not { } invocation)
            {
                continue;
            }

            Document? changedDocument = await TryUseWriteFormattedAsync(
                context.Document,
                [invocation.Span],
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

    private static InvocationExpressionSyntax? FindInvocation(SyntaxNode root, TextSpan diagnosticSpan)
    {
        SyntaxNode node = root.FindNode(diagnosticSpan, getInnermostNodeForTie: true);
        SimpleNameSyntax? methodName = node as SimpleNameSyntax ?? node.FirstAncestorOrSelf<SimpleNameSyntax>();
        return methodName?.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Parent as InvocationExpressionSyntax,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Parent as InvocationExpressionSyntax,
            _ => null
        };
    }

    private static async Task<Document?> TryUseWriteFormattedAsync(
        Document document,
        IReadOnlyCollection<TextSpan> invocationSpans,
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

        List<InvocationExpressionSyntax> invocations = new(invocationSpans.Count);
        HashSet<SyntaxNode> replacedBindingNodes = [];
        foreach (TextSpan invocationSpan in invocationSpans)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNode currentNode = compilationUnit.FindNode(invocationSpan, getInnermostNodeForTie: true);
            InvocationExpressionSyntax? invocation = currentNode as InvocationExpressionSyntax
                ?? currentNode.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation is null
                || invocation.Span != invocationSpan
                || !IsEligibleInvocation(invocation, originalSemanticModel, cancellationToken))
            {
                continue;
            }

            SimpleNameSyntax? originalMethodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
                _ => null
            };
            if (originalMethodName is null)
            {
                continue;
            }

            invocations.Add(invocation);
            replacedBindingNodes.Add(invocation);
            replacedBindingNodes.Add(originalMethodName);
            if (invocation.ArgumentList.Arguments[0].NameColon?.Name is { } originalArgumentName)
            {
                replacedBindingNodes.Add(originalArgumentName);
            }
        }

        if (invocations.Count == 0)
        {
            return null;
        }

        List<BindingSnapshot> bindingSnapshots = [];
        Dictionary<SyntaxNode, SyntaxAnnotation> bindingAnnotations = [];
        foreach (SyntaxNode descendant in compilationUnit.DescendantNodes(descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (descendant is not SimpleNameSyntax
                and not InvocationExpressionSyntax
                and not CrefSyntax
                || replacedBindingNodes.Contains(descendant))
            {
                continue;
            }

            ISymbol? symbol = originalSemanticModel.GetSymbolInfo(descendant, cancellationToken).Symbol;
            if (symbol is null)
            {
                continue;
            }

            SyntaxAnnotation annotation = CreateIndexedAnnotation(
                BindingAnnotationKind,
                bindingSnapshots.Count);
            bindingAnnotations.Add(descendant, annotation);
            bindingSnapshots.Add(new(GetSymbolIdentity(symbol)));
        }

        compilationUnit = compilationUnit.ReplaceNodes(
            bindingAnnotations.Keys,
            (original, rewritten) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return rewritten.WithAdditionalAnnotations(bindingAnnotations[original]);
            });

        Dictionary<InvocationExpressionSyntax, SyntaxAnnotation> invocationAnnotations = [];
        foreach (InvocationExpressionSyntax originalInvocation in invocations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SyntaxNode currentNode = compilationUnit.FindNode(
                originalInvocation.Span,
                getInnermostNodeForTie: true);
            InvocationExpressionSyntax? invocation = currentNode as InvocationExpressionSyntax
                ?? currentNode.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocation is null || invocation.Span != originalInvocation.Span)
            {
                continue;
            }

            SimpleNameSyntax? methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
                MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
                _ => null
            };
            if (methodName is null)
            {
                continue;
            }

            invocationAnnotations.Add(
                invocation,
                CreateIndexedAnnotation(
                    InvocationAnnotationKind,
                    invocationAnnotations.Count));
        }

        if (invocationAnnotations.Count == 0)
        {
            return null;
        }

        CompilationUnitSyntax updatedRoot = compilationUnit.ReplaceNodes(
            invocationAnnotations.Keys,
            (original, rewritten) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return RewriteInvocation(
                    (InvocationExpressionSyntax)rewritten,
                    invocationAnnotations[original]);
            });
        Document changedDocument = document.WithSyntaxRoot(updatedRoot);
        if (await ReplacementPreservesDocumentAsync(
            originalSemanticModel,
            changedDocument,
            invocationAnnotations.Count,
            bindingSnapshots,
            cancellationToken).ConfigureAwait(false))
        {
            return changedDocument;
        }

        UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(ExtensionNamespace))
            .WithAdditionalAnnotations(Formatter.Annotation);
        changedDocument = document.WithSyntaxRoot(updatedRoot.AddUsings(usingDirective));
        if (!await ReplacementPreservesDocumentAsync(
            originalSemanticModel,
            changedDocument,
            invocationAnnotations.Count,
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

    private static InvocationExpressionSyntax RewriteInvocation(
        InvocationExpressionSyntax invocation,
        SyntaxAnnotation invocationAnnotation)
    {
        SimpleNameSyntax? methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
            _ => null
        };
        if (methodName is null)
        {
            return invocation;
        }

        IdentifierNameSyntax replacementName = SyntaxFactory.IdentifierName(
            SyntaxFactory.Identifier(
                methodName.Identifier.LeadingTrivia,
                "WriteFormatted",
                methodName.Identifier.TrailingTrivia));
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
            replacement = replacement.ReplaceNode(
                argument,
                argument.WithNameColon(nameColon.WithName(builderName)));
        }

        return replacement;
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

    private static bool BindToTextWriterExtensions(
        SemanticModel semanticModel,
        SyntaxNode?[] invocationNodes,
        CancellationToken cancellationToken)
    {
        Compilation compilation = semanticModel.Compilation;
        if (compilation.GetTypeByMetadataName(TextWriterMetadataName) is not { } textWriter
            || compilation.GetTypeByMetadataName(TextWriterExtensionsMetadataName) is not { } extensions
            || compilation.GetTypeByMetadataName(InterpolatedStringHandlerAttributeMetadataName)
                is not { } handlerAttribute)
        {
            return false;
        }

        foreach (SyntaxNode? invocationNode in invocationNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (invocationNode is not InvocationExpressionSyntax invocation)
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
            if (!SymbolEqualityComparer.Default.Equals(definition.ContainingType, extensions)
                || !definition.IsExtensionMethod
                || !definition.IsStatic
                || !definition.ReturnsVoid
                || definition.Parameters.Length != 2
                || definition.Parameters[0].RefKind != RefKind.None
                || !SymbolEqualityComparer.Default.Equals(definition.Parameters[0].Type, textWriter)
                || definition.Parameters[1].RefKind != RefKind.Ref
                || !HasAttribute(definition.Parameters[1].Type, handlerAttribute))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> ReplacementPreservesDocumentAsync(
        SemanticModel originalSemanticModel,
        Document changedDocument,
        int invocationCount,
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

        if (!TryCollectAnnotatedNodes(
            changedRoot,
            invocationCount,
            bindingSnapshots.Count,
            cancellationToken,
            out SyntaxNode?[] invocationNodes,
            out SyntaxNode?[] bindingNodes)
            || !BindToTextWriterExtensions(
                changedSemanticModel,
                invocationNodes,
                cancellationToken))
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

        for (int index = 0; index < bindingSnapshots.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ISymbol? changedSymbol = bindingNodes[index] is not { } changedNode
                ? null
                : changedSemanticModel.GetSymbolInfo(changedNode, cancellationToken).Symbol;
            if (changedSymbol is null
                || GetSymbolIdentity(changedSymbol) != bindingSnapshots[index].SymbolIdentity)
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectAnnotatedNodes(
        SyntaxNode root,
        int invocationCount,
        int bindingCount,
        CancellationToken cancellationToken,
        out SyntaxNode?[] invocationNodes,
        out SyntaxNode?[] bindingNodes)
    {
        invocationNodes = new SyntaxNode?[invocationCount];
        bindingNodes = new SyntaxNode?[bindingCount];
        int foundInvocationCount = 0;
        int foundBindingCount = 0;
        foreach (SyntaxNode node in root.DescendantNodesAndSelf(descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryCollectAnnotations(
                node,
                InvocationAnnotationKind,
                invocationNodes,
                ref foundInvocationCount,
                cancellationToken)
                || !TryCollectAnnotations(
                    node,
                    BindingAnnotationKind,
                    bindingNodes,
                    ref foundBindingCount,
                    cancellationToken))
            {
                return false;
            }
        }

        return foundInvocationCount == invocationNodes.Length
            && foundBindingCount == bindingNodes.Length;
    }

    private static bool TryCollectAnnotations(
        SyntaxNode node,
        string annotationKind,
        SyntaxNode?[] nodes,
        ref int foundCount,
        CancellationToken cancellationToken)
    {
        foreach (SyntaxAnnotation annotation in node.GetAnnotations(annotationKind))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!int.TryParse(
                annotation.Data,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int index)
                || index < 0
                || index >= nodes.Length
                || nodes[index] is not null)
            {
                return false;
            }

            nodes[index] = node;
            foundCount++;
        }

        return true;
    }

    private static SyntaxAnnotation CreateIndexedAnnotation(string kind, int index) =>
        new(kind, index.ToString(CultureInfo.InvariantCulture));

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