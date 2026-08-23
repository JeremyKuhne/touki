// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Use Path.Join" fix for <c>TOUKI0032</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UsePathJoinCodeFixProvider))]
[Shared]
public sealed class UsePathJoinCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; this is a stable public contract.
    private const string UsePathJoinId = "TOUKI0032";
    private const string SystemPathMetadataName = "System.IO.Path";
    private const string RedistPathMetadataName = "Microsoft.IO.Path";
    private const string RedistAssemblyName = "Microsoft.IO.Redist";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [UsePathJoinId];

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null
            || context.Document.FilePath is not null
                && DocumentFileUtilities.HasSharedFilePath(context.Document.Project.Solution, context.Document))
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (diagnostic.Location.SourceTree != root.SyntaxTree)
            {
                continue;
            }

            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            InvocationExpressionSyntax? invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            SimpleNameSyntax? methodName = invocation is null ? null : GetMethodName(invocation.Expression);
            if (invocation is null || methodName is null || diagnostic.Location.SourceSpan != methodName.Span)
            {
                continue;
            }

            Document? changedDocument = await TryUsePathJoinAsync(
                context.Document,
                invocation.Span,
                context.CancellationToken).ConfigureAwait(false);
            if (changedDocument is null)
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use Path.Join",
                    createChangedDocument: _ => Task.FromResult(changedDocument),
                    equivalenceKey: nameof(UsePathJoinCodeFixProvider)),
                diagnostic);
        }
    }

    private static async Task<Document?> TryUsePathJoinAsync(
        Document document,
        TextSpan invocationSpan,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return null;
        }

        SyntaxNode node = root.FindNode(invocationSpan, getInnermostNodeForTie: true);
        InvocationExpressionSyntax? invocation = node as InvocationExpressionSyntax
            ?? node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null
            || invocation.Span != invocationSpan
            || semanticModel.GetOperation(invocation, cancellationToken) is not IInvocationOperation operation
            || operation.TargetMethod.Name != "Combine"
            || !operation.TargetMethod.IsStatic
            || semanticModel.Compilation.GetTypeByMetadataName(SystemPathMetadataName) is not { } systemPath)
        {
            return null;
        }

        INamedTypeSymbol? redistPath = semanticModel.Compilation.GetTypeByMetadataName(RedistPathMetadataName);
        if (redistPath is not null && !IsRedistPath(redistPath))
        {
            redistPath = null;
        }

        bool combinesWithSystemPath =
            SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, systemPath);
        bool combinesWithRedistPath =
            SymbolEqualityComparer.Default.Equals(operation.TargetMethod.ContainingType, redistPath);
        if (!combinesWithSystemPath && !combinesWithRedistPath)
        {
            return null;
        }

        if (combinesWithRedistPath)
        {
            if (redistPath is null || !HasPublicJoin(redistPath))
            {
                return null;
            }

            ExpressionSyntax? redistExpression = RenameMethod(invocation.Expression);
            if (redistExpression is not null)
            {
                Document? renamedDocument = await TryRewriteAsync(
                    document,
                    root,
                    invocation,
                    redistExpression,
                    RedistPathMetadataName,
                    cancellationToken).ConfigureAwait(false);
                if (renamedDocument is not null)
                {
                    return renamedDocument;
                }
            }

            if (ContainsSignificantTrivia(invocation.Expression))
            {
                return null;
            }

            return await TryRewriteAsync(
                document,
                root,
                invocation,
                SyntaxFactory.ParseExpression("global::Microsoft.IO.Path.Join")
                    .WithTriviaFrom(invocation.Expression),
                RedistPathMetadataName,
                cancellationToken).ConfigureAwait(false);
        }

        bool systemPathHasJoin = HasPublicJoin(systemPath);
        if (!systemPathHasJoin
            && redistPath is not null
            && HasPublicJoin(redistPath)
            && !ContainsSignificantTrivia(invocation.Expression))
        {
            Document? redistDocument = await TryRewriteAsync(
                document,
                root,
                invocation,
                SyntaxFactory.ParseExpression("global::Microsoft.IO.Path.Join").WithTriviaFrom(invocation.Expression),
                RedistPathMetadataName,
                cancellationToken).ConfigureAwait(false);
            if (redistDocument is not null)
            {
                return redistDocument;
            }
        }

        if (!systemPathHasJoin)
        {
            return null;
        }

        ExpressionSyntax? renamedExpression = RenameMethod(invocation.Expression);
        if (renamedExpression is not null)
        {
            Document? renamedDocument = await TryRewriteAsync(
                document,
                root,
                invocation,
                renamedExpression,
                SystemPathMetadataName,
                cancellationToken).ConfigureAwait(false);
            if (renamedDocument is not null)
            {
                return renamedDocument;
            }
        }

        if (ContainsSignificantTrivia(invocation.Expression))
        {
            return null;
        }

        return await TryRewriteAsync(
            document,
            root,
            invocation,
            SyntaxFactory.ParseExpression("global::System.IO.Path.Join").WithTriviaFrom(invocation.Expression),
            SystemPathMetadataName,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Document?> TryRewriteAsync(
        Document document,
        SyntaxNode root,
        InvocationExpressionSyntax invocation,
        ExpressionSyntax replacementExpression,
        string expectedPathMetadataName,
        CancellationToken cancellationToken)
    {
        SyntaxAnnotation annotation = new();
        InvocationExpressionSyntax replacement = invocation
            .WithExpression(replacementExpression)
            .WithAdditionalAnnotations(annotation);
        Document changedDocument = document.WithSyntaxRoot(root.ReplaceNode(invocation, replacement));
        SyntaxNode? changedRoot = await changedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        SemanticModel? changedSemanticModel =
            await changedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (changedRoot is null || changedSemanticModel is null)
        {
            return null;
        }

        InvocationExpressionSyntax? changedInvocation = null;
        foreach (SyntaxNode annotatedNode in changedRoot.GetAnnotatedNodes(annotation))
        {
            if (annotatedNode is InvocationExpressionSyntax candidate)
            {
                changedInvocation = candidate;
                break;
            }
        }

        if (changedInvocation is null
            || changedSemanticModel.GetOperation(changedInvocation, cancellationToken)
                is not IInvocationOperation changedOperation
            || changedSemanticModel.Compilation.GetTypeByMetadataName(expectedPathMetadataName)
                is not { } expectedPath
            || expectedPathMetadataName == RedistPathMetadataName && !IsRedistPath(expectedPath)
            || changedOperation.TargetMethod.Name != "Join"
            || !changedOperation.TargetMethod.IsStatic
            || !SymbolEqualityComparer.Default.Equals(changedOperation.TargetMethod.ContainingType, expectedPath))
        {
            return null;
        }

        return changedDocument;
    }

    private static ExpressionSyntax? RenameMethod(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case SimpleNameSyntax simpleName:
                return SyntaxFactory.IdentifierName(
                    SyntaxFactory.Identifier(
                        simpleName.Identifier.LeadingTrivia,
                        "Join",
                        simpleName.Identifier.TrailingTrivia));
            case MemberAccessExpressionSyntax memberAccess:
                IdentifierNameSyntax join = SyntaxFactory.IdentifierName(
                    SyntaxFactory.Identifier(
                        memberAccess.Name.Identifier.LeadingTrivia,
                        "Join",
                        memberAccess.Name.Identifier.TrailingTrivia));
                return memberAccess.WithName(join);
            default:
                return null;
        }
    }

    private static SimpleNameSyntax? GetMethodName(ExpressionSyntax expression) => expression switch
    {
        SimpleNameSyntax simpleName => simpleName,
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
        _ => null
    };

    private static bool IsRedistPath(INamedTypeSymbol path)
    {
        AssemblyIdentity identity = path.ContainingAssembly.Identity;
        return identity.Name == RedistAssemblyName
            && identity.PublicKeyToken.Length == 8
            && identity.PublicKeyToken[0] == 0xcc
            && identity.PublicKeyToken[1] == 0x7b
            && identity.PublicKeyToken[2] == 0x13
            && identity.PublicKeyToken[3] == 0xff
            && identity.PublicKeyToken[4] == 0xcd
            && identity.PublicKeyToken[5] == 0x2d
            && identity.PublicKeyToken[6] == 0xdd
            && identity.PublicKeyToken[7] == 0x51;
    }

    private static bool ContainsSignificantTrivia(ExpressionSyntax expression)
    {
        foreach (SyntaxTrivia trivia in expression.DescendantTrivia(descendIntoTrivia: true))
        {
            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia)
                && !trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasPublicJoin(INamedTypeSymbol path)
    {
        foreach (ISymbol member in path.GetMembers("Join"))
        {
            if (member is IMethodSymbol
                {
                    IsStatic: true,
                    DeclaredAccessibility: Accessibility.Public
                })
            {
                return true;
            }
        }

        return false;
    }
}