// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports every type declared in a file after the first, so that each file declares a single type.
/// </summary>
/// <remarks>
///  <para>
///   Nested types count: a nested type must live in its own file too, which does not stop it from being
///   nested. The established shape is a file per type where the containing types are re-declared as
///   <see langword="partial"/> shells, for example <c>Outer.Nested.cs</c> holding
///   <c>partial class Outer { struct Nested { } }</c>. Such a shell only hosts the type the file declares,
///   so it is not counted.
///  </para>
///  <para>
///   The diagnostic is reported on the identifier of the extra type. The companion code fix moves the
///   declaration to a new file and supports solution-wide Fix All, including delegates and nested types.
///  </para>
///  <para>
///   Set <c>dotnet_code_quality.TOUKI0020.exclude_nested_types = true</c> to enforce top-level types while
///   deferring nested-type adoption.
///  </para>
///  <para>
///   File-local types and the types nested within them are excluded. Their <see langword="file"/> modifier
///   intentionally ties them to the source file that contains them.
///  </para>
///  <para>
///   <b>Constraints and limitations.</b> The rule is purely syntactic; it never binds a symbol.
///   <list type="bullet">
///    <item>
///     <description>
///      Classes, structs, interfaces, records, enums, and delegates all count as types, whether they are
///      declared at the top level or nested inside another type.
///     </description>
///    </item>
///    <item>
///     <description>
///      A <see langword="partial"/> declaration whose members are all types is a hosting shell for the types
///      nested in it and is not itself counted. A <see langword="partial"/> declaration that adds any other
///      member - a field, a method, a property - is contributing to its own type and does count, as does an
///      empty one, which hosts nothing.
///     </description>
///    </item>
///    <item>
///     <description>
///      Repeated <see langword="partial"/> declarations of the same type in one file are one type, matched by
///      declaration kind, name, and arity. Because the match is syntactic it ignores the containing scope, so
///      two same-named partial types declared in <em>different</em> namespaces in one file are treated as one
///      type and go unreported - a deliberate false negative in a pathological shape.
///     </description>
///    </item>
///   </list>
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class OneTypePerFileAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0020";

    /// <summary>
    ///  The <c>.editorconfig</c> key that excludes nested types while retaining top-level type enforcement.
    /// </summary>
    public const string ExcludeNestedTypesOption = "dotnet_code_quality.TOUKI0020.exclude_nested_types";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Declare one type per file",
        messageFormat: "Move '{0}' to its own file, '{1}' is already declared in this file",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A file should declare a single type, nested types included, so that types are easy to find by file name. A 'partial' declaration that only hosts nested types and repeated 'partial' declarations of the same type do not count as additional types.",
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

        // The rule is about the file as a whole, so it needs the whole file. Only member lists are walked, which
        // keeps the walk proportional to the number of declarations rather than to the size of the file.
        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        if (context.Tree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax compilationUnit)
        {
            return;
        }

        AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree);
        bool excludeNestedTypes = options.TryGetValue(ExcludeNestedTypesOption, out string? configuredValue)
            && bool.TryParse(configuredValue.Trim(), out bool configuredExcludeNestedTypes)
            && configuredExcludeNestedTypes;

        List<MemberDeclarationSyntax> types = [];
        CollectTypes(compilationUnit.Members, types, includeNestedTypes: !excludeNestedTypes);

        if (types.Count < 2)
        {
            return;
        }

        string firstName = GetDisplayName(types[0]);

        for (int i = 1; i < types.Count; i++)
        {
            SyntaxToken identifier = GetIdentifier(types[i]);

            context.ReportDiagnostic(
                Diagnostic.Create(s_rule, identifier.GetLocation(), GetDisplayName(types[i]), firstName));
        }
    }

    /// <summary>
    ///  Adds every type declared in <paramref name="members"/> to <paramref name="types"/> in source order,
    ///  descending through namespace declarations and type bodies alike.
    /// </summary>
    private static void CollectTypes(
        SyntaxList<MemberDeclarationSyntax> members,
        List<MemberDeclarationSyntax> types,
        bool includeNestedTypes)
    {
        foreach (MemberDeclarationSyntax member in members)
        {
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    CollectTypes(namespaceDeclaration.Members, types, includeNestedTypes);
                    break;
                case TypeDeclarationSyntax typeDeclaration:
                    if (HasFileModifier(typeDeclaration))
                    {
                        break;
                    }

                    if (IsNamedType(typeDeclaration)
                        && !IsHostingShell(typeDeclaration)
                        && !IsRepeatedPartialDeclaration(typeDeclaration, types))
                    {
                        types.Add(typeDeclaration);
                    }

                    if (includeNestedTypes)
                    {
                        CollectTypes(typeDeclaration.Members, types, includeNestedTypes);
                    }

                    break;
                case EnumDeclarationSyntax or DelegateDeclarationSyntax:
                    // Neither can be partial or contain a nested type, so they are always a plain leaf.
                    if (!HasFileModifier(member))
                    {
                        types.Add(member);
                    }

                    break;
            }
        }
    }

    private static bool HasFileModifier(MemberDeclarationSyntax declaration) => declaration switch
    {
        BaseTypeDeclarationSyntax type => type.Modifiers.Any(SyntaxKind.FileKeyword),
        DelegateDeclarationSyntax @delegate => @delegate.Modifiers.Any(SyntaxKind.FileKeyword),
        _ => false
    };

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="member"/> declares a named type. An extension block
    ///  is a <see cref="TypeDeclarationSyntax"/> with no identifier - a container for extension members inside
    ///  a static class rather than a type of its own - so it is excluded. The test is on the identifier rather
    ///  than on the node type because the analyzer is built against a Roslyn version that predates the syntax.
    /// </summary>
    private static bool IsNamedType(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax type => !type.Identifier.IsKind(SyntaxKind.None),
        DelegateDeclarationSyntax => true,
        _ => false
    };

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="type"/> is a <see langword="partial"/> declaration
    ///  that declares one or more types and nothing else, making it a host for the types nested in it rather
    ///  than one of the file's own types.
    /// </summary>
    private static bool IsHostingShell(TypeDeclarationSyntax type)
    {
        // An empty partial declaration hosts nothing, so it is this file's declaration of that type rather
        // than a container for another one.
        if (type.Members.Count == 0 || !type.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return false;
        }

        foreach (MemberDeclarationSyntax member in type.Members)
        {
            if (!IsNamedType(member))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="candidate"/> is another <see langword="partial"/>
    ///  declaration of a type already collected in this file.
    /// </summary>
    private static bool IsRepeatedPartialDeclaration(TypeDeclarationSyntax candidate, List<MemberDeclarationSyntax> types)
    {
        // Only classes, structs, interfaces, and records can be partial.
        if (!candidate.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            return false;
        }

        for (int i = 0; i < types.Count; i++)
        {
            if (types[i] is TypeDeclarationSyntax collected
                && collected.RawKind == candidate.RawKind
                && collected.Identifier.ValueText == candidate.Identifier.ValueText
                && GetArity(collected) == GetArity(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static int GetArity(TypeDeclarationSyntax type) => type.TypeParameterList?.Parameters.Count ?? 0;

    private static SyntaxToken GetIdentifier(MemberDeclarationSyntax member) => member switch
    {
        BaseTypeDeclarationSyntax type => type.Identifier,
        DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Identifier,
        _ => default
    };

    /// <summary>
    ///  Gets the name to show in the message, with type parameters, so that two types that differ only in
    ///  arity are told apart.
    /// </summary>
    private static string GetDisplayName(MemberDeclarationSyntax member)
    {
        TypeParameterListSyntax? typeParameters = member switch
        {
            TypeDeclarationSyntax type => type.TypeParameterList,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.TypeParameterList,
            _ => null
        };

        string name = GetIdentifier(member).ValueText;
        return typeParameters is null ? name : name + typeParameters.ToString();
    }
}
