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
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DefensiveCopyId, NonCopyableDefensiveCopyId);

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
        Solution updatedSolution = solution;

        foreach (SyntaxReference reference in member.DeclaringSyntaxReferences)
        {
            Document? document = updatedSolution.GetDocument(reference.SyntaxTree);
            if (document is null)
            {
                continue;
            }

            SyntaxNode declaration = await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false);
            SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                continue;
            }

            SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
            DeclarationModifiers modifiers = generator.GetModifiers(declaration);
            if (modifiers.IsReadOnly)
            {
                continue;
            }

            SyntaxNode updatedDeclaration = generator.WithModifiers(declaration, modifiers.WithIsReadOnly(true));
            updatedSolution = updatedSolution.WithDocumentSyntaxRoot(
                document.Id,
                root.ReplaceNode(declaration, updatedDeclaration));
        }

        return updatedSolution;
    }
}
