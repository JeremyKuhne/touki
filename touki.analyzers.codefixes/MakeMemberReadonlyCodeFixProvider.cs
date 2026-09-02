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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Touki.Analyzers;

/// <summary>
///  Offers a "Make '{member}' readonly" fix for the defensive-copy diagnostics (<c>TOUKI0002</c> and
///  <c>TOUKI0003</c>). Marking the accessed instance member <see langword="readonly"/> is the usual remedy: it
///  tells the compiler the member does not mutate, so no defensive copy is needed.
/// </summary>
/// <remarks>
///  <para>
///   The fix is only offered when the member is declared in source. If the member actually mutates state, marking
///   it <see langword="readonly"/> produces a compiler error the developer can act on; the analyzer cannot prove
///   non-mutation cheaply, so it defers that judgment.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MakeMemberReadonlyCodeFixProvider))]
[Shared]
public sealed partial class MakeMemberReadonlyCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; these ids are a stable public contract.
    private const string DefensiveCopyId = "TOUKI0002";
    private const string NonCopyableDefensiveCopyId = "TOUKI0003";
    private const string EquivalenceKey = "MakeMemberReadonly";

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds =
        [DefensiveCopyId, NonCopyableDefensiveCopyId];
    private static readonly FixAllProvider s_fixAllProvider = new MakeMemberReadonlyFixAllProvider();

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SemanticModel? semanticModel =
            await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        SyntaxNode? root =
            await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null || root is null)
        {
            return;
        }

        HashSet<DocumentId> sharedDocuments = IndexSharedDocuments(
            context.Document.Project.Solution,
            context.CancellationToken);

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            ISymbol? member = TryGetAccessedMember(
                node,
                diagnostic.Location.SourceSpan,
                semanticModel,
                context.CancellationToken);
            Dictionary<DocumentId, HashSet<TextSpan>> declarationsByDocument = [];
            if (member is null
                || await ResolveSourceMemberAsync(
                    context.Document.Project.Solution,
                    member,
                    context.CancellationToken).ConfigureAwait(false) is not { } sourceMember
                || !TryCollectDeclarations(
                    context.Document.Project.Solution,
                    sourceMember,
                    sharedDocuments,
                    declarationsByDocument,
                    context.CancellationToken))
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Make '{member.Name}' readonly",
                    createChangedSolution: cancellationToken =>
                        MakeMembersReadonlyAsync(
                            context.Document.Project.Solution,
                            declarationsByDocument,
                            cancellationToken),
                    equivalenceKey: EquivalenceKey),
                diagnostic);
        }
    }

    private static ISymbol? TryGetAccessedMember(
        SyntaxNode node,
        TextSpan diagnosticSpan,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        // The diagnostic is reported on the receiver; the accessed member is on the enclosing access expression.
        for (SyntaxNode? current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case InvocationExpressionSyntax invocation
                    when semanticModel.GetOperation(invocation, cancellationToken) is IInvocationOperation operation
                        && operation.Instance?.Syntax.Span == diagnosticSpan
                        && IsCurrentDefensiveCopyAccess(
                        operation.Instance,
                        operation.TargetMethod,
                        semanticModel.GetEnclosingSymbol(invocation.SpanStart, cancellationToken)):
                    return operation.TargetMethod;
                case ElementAccessExpressionSyntax elementAccess
                    when semanticModel.GetOperation(elementAccess, cancellationToken)
                        is IPropertyReferenceOperation operation
                        && operation.Instance?.Syntax.Span == diagnosticSpan
                        && IsCurrentDefensiveCopyAccess(
                        operation.Instance,
                        operation.Property,
                        semanticModel.GetEnclosingSymbol(elementAccess.SpanStart, cancellationToken)):
                    return operation.Property;
                case MemberAccessExpressionSyntax memberAccess
                    when semanticModel.GetOperation(memberAccess, cancellationToken)
                        is IPropertyReferenceOperation operation
                        && operation.Instance?.Syntax.Span == diagnosticSpan
                        && IsCurrentDefensiveCopyAccess(
                        operation.Instance,
                        operation.Property,
                        semanticModel.GetEnclosingSymbol(memberAccess.SpanStart, cancellationToken)):
                    return operation.Property;
                case StatementSyntax:
                    return null;
            }
        }

        return null;
    }

    private static async Task<ISymbol?> ResolveSourceMemberAsync(
        Solution solution,
        ISymbol member,
        CancellationToken cancellationToken)
    {
        member = member.OriginalDefinition;
        foreach (SyntaxReference reference in member.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? document = GetOrdinaryDocument(solution, reference.SyntaxTree);
            if (document is null)
            {
                continue;
            }

            SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode declaration = await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
            ISymbol? sourceMember = semanticModel?.GetDeclaredSymbol(declaration, cancellationToken);
            if (sourceMember is not null)
            {
                return sourceMember.OriginalDefinition;
            }
        }

        return null;
    }

    private static bool IsCurrentDefensiveCopyAccess(
        IOperation? receiver,
        ISymbol member,
        ISymbol? containingSymbol)
    {
        if (receiver?.Type is not
            {
                IsValueType: true,
                IsReadOnly: false,
                TypeKind: TypeKind.Struct
            }
            || member.IsStatic
            || !IsReadOnlyReceiver(receiver, containingSymbol))
        {
            return false;
        }

        return member switch
        {
            IMethodSymbol method => !method.IsReadOnly,
            IPropertySymbol property => property.GetMethod is { IsReadOnly: false } && property.SetMethod is null,
            _ => false
        };
    }

    private static bool IsReadOnlyReceiver(IOperation receiver, ISymbol? containingSymbol) => receiver switch
    {
        IParameterReferenceOperation parameter =>
            parameter.Parameter.RefKind is RefKind.In or RefKind.RefReadOnlyParameter,
        IFieldReferenceOperation field =>
            field.Field.IsReadOnly && !IsInsideInitializingConstructor(field.Field, containingSymbol)
                || field.Instance is not null && IsReadOnlyReceiver(field.Instance, containingSymbol),
        ILocalReferenceOperation local => local.Local.RefKind == RefKind.RefReadOnly,
        IInvocationOperation invocation => invocation.TargetMethod.ReturnsByRefReadonly,
        IPropertyReferenceOperation property => property.Property.ReturnsByRefReadonly,
        _ => false
    };

    private static bool IsInsideInitializingConstructor(IFieldSymbol field, ISymbol? containingSymbol)
    {
        if (containingSymbol is not IMethodSymbol method)
        {
            return false;
        }

        bool isMatchingConstructor = field.IsStatic
            ? method.MethodKind == MethodKind.StaticConstructor
            : method.MethodKind == MethodKind.Constructor;
        return isMatchingConstructor
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, field.ContainingType);
    }

    private static bool TryCollectDeclarations(
        Solution solution,
        ISymbol member,
        ISet<DocumentId> sharedDocuments,
        Dictionary<DocumentId, HashSet<TextSpan>> declarationsByDocument,
        CancellationToken cancellationToken)
    {
        member = member.OriginalDefinition;

        // A member-level 'readonly' modifier also applies to a property/indexer's set or init accessor, which is
        // a compiler error even when the getter is non-mutating. Only offer the fix for read-only properties
        // (get-only / expression-bodied); a getter-only readonly edit would be a future enhancement.
        if (member.DeclaringSyntaxReferences.IsEmpty || member is IPropertySymbol { SetMethod: not null })
        {
            return false;
        }

        HashSet<ISymbol> members = new(SymbolEqualityComparer.Default) { member };

        if (member is IMethodSymbol method)
        {
            if (method.PartialDefinitionPart is { } definition)
            {
                members.Add(definition);
            }

            if (method.PartialImplementationPart is { } implementation)
            {
                members.Add(implementation);
            }
        }
        else if (member is IPropertySymbol property)
        {
            if (property.PartialDefinitionPart is { } definition)
            {
                members.Add(definition);
            }

            if (property.PartialImplementationPart is { } implementation)
            {
                members.Add(implementation);
            }
        }

        List<(DocumentId DocumentId, TextSpan Span)> declarations = [];
        foreach (ISymbol declaredMember in members)
        {
            foreach (SyntaxReference reference in declaredMember.DeclaringSyntaxReferences)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Document? document = GetOrdinaryDocument(solution, reference.SyntaxTree);
                if (document is null
                    || sharedDocuments.Contains(document.Id)
                    || reference.SyntaxTree.Options is not CSharpParseOptions
                    {
                        LanguageVersion: >= LanguageVersion.CSharp8
                    })
                {
                    return false;
                }

                declarations.Add((document.Id, reference.Span));
            }
        }

        if (declarations.Count == 0)
        {
            return false;
        }

        foreach ((DocumentId documentId, TextSpan span) in declarations)
        {
            if (!declarationsByDocument.TryGetValue(documentId, out HashSet<TextSpan>? declarationSpans))
            {
                declarationSpans = [];
                declarationsByDocument.Add(documentId, declarationSpans);
            }

            declarationSpans.Add(span);
        }

        return true;
    }

    private static Document? GetOrdinaryDocument(Solution solution, SyntaxTree syntaxTree)
    {
        foreach (Project project in solution.Projects)
        {
            if (project.GetDocument(syntaxTree) is { } candidate)
            {
                return candidate is SourceGeneratedDocument ? null : candidate;
            }
        }

        return null;
    }

    private static async Task<Solution> MakeMembersReadonlyAsync(
        Solution solution,
        IReadOnlyDictionary<DocumentId, HashSet<TextSpan>> declarationsByDocument,
        CancellationToken cancellationToken)
    {
        Solution updatedSolution = solution;

        foreach (KeyValuePair<DocumentId, HashSet<TextSpan>> entry in declarationsByDocument)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document? document = updatedSolution.GetDocument(entry.Key);
            if (document is null)
            {
                continue;
            }

            SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                continue;
            }

            List<SyntaxNode> declarations = new(entry.Value.Count);
            foreach (TextSpan span in entry.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SyntaxNode declaration = root.FindNode(span, getInnermostNodeForTie: true);
                if (declaration.Span == span)
                {
                    declarations.Add(declaration);
                }
            }

            if (declarations.Count == 0)
            {
                continue;
            }

            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
            SyntaxNode updatedRoot = root.ReplaceNodes(
                declarations,
                (_, rewritten) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return MakeReadonly(rewritten, generator);
                });
            cancellationToken.ThrowIfCancellationRequested();
            updatedSolution = updatedSolution.WithDocumentSyntaxRoot(
                document.Id,
                updatedRoot);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return updatedSolution;
    }

    private static SyntaxNode MakeReadonly(SyntaxNode declaration, SyntaxGenerator generator)
    {
        return declaration switch
        {
            MethodDeclarationSyntax method => method.WithModifiers(AddReadonly(method.Modifiers)),
            PropertyDeclarationSyntax property => property.WithModifiers(AddReadonly(property.Modifiers)),
            IndexerDeclarationSyntax indexer => indexer.WithModifiers(AddReadonly(indexer.Modifiers)),
            _ => generator.WithModifiers(
                declaration,
                generator.GetModifiers(declaration).WithIsReadOnly(true))
        };
    }

    private static SyntaxTokenList AddReadonly(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.ReadOnlyKeyword))
        {
            return modifiers;
        }

        for (int index = 0; index < modifiers.Count; index++)
        {
            if (modifiers[index].IsKind(SyntaxKind.PartialKeyword))
            {
                return modifiers.Insert(index, SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
            }
        }

        return modifiers.Add(SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
    }

    private static HashSet<DocumentId> IndexSharedDocuments(
        Solution solution,
        CancellationToken cancellationToken) =>
        DocumentFileUtilities.IndexSharedDocuments(
            solution,
            cancellationToken);
}
