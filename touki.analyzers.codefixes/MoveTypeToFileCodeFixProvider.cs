// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Editing;

namespace Touki.Analyzers;

/// <summary>
///  Offers a move-to-file fix for extra declarations reported by <c>TOUKI0020</c>.
/// </summary>
/// <remarks>
///  <para>
///   Nested types remain nested. Their containing types are repeated as partial shells with the original
///   modifiers and type parameters, while attributes, inheritance, constraints, and primary constructors stay
///   on the original declaration.
///  </para>
///  <para>
///   Solution-wide Fix All is not offered in <c>MSBuildWorkspace</c>, used by <c>dotnet format</c>, because that
///   workspace persists added source documents as explicit compile items that collide with SDK default globs.
///  </para>
///  <para>
///   No fix is offered for a source file containing directives, a file-local declaration, or a declaration that
///   references a file-local type. Moving any of those independently can change preprocessing or symbol binding.
///  </para>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MoveTypeToFileCodeFixProvider))]
[Shared]
public sealed partial class MoveTypeToFileCodeFixProvider : CodeFixProvider
{
    private const string OneTypePerFileId = "TOUKI0020";
    private const string DetailSeparatorsOption = "dotnet_code_quality.TOUKI0021.file_name_detail_separators";
    private const string DefaultDetailSeparators = ".-_";
    private const string EquivalenceKey = nameof(MoveTypeToFileCodeFixProvider);

