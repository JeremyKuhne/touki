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
public sealed class MakeMemberReadonlyCodeFixProvider : CodeFixProvider
{
    // Hardcoded to avoid a dependency on the analyzer assembly; these ids are a stable public contract.
    private const string DefensiveCopyId = "TOUKI0002";
    private const string NonCopyableDefensiveCopyId = "TOUKI0003";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => [DefensiveCopyId, NonCopyableDefensiveCopyId];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

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

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            ISymbol? member = TryGetAccessedMember(node, semanticModel, context.CancellationToken);

            // Only members declared in source can be edited.
            if (member is null || member.DeclaringSyntaxReferences.IsEmpty)
            {
                continue;
            }

            // A member-level 'readonly' modifier also applies to a property/indexer's set or init accessor, which is
            // a compiler error even when the getter is non-mutating. Only offer the fix for read-only properties
            // (get-only / expression-bodied); a getter-only readonly edit would be a future enhancement.
            if (member is IPropertySymbol { SetMethod: not null })
            {
                continue;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Make '{member.Name}' readonly",
                    createChangedSolution: cancellationToken =>
                        MakeMemberReadonlyAsync(context.Document.Project.Solution, member, cancellationToken),
                    equivalenceKey: "MakeMemberReadonly"),
                diagnostic);
        }
    }

    private static ISymbol? TryGetAccessedMember(SyntaxNode node, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        // The diagnostic is reported on the receiver; the accessed member is on the enclosing access expression.
        for (SyntaxNode? current = node; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case InvocationExpressionSyntax invocation:
                    return semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol;
                case ElementAccessExpressionSyntax elementAccess:
                    return semanticModel.GetSymbolInfo(elementAccess, cancellationToken).Symbol;
                case MemberAccessExpressionSyntax memberAccess:
                    return semanticModel.GetSymbolInfo(memberAccess, cancellationToken).Symbol;
                case StatementSyntax:
                    return null;
            }
        }

        return null;
    }

    private static async Task<Solution> MakeMemberReadonlyAsync(Solution solution, ISymbol member, CancellationToken cancellationToken)
    {
        Dictionary<DocumentId, List<SyntaxNode>> declarationsByDocument = [];
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

        foreach (ISymbol declaredMember in members)
        {
            foreach (SyntaxReference reference in declaredMember.DeclaringSyntaxReferences)
            {
                Document? document = solution.GetDocument(reference.SyntaxTree);
                if (document is null)
                {
                    continue;
                }

                SyntaxNode declaration = await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
                if (!declarationsByDocument.TryGetValue(document.Id, out List<SyntaxNode>? declarations))
                {
                    declarations = [];
                    declarationsByDocument.Add(document.Id, declarations);
                }

                if (!declarations.Contains(declaration))
                {
                    declarations.Add(declaration);
                }
            }
        }

        Solution updatedSolution = solution;

        foreach (KeyValuePair<DocumentId, List<SyntaxNode>> entry in declarationsByDocument)
        {
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

            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
            updatedSolution = updatedSolution.WithDocumentSyntaxRoot(
                document.Id,
                root.ReplaceNodes(
                    entry.Value,
                    (_, rewritten) => MakeReadonly(rewritten, generator)));
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
}
