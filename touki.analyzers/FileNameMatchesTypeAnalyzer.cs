// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
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
///   declares no types is not reported, and neither is a tree with no path.
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
        messageFormat: "File name '{0}' does not match type '{1}'",
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

        // The rule is about the file as a whole. Only member lists are walked, which keeps the walk
        // proportional to the number of declarations rather than to the size of the file.
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        // An in-memory tree has no path to match against.
        if (string.IsNullOrEmpty(context.Tree.FilePath)
            || context.Tree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax compilationUnit)
        {
            return;
        }

        List<string> candidates = [];
        SyntaxToken firstIdentifier = default;
        CollectNames(compilationUnit.Members, prefix: null, candidates, ref firstIdentifier);

        if (candidates.Count == 0)
        {
            // Global usings, assembly attributes, and the like name nothing.
            return;
        }

        string fileName = Path.GetFileNameWithoutExtension(context.Tree.FilePath);
        string separators = GetDetailSeparators(context);

        foreach (string candidate in candidates)
        {
            if (Matches(fileName, candidate, separators))
            {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(s_rule, firstIdentifier.GetLocation(), fileName, firstIdentifier.ValueText));
    }

    /// <summary>
    ///  Adds every name a file declaring <paramref name="members"/> may be called to
    ///  <paramref name="candidates"/>, and records the first type's identifier in
    ///  <paramref name="firstIdentifier"/>. A nested type contributes both its own name and its dotted path.
    /// </summary>
    private static void CollectNames(
        SyntaxList<MemberDeclarationSyntax> members,
        string? prefix,
        List<string> candidates,
        ref SyntaxToken firstIdentifier)
    {
        foreach (MemberDeclarationSyntax member in members)
        {
            if (member is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                CollectNames(namespaceDeclaration.Members, prefix, candidates, ref firstIdentifier);
                continue;
            }

            SyntaxToken identifier = GetIdentifier(member);

            // Anything that is not a named type declaration names nothing. An extension block is a type
            // declaration with no identifier, so this excludes it as well.
            if (identifier.IsKind(SyntaxKind.None))
            {
                continue;
            }

            if (firstIdentifier.IsKind(SyntaxKind.None))
            {
                firstIdentifier = identifier;
            }

            string name = identifier.ValueText;
            string qualified = prefix is null ? name : $"{prefix}.{name}";

            candidates.Add(name);

            if (prefix is not null)
            {
                candidates.Add(qualified);
            }

            // Only class, struct, interface, and record bodies can hold a nested type.
            if (member is TypeDeclarationSyntax typeDeclaration)
            {
                CollectNames(typeDeclaration.Members, qualified, candidates, ref firstIdentifier);
            }
        }
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

    private static string GetDetailSeparators(SyntaxTreeAnalysisContext context)
    {
        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);

        if (options.TryGetValue(DetailSeparatorsOption, out string? value))
        {
            string separators = value.Trim();

            // An empty value is far more likely to be a mistake than a deliberate "allow no detail".
            if (separators.Length > 0)
            {
                return separators;
            }
        }

        return DefaultDetailSeparators;
    }
}