    private static readonly ImmutableArray<string> s_fixableDiagnosticIds = [OneTypePerFileId];
    private static readonly FixAllProvider s_fixAllProvider = new MoveTypeFixAllProvider();
    private static readonly SyntaxAnnotation s_madePartialByMove = new(nameof(s_madePartialByMove));

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => s_fixableDiagnosticIds;

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => s_fixAllProvider;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        if (await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false)
            is not CompilationUnitSyntax root)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            MemberDeclarationSyntax? declaration = FindDeclaration(root, diagnostic.Location.SourceSpan.Start);
            if (declaration is null
                || !await CanMoveAsync(
                    context.Document,
                    root,
                    declaration,
                    context.CancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            char detailSeparator = GetDetailSeparator(context.Document, root.SyntaxTree);
            string fileName = GetAvailableFileName(
                context.Document.Project.Solution,
                context.Document,
                declaration,
                detailSeparator);

            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Move type to '{fileName}'",
                    cancellationToken => MoveAsync(
                        context.Document,
                        declaration,
                        fileName,
                        cancellationToken),
                    EquivalenceKey),
                diagnostic);
        }
    }

    private static MemberDeclarationSyntax? FindDeclaration(CompilationUnitSyntax root, int position)
    {
        for (SyntaxNode? node = root.FindToken(position).Parent; node is not null; node = node.Parent)
        {
            if (node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax)
            {
                return (MemberDeclarationSyntax)node;
            }
        }

        return null;
    }

    private static async Task<bool> CanMoveAsync(
        Document document,
        CompilationUnitSyntax root,
        MemberDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        if (document.FilePath is null
            || document.Project.Solution.Workspace.Kind == WorkspaceKind.MSBuild
            || !document.Project.Solution.Workspace.CanApplyChange(ApplyChangesKind.AddDocument)
            || !document.Project.Solution.Workspace.CanApplyChange(ApplyChangesKind.ChangeDocument)
            || DocumentFileUtilities.HasSharedFilePath(document.Project.Solution, document)
            || ContainsDirective(root)
            || ContainsFileLocalDeclaration(root))
        {
            return false;
        }

        for (SyntaxNode? node = declaration; node is not null; node = node.Parent)
        {
            if (node is MemberDeclarationSyntax memberDeclaration
                && HasFileModifier(memberDeclaration))
            {
                return false;
            }

            if (node is CompilationUnitSyntax)
            {
                break;
            }
        }

        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel is null
            || ContainsFileLocalType(semanticModel.GetDeclaredSymbol(declaration, cancellationToken)))
        {
            return false;
        }

        foreach (SyntaxNode node in declaration.DescendantNodes())
        {
            if (node is not SimpleNameSyntax name)
            {
                continue;
            }

            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(name, cancellationToken);
            if (ContainsFileLocalType(symbolInfo.Symbol))
            {
                return false;
            }

            foreach (ISymbol candidate in symbolInfo.CandidateSymbols)
            {
                if (ContainsFileLocalType(candidate))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ContainsDirective(CompilationUnitSyntax root)
    {
        foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.IsDirective)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsFileLocalDeclaration(CompilationUnitSyntax root)
    {
        foreach (SyntaxNode node in root.DescendantNodes())
        {
            if (node is MemberDeclarationSyntax declaration && HasFileModifier(declaration))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasFileModifier(MemberDeclarationSyntax declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax typeDeclaration => typeDeclaration.Modifiers.Any(SyntaxKind.FileKeyword),
        DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Modifiers.Any(SyntaxKind.FileKeyword),
        _ => false
    };

    private static bool ContainsFileLocalType(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return false;
        }

        if (symbol is IAliasSymbol alias)
        {
            return ContainsFileLocalType(alias.Target);
        }

        if (ContainsFileLocalType(symbol.ContainingType))
        {
            return true;
        }

        return symbol switch
        {
            IEventSymbol eventSymbol => ContainsFileLocalType(eventSymbol.Type),
            IFieldSymbol fieldSymbol => ContainsFileLocalType(fieldSymbol.Type),
            ILocalSymbol localSymbol => ContainsFileLocalType(localSymbol.Type),
            IMethodSymbol methodSymbol => ContainsFileLocalType(methodSymbol.ReturnType)
                || ContainsFileLocalType(methodSymbol.Parameters),
            INamedTypeSymbol namedType => ContainsFileLocalType((ITypeSymbol)namedType),
            IParameterSymbol parameterSymbol => ContainsFileLocalType(parameterSymbol.Type),
            IPropertySymbol propertySymbol => ContainsFileLocalType(propertySymbol.Type),
            _ => false
        };
    }

    private static bool ContainsFileLocalType(ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (IParameterSymbol parameter in parameters)
        {
            if (ContainsFileLocalType(parameter.Type))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsFileLocalType(ITypeSymbol? type)
    {
        switch (type)
        {
            case null:
                return false;
            case IArrayTypeSymbol arrayType:
                return ContainsFileLocalType(arrayType.ElementType);
            case INamedTypeSymbol namedType:
                if (namedType.IsFileLocal)
                {
                    return true;
                }

                foreach (ITypeSymbol typeArgument in namedType.TypeArguments)
                {
                    if (ContainsFileLocalType(typeArgument))
                    {
                        return true;
                    }
                }

                return false;
            case IPointerTypeSymbol pointerType:
                return ContainsFileLocalType(pointerType.PointedAtType);
            default:
                return false;
        }
    }

    private static char GetDetailSeparator(Document document, SyntaxTree syntaxTree)
    {
        AnalyzerConfigOptions options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);
        string separators = options.TryGetValue(DetailSeparatorsOption, out string? configuredSeparators)
            && !string.IsNullOrWhiteSpace(configuredSeparators)
                ? configuredSeparators!.Trim()
                : DefaultDetailSeparators;

        foreach (char separator in separators)
        {
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), separator) < 0)
            {
                return separator;
            }
        }

        return '.';
    }

    private static string GetAvailableFileName(
        Solution solution,
        Document document,
        MemberDeclarationSyntax declaration,
        char detailSeparator)
    {
        string extension = Path.GetExtension(document.FilePath!);
        string typeName = GetIdentifier(declaration).ValueText;
        string simpleCandidate = typeName + extension;
        if (IsDestinationAvailable(solution, document, simpleCandidate))
        {
            return simpleCandidate;
        }

        string qualifiedStem = GetQualifiedTypeStem(declaration);
        string qualifiedCandidate = qualifiedStem + extension;
        if (!DocumentFileUtilities.PathComparer.Equals(simpleCandidate, qualifiedCandidate)
            && IsDestinationAvailable(solution, document, qualifiedCandidate))
        {
            return qualifiedCandidate;
        }

        string currentStem = Path.GetFileNameWithoutExtension(document.FilePath!);
        string detailStem = DocumentFileUtilities.PathComparer.Equals(qualifiedStem, currentStem)
            ? qualifiedStem
            : $"{qualifiedStem}{detailSeparator}{currentStem}";
        string detailCandidate = detailStem + extension;
        if (IsDestinationAvailable(solution, document, detailCandidate))
        {
            return detailCandidate;
        }

        for (int suffix = 2; ; suffix++)
        {
            string candidate = $"{detailStem}{detailSeparator}{suffix}{extension}";
            if (IsDestinationAvailable(solution, document, candidate))
            {
                return candidate;
            }
        }
    }

    private static string GetQualifiedTypeStem(MemberDeclarationSyntax declaration)
    {
        Stack<string> names = new();
        names.Push(GetIdentifier(declaration).ValueText);

        foreach (SyntaxNode ancestor in declaration.Ancestors())
        {
            if (ancestor is TypeDeclarationSyntax typeDeclaration)
            {
                names.Push(typeDeclaration.Identifier.ValueText);
            }
        }

        return string.Join(".", names);
    }

    private static bool IsDestinationAvailable(Solution solution, Document document, string fileName)
    {
        string targetFilePath = DocumentFileUtilities.GetTargetFilePath(document, fileName)!;
        return !DocumentFileUtilities.HasDocumentWithFilePath(solution, targetFilePath)
            && DocumentFileUtilities.IsFileSystemDestinationAvailable(document.FilePath!, targetFilePath);
    }

    private static async Task<Solution> MoveAsync(
        Document document,
        MemberDeclarationSyntax declaration,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false)
            is not CompilationUnitSyntax root)
        {
            return document.Project.Solution;
        }

        SyntaxGenerator generator = SyntaxGenerator.GetGenerator(document);
        ImmutableArray<MemberDeclarationSyntax> declarations =
            await GetDeclarationsToMoveAsync(document, root, declaration, cancellationToken).ConfigureAwait(false);
        CompilationUnitSyntax destinationRoot = CreateDestinationRoot(root, declarations, generator);
        CompilationUnitSyntax sourceRoot = CreateSourceRoot(root, declarations, generator);
        string? targetFilePath = DocumentFileUtilities.GetTargetFilePath(document, fileName);
        if (targetFilePath is null)
        {
            return document.Project.Solution;
        }

        Document destinationDocument = document.Project.AddDocument(
            fileName,
            destinationRoot,
            document.Folders,
            targetFilePath);
        return destinationDocument.Project.Solution.WithDocumentSyntaxRoot(document.Id, sourceRoot);
    }

    private static async Task<ImmutableArray<MemberDeclarationSyntax>> GetDeclarationsToMoveAsync(
        Document document,
        CompilationUnitSyntax root,
        MemberDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        if (declaration is not TypeDeclarationSyntax typeDeclaration
            || !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return [declaration];
        }

        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        ISymbol? symbol = semanticModel?.GetDeclaredSymbol(typeDeclaration, cancellationToken);
        if (symbol is null)
        {
            return [declaration];
        }

        List<MemberDeclarationSyntax> declarations = [];
        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == root.SyntaxTree
                && await reference.GetSyntaxAsync(cancellationToken).ConfigureAwait(false)
                    is MemberDeclarationSyntax matchingDeclaration)
            {
                declarations.Add(matchingDeclaration);
            }
        }

        declarations.Sort(static (left, right) => left.SpanStart.CompareTo(right.SpanStart));
        return declarations.Count == 0 ? [declaration] : [.. declarations];
    }

    private static CompilationUnitSyntax CreateDestinationRoot(
        CompilationUnitSyntax root,
        ImmutableArray<MemberDeclarationSyntax> declarations,
        SyntaxGenerator generator)
    {
        HashSet<MemberDeclarationSyntax> declarationsToMove = new(declarations);
        SyntaxList<MemberDeclarationSyntax> movedMembers = GetMovedMembers(
            root.Members,
            declarationsToMove,
            generator);

        List<UsingDirectiveSyntax> usings = [];
        foreach (UsingDirectiveSyntax usingDirective in root.Usings)
        {
            if (usingDirective.GlobalKeyword.IsKind(SyntaxKind.None))
            {
                usings.Add(usingDirective);
            }
        }

        CompilationUnitSyntax destinationRoot = root
            .WithAttributeLists([])
            .WithUsings(SyntaxFactory.List(usings))
            .WithMembers(movedMembers);
        return CopyFileBanner(root, destinationRoot);
    }

    private static SyntaxList<MemberDeclarationSyntax> GetMovedMembers(
        SyntaxList<MemberDeclarationSyntax> members,
        HashSet<MemberDeclarationSyntax> declarationsToMove,
        SyntaxGenerator generator)
    {
        List<MemberDeclarationSyntax> movedMembers = [];

        foreach (MemberDeclarationSyntax member in members)
        {
            if (declarationsToMove.Contains(member))
            {
                movedMembers.Add(member);
                continue;
            }

            switch (member)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    SyntaxList<MemberDeclarationSyntax> namespaceMembers = GetMovedMembers(
                        namespaceDeclaration.Members,
                        declarationsToMove,
                        generator);
                    if (namespaceMembers.Count > 0)
                    {
                        movedMembers.Add(namespaceDeclaration.WithMembers(namespaceMembers));
                    }

                    break;
                case TypeDeclarationSyntax typeDeclaration:
                    SyntaxList<MemberDeclarationSyntax> nestedMembers = GetMovedMembers(
                        typeDeclaration.Members,
                        declarationsToMove,
                        generator);
                    if (nestedMembers.Count > 0)
                    {
                        movedMembers.Add(CreatePartialShell(typeDeclaration, nestedMembers, generator));
                    }

                    break;
            }
        }

        return SyntaxFactory.List(movedMembers);
    }

    private static TypeDeclarationSyntax CreatePartialShell(
        TypeDeclarationSyntax container,
        SyntaxList<MemberDeclarationSyntax> members,
        SyntaxGenerator generator)
    {
        TypeDeclarationSyntax shell = container
            .WithAttributeLists([])
            .WithBaseList(null)
            .WithConstraintClauses([])
            .WithParameterList(null)
            .WithMembers(members);
        if (shell.TypeParameterList is { } typeParameterList)
        {
            TypeParameterListSyntax sanitizedTypeParameters = typeParameterList.ReplaceNodes(
                typeParameterList.Parameters,
                static (_, rewritten) => rewritten.WithAttributeLists([]));
            shell = shell.WithTypeParameterList(sanitizedTypeParameters);
        }

        shell = MakePartial(shell, generator);

        SyntaxTriviaList leadingTrivia = shell.GetLeadingTrivia();
        List<SyntaxTrivia> whitespace = [];
        foreach (SyntaxTrivia trivia in leadingTrivia)
        {
            if (trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                whitespace.Add(trivia);
            }
        }

        return shell.WithLeadingTrivia(whitespace);
    }

    private static CompilationUnitSyntax CreateSourceRoot(
        CompilationUnitSyntax root,
        ImmutableArray<MemberDeclarationSyntax> declarations,
        SyntaxGenerator generator)
    {
        SyntaxAnnotation declarationAnnotation = new();
        SyntaxAnnotation prunableContainerAnnotation = new();
        CompilationUnitSyntax annotatedRoot = root.ReplaceNodes(
            declarations,
            (_, rewritten) => rewritten.WithAdditionalAnnotations(declarationAnnotation));
        List<TypeDeclarationSyntax> containers = [];
        HashSet<TypeDeclarationSyntax> prunableContainers = [];

        foreach (SyntaxNode annotatedDeclaration in annotatedRoot.GetAnnotatedNodes(declarationAnnotation))
        {
            foreach (SyntaxNode ancestor in annotatedDeclaration.Ancestors())
            {
                if (ancestor is TypeDeclarationSyntax typeDeclaration && !containers.Contains(typeDeclaration))
                {
                    containers.Add(typeDeclaration);
                    if (IsContributionFreePartialContainer(typeDeclaration))
                    {
                        prunableContainers.Add(typeDeclaration);
                    }
                }
            }
        }

        SyntaxNode updatedRoot = annotatedRoot.ReplaceNodes(
            containers,
            (original, rewritten) =>
            {
                TypeDeclarationSyntax updatedContainer = MakePartial(
                    (TypeDeclarationSyntax)rewritten,
                    generator);
                if (!((TypeDeclarationSyntax)original).Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    updatedContainer = updatedContainer.WithAdditionalAnnotations(s_madePartialByMove);
                }

                return prunableContainers.Contains((TypeDeclarationSyntax)original)
                    ? updatedContainer.WithAdditionalAnnotations(prunableContainerAnnotation)
                    : updatedContainer;
            });
        List<SyntaxNode> declarationsToRemove = [.. updatedRoot.GetAnnotatedNodes(declarationAnnotation)];
        updatedRoot = updatedRoot.RemoveNodes(declarationsToRemove, SyntaxRemoveOptions.KeepNoTrivia)!;

        while (true)
        {
            TypeDeclarationSyntax? emptyShell = null;
            foreach (SyntaxNode node in updatedRoot.GetAnnotatedNodes(prunableContainerAnnotation))
            {
                if (node is TypeDeclarationSyntax typeDeclaration && IsContributionFreeEmptyShell(typeDeclaration))
                {
                    emptyShell = typeDeclaration;
                    break;
                }
            }

            if (emptyShell is null)
            {
                return (CompilationUnitSyntax)updatedRoot;
            }

            updatedRoot = updatedRoot.RemoveNode(emptyShell, SyntaxRemoveOptions.KeepExteriorTrivia)!;
        }
    }

    private static bool IsContributionFreePartialContainer(TypeDeclarationSyntax declaration)
    {
        if (declaration.AttributeLists.Count > 0
            || declaration.BaseList is not null
            || declaration.ConstraintClauses.Count > 0
            || declaration.ParameterList is not null
            || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || declaration.HasAnnotation(s_madePartialByMove)
            || HasDocumentationComment(declaration.GetLeadingTrivia()))
        {
            return false;
        }

        if (declaration.TypeParameterList is not { } typeParameterList)
        {
            return true;
        }

        foreach (TypeParameterSyntax parameter in typeParameterList.Parameters)
        {
            if (parameter.AttributeLists.Count > 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDocumentationComment(SyntaxTriviaList triviaList)
    {
        foreach (SyntaxTrivia trivia in triviaList)
        {
            if (trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsContributionFreeEmptyShell(TypeDeclarationSyntax declaration)
    {
        return declaration.Members.Count == 0 && IsContributionFreePartialContainer(declaration);
    }

    private static TypeDeclarationSyntax MakePartial(TypeDeclarationSyntax declaration, SyntaxGenerator generator)
    {
        DeclarationModifiers modifiers = generator.GetModifiers(declaration);
        return modifiers.IsPartial
            ? declaration
            : (TypeDeclarationSyntax)generator.WithModifiers(declaration, modifiers.WithPartial(true));
    }

    private static MemberDeclarationSyntax? GetAnnotatedDeclaration(SyntaxNode root, SyntaxAnnotation annotation)
    {
        foreach (SyntaxNode node in root.GetAnnotatedNodes(annotation))
        {
            if (node is MemberDeclarationSyntax declaration)
            {
                return declaration;
            }
        }

        return null;
    }

    private static CompilationUnitSyntax CopyFileBanner(
        CompilationUnitSyntax sourceRoot,
        CompilationUnitSyntax destinationRoot)
    {
        SyntaxTriviaList banner = GetFileBanner(sourceRoot);
        if (banner.Count == 0
            || destinationRoot.GetLeadingTrivia().ToFullString().StartsWith(
                banner.ToFullString(),
                StringComparison.Ordinal))
        {
            return destinationRoot;
        }

        return destinationRoot.WithLeadingTrivia(banner.AddRange(destinationRoot.GetLeadingTrivia()));
    }

    private static SyntaxTriviaList GetFileBanner(CompilationUnitSyntax root)
    {
        SyntaxTriviaList leadingTrivia = root.GetLeadingTrivia();
        List<SyntaxTrivia> banner = [];
        bool sawComment = false;
        int consecutiveLineBreaks = 0;

        foreach (SyntaxTrivia trivia in leadingTrivia)
        {
            banner.Add(trivia);

            if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                sawComment = true;
                consecutiveLineBreaks = 0;
                continue;
            }

            if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
            {
                consecutiveLineBreaks++;
                if (sawComment && consecutiveLineBreaks >= 2)
                {
                    return SyntaxFactory.TriviaList(banner);
                }

                continue;
            }

            if (!trivia.IsKind(SyntaxKind.WhitespaceTrivia))
            {
                return [];
            }
        }

        return [];
    }

    private static SyntaxToken GetIdentifier(MemberDeclarationSyntax declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax typeDeclaration => typeDeclaration.Identifier,
        DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Identifier,
        _ => default
    };

}