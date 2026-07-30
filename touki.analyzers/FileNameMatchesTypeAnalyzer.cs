// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports files whose name does not match a type declared in them, so that a type can be found from its
///  file name alone.
/// </summary>
/// <remarks>
///  <para>
///   A nested type may be named either way: <c>Bar</c> nested in <c>Foo</c> is matched by both
///   <c>Foo.Bar.cs</c> and <c>Bar.cs</c>.
///  </para>
///  <para>
///   Detail may follow the type name when introduced by an approved separator, so <c>Foo.Windows.cs</c>,
///   <c>Foo-Windows.cs</c>, and <c>Foo_Windows.cs</c> all match <c>Foo</c>. The approved separators default
///   to <see cref="DefaultDetailSeparators"/> and are configured per path in <c>.editorconfig</c> as the set
///   of characters to allow:
///   <code>dotnet_code_quality.TOUKI0021.file_name_detail_separators = .-</code>
///  </para>
///  <para>
///   Comparison is ordinal, so casing must match even on a case-insensitive file system. A file that
///   declares no types is not reported, and neither is a tree with no path. An empty partial declaration whose
///   body contains conditional directives is also omitted: after preprocessing it is only a shell for a type
///   declared in another build configuration.
///  </para>
///  <para>
///   Diagnostics include a collision-aware suggested file name. A nested declaration prefers its containing
///   type path, and one part of a type split across files keeps the current stem as detail.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FileNameMatchesTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0021";

    /// <summary>
    ///  The diagnostic property containing the suggested destination file name.
    /// </summary>
    public const string SuggestedFileNameProperty = "SuggestedFileName";

    /// <summary>
    ///  The diagnostic property containing the separator used to add distinguishing detail.
    /// </summary>
    public const string SuggestedDetailSeparatorProperty = "SuggestedDetailSeparator";

    /// <summary>
    ///  The <c>.editorconfig</c> key that overrides the characters allowed to introduce trailing detail in a
    ///  file name. The value is the set of characters, for example <c>.-</c>.
    /// </summary>
    public const string DetailSeparatorsOption = "dotnet_code_quality.TOUKI0021.file_name_detail_separators";

    /// <summary>
    ///  The characters allowed to introduce trailing detail when <see cref="DetailSeparatorsOption"/> is not
    ///  configured.
    /// </summary>
    public const string DefaultDetailSeparators = ".-_";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "File name should match the type it declares",
        messageFormat: "Rename file '{0}' to '{2}' to match type '{1}'",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A file should be named for the type it declares so the type can be found from the file name. A nested type may use either its own name or its containing type's dotted path, and detail may follow the name after an approved separator.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    // Cache the supported-diagnostics array so the property does not allocate a new array on every access.
    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // The rule is about the file as a whole. Only member lists are walked. A symbol is bound only after a
        // mismatch, when deciding whether a partial type needs the current file stem as distinguishing detail.
        context.RegisterSemanticModelAction(AnalyzeSemanticModel);
    }

    private static void AnalyzeSemanticModel(SemanticModelAnalysisContext context)
    {
        SyntaxTree tree = context.SemanticModel.SyntaxTree;

        // An in-memory tree has no path to match against.
        if (string.IsNullOrEmpty(tree.FilePath)
            || tree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax compilationUnit)
        {
            return;
        }

        List<string> candidates = [];
        MemberDeclarationSyntax? preferredDeclaration = null;
        string? preferredStem = null;
        CollectNames(
            compilationUnit.Members,
            prefix: null,
            candidates,
            ref preferredDeclaration,
            ref preferredStem);

        if (candidates.Count == 0)
        {
            // Global usings, assembly attributes, and the like name nothing.
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(tree.FilePath);
        string separators = GetDetailSeparators(context);

        foreach (string candidate in candidates)
        {
            if (Matches(fileName, candidate, separators))
            {
                return;
            }
        }

        if (preferredDeclaration is null || preferredStem is null)
        {
            return;
        }

        SyntaxToken preferredIdentifier = GetIdentifier(preferredDeclaration);
        char detailSeparator = GetPreferredDetailSeparator(separators);
        string suggestedFileName = GetSuggestedFileName(
            context,
            preferredDeclaration,
            preferredStem,
            detailSeparator);
        ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
            .Add(SuggestedFileNameProperty, suggestedFileName)
            .Add(SuggestedDetailSeparatorProperty, detailSeparator.ToString());

        context.ReportDiagnostic(
            Diagnostic.Create(
                s_rule,
                preferredIdentifier.GetLocation(),
                properties,
                fileName,
                preferredIdentifier.ValueText,
                suggestedFileName));
    }

    /// <summary>
    ///  Adds every name a file declaring <paramref name="members"/> may be called to
    ///  <paramref name="candidates"/>, and records the first non-hosting declaration and qualified stem in
    ///  <paramref name="preferredDeclaration"/> and <paramref name="preferredStem"/>. A nested type contributes
    ///  both its own name and its dotted path.
    /// </summary>
    private static void CollectNames(
        SyntaxList<MemberDeclarationSyntax> members,
        string? prefix,
        List<string> candidates,
        ref MemberDeclarationSyntax? preferredDeclaration,
        ref string? preferredStem)
    {
        foreach (MemberDeclarationSyntax member in members)
        {
            if (member is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                CollectNames(
                    namespaceDeclaration.Members,
                    prefix,
                    candidates,
                    ref preferredDeclaration,
                    ref preferredStem);
                continue;
            }

            SyntaxToken identifier = GetIdentifier(member);

            // Anything that is not a named type declaration names nothing. An extension block is a type
            // declaration with no identifier, so this excludes it as well.
            if (identifier.IsKind(SyntaxKind.None))
            {
                continue;
            }

            if (member is TypeDeclarationSyntax conditionalShell
                && IsConditionallyEmptyPartial(conditionalShell))
            {
                continue;
            }

            string name = identifier.ValueText;
            string qualified = prefix is null ? name : $"{prefix}.{name}";

            if (preferredDeclaration is null
                && (member is not TypeDeclarationSyntax candidate || !IsHostingShell(candidate)))
            {
                preferredDeclaration = member;
                preferredStem = qualified;
            }

            candidates.Add(name);

            if (prefix is not null)
            {
                candidates.Add(qualified);
            }

            string? bracedGenericSuffix = GetBracedGenericSuffix(member);

            if (bracedGenericSuffix is not null)
            {
                candidates.Add(name + bracedGenericSuffix);

                if (prefix is not null)
                {
                    candidates.Add(qualified + bracedGenericSuffix);
                }
            }

            // Only class, struct, interface, and record bodies can hold a nested type.
            if (member is TypeDeclarationSyntax typeDeclaration)
            {
                CollectNames(
                    typeDeclaration.Members,
                    qualified,
                    candidates,
                    ref preferredDeclaration,
                    ref preferredStem);
            }
        }
    }

    private static bool IsHostingShell(TypeDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration.Members.Count == 0
            || !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return false;
        }

        foreach (MemberDeclarationSyntax member in typeDeclaration.Members)
        {
            if (GetIdentifier(member).IsKind(SyntaxKind.None))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsConditionallyEmptyPartial(TypeDeclarationSyntax typeDeclaration)
    {
        if (typeDeclaration.Members.Count != 0
            || !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || typeDeclaration.OpenBraceToken.IsKind(SyntaxKind.None)
            || typeDeclaration.CloseBraceToken.IsKind(SyntaxKind.None))
        {
            return false;
        }

        int bodyStart = typeDeclaration.OpenBraceToken.Span.End;
        int bodyEnd = typeDeclaration.CloseBraceToken.SpanStart;

        foreach (SyntaxTrivia trivia in typeDeclaration.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.SpanStart < bodyStart || trivia.Span.End > bodyEnd)
            {
                continue;
            }

            if (trivia.IsKind(SyntaxKind.IfDirectiveTrivia)
                || trivia.IsKind(SyntaxKind.ElifDirectiveTrivia)
                || trivia.IsKind(SyntaxKind.ElseDirectiveTrivia)
                || trivia.IsKind(SyntaxKind.EndIfDirectiveTrivia))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetSuggestedFileName(
        SemanticModelAnalysisContext context,
        MemberDeclarationSyntax preferredDeclaration,
        string preferredStem,
        char detailSeparator)
    {
        SyntaxTree tree = context.SemanticModel.SyntaxTree;
        Compilation compilation = context.SemanticModel.Compilation;
        string extension = Path.GetExtension(tree.FilePath);
        string currentStem = Path.GetFileNameWithoutExtension(tree.FilePath);
        bool isSplitPartial = preferredDeclaration is TypeDeclarationSyntax typeDeclaration
            && typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            && IsDeclaredInAnotherTree(context.SemanticModel, tree, typeDeclaration, context.CancellationToken);

        string firstStem = isSplitPartial ? $"{preferredStem}{detailSeparator}{currentStem}" : preferredStem;
        string firstCandidate = firstStem + extension;
        if (IsAvailableFileName(compilation, tree, firstCandidate))
        {
            return firstCandidate;
        }

        string detailStem = $"{preferredStem}{detailSeparator}{currentStem}";
        string detailCandidate = detailStem + extension;
        if (!string.Equals(firstCandidate, detailCandidate, StringComparison.OrdinalIgnoreCase)
            && IsAvailableFileName(compilation, tree, detailCandidate))
        {
            return detailCandidate;
        }

        for (int suffix = 2; ; suffix++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            string candidate = $"{detailStem}{detailSeparator}{suffix}{extension}";
            if (IsAvailableFileName(compilation, tree, candidate))
            {
                return candidate;
            }
        }
    }

    private static char GetPreferredDetailSeparator(string separators)
        => separators[0];

    private static bool IsDeclaredInAnotherTree(
        SemanticModel semanticModel,
        SyntaxTree currentTree,
        TypeDeclarationSyntax declaration,
        CancellationToken cancellationToken)
    {
        ISymbol? symbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);

        if (symbol is null)
        {
            return false;
        }

        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree != currentTree)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAvailableFileName(Compilation compilation, SyntaxTree currentTree, string candidate)
    {
        string currentDirectory = Path.GetDirectoryName(currentTree.FilePath) ?? string.Empty;

        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            if (tree == currentTree || string.IsNullOrEmpty(tree.FilePath))
            {
                continue;
            }

            string directory = Path.GetDirectoryName(tree.FilePath) ?? string.Empty;
            if (string.Equals(directory, currentDirectory, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFileName(tree.FilePath), candidate, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Returns the identifier of <paramref name="member"/>, or a <see cref="SyntaxKind.None"/> token when it
    ///  does not declare a named type. The test is on the identifier rather than on the node type because the
    ///  analyzer is built against a Roslyn version that predates the extension-block syntax.
    /// </summary>
    private static SyntaxToken GetIdentifier(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier,
        DelegateDeclarationSyntax declaration => declaration.Identifier,
        _ => default
    };

    private static string? GetBracedGenericSuffix(MemberDeclarationSyntax member)
    {
        TypeParameterListSyntax? typeParameterList = member switch
        {
            TypeDeclarationSyntax typeDeclaration => typeDeclaration.TypeParameterList,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.TypeParameterList,
            _ => null
        };

        if (typeParameterList is null || typeParameterList.Parameters.Count == 0)
        {
            return null;
        }

        string[] typeParameterNames = new string[typeParameterList.Parameters.Count];

        for (int i = 0; i < typeParameterNames.Length; i++)
        {
            typeParameterNames[i] = typeParameterList.Parameters[i].Identifier.ValueText;
        }

        return "{" + string.Join(",", typeParameterNames) + "}";
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="fileName"/> is <paramref name="candidate"/>, either
    ///  exactly or followed by detail introduced by one of <paramref name="separators"/>.
    /// </summary>
    private static bool Matches(string fileName, string candidate, string separators)
    {
        if (!fileName.StartsWith(candidate, StringComparison.Ordinal))
        {
            return false;
        }

        if (fileName.Length == candidate.Length)
        {
            return true;
        }

        return separators.IndexOf(fileName[candidate.Length]) >= 0;
    }

    private static string GetDetailSeparators(SemanticModelAnalysisContext context)
    {
        AnalyzerConfigOptions options =
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.SemanticModel.SyntaxTree);

        if (options.TryGetValue(DetailSeparatorsOption, out string? value))
        {
            string separators = GetValidDetailSeparators(value.Trim());

            // An empty value is far more likely to be a mistake than a deliberate "allow no detail".
            if (separators.Length > 0)
            {
                return separators;
            }
        }

        return DefaultDetailSeparators;
    }

    private static string GetValidDetailSeparators(string separators)
    {
        char[] validSeparators = new char[separators.Length];
        int count = 0;

        foreach (char separator in separators)
        {
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), separator) < 0)
            {
                validSeparators[count++] = separator;
            }
        }

        return count == 0 ? string.Empty : new string(validSeparators, 0, count);
    }
}
