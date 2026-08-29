// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Reports source types that do not declare one XML <c>&lt;summary&gt;</c> element or inherit one from documented code.
/// </summary>
/// <remarks>
///  <para>
///   Classes, structs, interfaces, records, enums, and delegates are analyzed, including nested types. For a
///   partial type, exactly one declaration may contain a top-level <c>&lt;summary&gt;</c> element. A top-level
///   <c>&lt;inheritdoc&gt;</c> element satisfies a type with no local summary when its target has documentation.
///   Inheritdoc elements with a <c>path</c> filter do not satisfy the rule. Documentation on generated partial
///   declarations participates in the count, but diagnostics are reported only in user-authored code.
///  </para>
///  <para>
///   Configure the analyzed visibility with
///   <c>dotnet_code_quality.TOUKI0025.api_surface</c>. The accepted comma-separated values are <c>public</c>,
///   <c>internal</c>, <c>private</c>, and <c>file</c>; <c>all</c> is the default. A partial type is analyzed when
///   any declaring file includes its declared visibility. For nested types,
///   <c>dotnet_code_quality.TOUKI0025.effective_api_surface</c> can specify a different set based on visibility
///   through the containing-type hierarchy.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeXmlSummaryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier reported by this analyzer.
    /// </summary>
    public const string DiagnosticId = "TOUKI0025";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls which type visibilities are analyzed.
    /// </summary>
    public const string ApiSurfaceOption = "dotnet_code_quality.TOUKI0025.api_surface";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls effective visibilities for nested types.
    /// </summary>
    public const string EffectiveApiSurfaceOption =
        "dotnet_code_quality.TOUKI0025.effective_api_surface";

    private const string GeneratedCodeOption = "generated_code";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Document types",
        messageFormat: "Type '{0}' must declare one XML <summary> element or a valid <inheritdoc> element; found {1} summaries",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each type should have one XML summary or inherit documentation across its declarations.",
        helpLinkUri: HelpLinks.ForRule(DiagnosticId));

    private static readonly ImmutableArray<DiagnosticDescriptor> s_supportedDiagnostics = [s_rule];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => s_supportedDiagnostics;

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static compilationContext =>
        {
            INamedTypeSymbol? generatedCodeAttribute = compilationContext.Compilation.GetTypeByMetadataName(
                "System.CodeDom.Compiler.GeneratedCodeAttribute");
            INamedTypeSymbol? compilerGeneratedAttribute = compilationContext.Compilation.GetTypeByMetadataName(
                "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(
                    symbolContext,
                    compilationContext.Compilation,
                    generatedCodeAttribute,
                    compilerGeneratedAttribute),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        Compilation compilation,
        INamedTypeSymbol? generatedCodeAttribute,
        INamedTypeSymbol? compilerGeneratedAttribute)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
        if (type.IsImplicitlyDeclared
            || !IsSupportedTypeKind(type.TypeKind)
            || type.DeclaringSyntaxReferences.IsDefaultOrEmpty)
        {
            return;
        }

        ImmutableArray<SyntaxReference> declarations = type.DeclaringSyntaxReferences;
        MemberDeclarationSyntax? reportDeclaration = GetReportDeclaration(
            type,
            declarations,
            compilation,
            context.Options.AnalyzerConfigOptionsProvider,
            generatedCodeAttribute,
            compilerGeneratedAttribute,
            context.CancellationToken);
        if (reportDeclaration is null
            || !IsIncluded(type, declarations, context.Options.AnalyzerConfigOptionsProvider))
        {
            return;
        }

        XmlDocumentationInfo documentation = GetDocumentation(declarations, context.CancellationToken);
        if (documentation.SummaryCount == 1)
        {
            return;
        }

        if (documentation.SummaryCount == 0
            && documentation.HasInheritdoc
            && DocumentationInheritanceResolver.GetInheritdocDocumentation(
                type,
                documentation,
                compilation,
                includeSourceDeclaration: null,
                context.CancellationToken) is DocumentationAvailability.Documented or DocumentationAvailability.Unknown)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                s_rule,
                GetIdentifierLocation(reportDeclaration),
                type.Name,
                documentation.SummaryCount));
    }

    private static bool IsSupportedTypeKind(TypeKind typeKind) => typeKind is
        TypeKind.Class or TypeKind.Struct or TypeKind.Interface or TypeKind.Enum or TypeKind.Delegate;

    private static MemberDeclarationSyntax? GetReportDeclaration(
        INamedTypeSymbol type,
        ImmutableArray<SyntaxReference> declarations,
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        INamedTypeSymbol? generatedCodeAttribute,
        INamedTypeSymbol? compilerGeneratedAttribute,
        CancellationToken cancellationToken)
    {
        MemberDeclarationSyntax? result = null;

        foreach (SyntaxReference reference in declarations)
        {
            if (reference.GetSyntax(cancellationToken) is not MemberDeclarationSyntax declaration
                || IsGeneratedDeclaration(
                    type,
                    declaration,
                    compilation,
                    optionsProvider,
                    generatedCodeAttribute,
                    compilerGeneratedAttribute,
                    cancellationToken))
            {
                continue;
            }

            if (result is null || IsEarlier(declaration, result))
            {
                result = declaration;
            }
        }

        return result;
    }

    private static bool IsGeneratedDeclaration(
        INamedTypeSymbol type,
        MemberDeclarationSyntax declaration,
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        INamedTypeSymbol? generatedCodeAttribute,
        INamedTypeSymbol? compilerGeneratedAttribute,
        CancellationToken cancellationToken)
    {
        if (HasGeneratedAttribute(
            type,
            declaration,
            generatedCodeAttribute,
            compilerGeneratedAttribute,
            cancellationToken))
        {
            return true;
        }

        SyntaxTree tree = declaration.SyntaxTree;
        if (tree is CSharpSyntaxTree csharpTree
            && csharpTree.GetLineVisibility(declaration.SpanStart, cancellationToken) == LineVisibility.Hidden)
        {
            return true;
        }

        if (type.ContainingType is INamedTypeSymbol containingType
            && GetContainingTypeDeclaration(declaration) is MemberDeclarationSyntax containingDeclaration
            && IsGeneratedDeclaration(
                containingType,
                containingDeclaration,
                compilation,
                optionsProvider,
                generatedCodeAttribute,
                compilerGeneratedAttribute,
                cancellationToken))
        {
            return true;
        }

        AnalyzerConfigOptions options = optionsProvider.GetOptions(tree);
        if (options.TryGetValue(GeneratedCodeOption, out string? configured)
            && bool.TryParse(configured.Trim(), out bool configuredGenerated))
        {
            return configuredGenerated;
        }

        SyntaxTreeOptionsProvider? syntaxTreeOptions = compilation.Options.SyntaxTreeOptionsProvider;
        if (syntaxTreeOptions is not null)
        {
            GeneratedKind generatedKind = syntaxTreeOptions.IsGenerated(tree, cancellationToken);
            if (generatedKind == GeneratedKind.MarkedGenerated)
            {
                return true;
            }

            if (generatedKind == GeneratedKind.NotGenerated)
            {
                return false;
            }
        }

        return HasGeneratedFileName(tree.FilePath) || HasGeneratedHeader(tree, cancellationToken);
    }

    private static MemberDeclarationSyntax? GetContainingTypeDeclaration(MemberDeclarationSyntax declaration)
    {
        for (SyntaxNode? current = declaration.Parent; current is not null; current = current.Parent)
        {
            if (current is TypeDeclarationSyntax containingType)
            {
                return containingType;
            }
        }

        return null;
    }

    private static bool HasGeneratedAttribute(
        INamedTypeSymbol type,
        MemberDeclarationSyntax declaration,
        INamedTypeSymbol? generatedCodeAttribute,
        INamedTypeSymbol? compilerGeneratedAttribute,
        CancellationToken cancellationToken)
    {
        bool hasAttributeLists = declaration switch
        {
            BaseTypeDeclarationSyntax declaredType => declaredType.AttributeLists.Count > 0,
            DelegateDeclarationSyntax @delegate => @delegate.AttributeLists.Count > 0,
            _ => false
        };

        if (!hasAttributeLists || (generatedCodeAttribute is null && compilerGeneratedAttribute is null))
        {
            return false;
        }

        foreach (AttributeData attribute in type.GetAttributes())
        {
            if ((!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, generatedCodeAttribute)
                    && !SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, compilerGeneratedAttribute))
                || attribute.ApplicationSyntaxReference is not SyntaxReference application
                || application.SyntaxTree != declaration.SyntaxTree
                || !declaration.Span.Contains(application.Span))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return true;
        }

        return false;
    }

    private static bool HasGeneratedFileName(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        return fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasGeneratedHeader(SyntaxTree tree, CancellationToken cancellationToken)
    {
        SyntaxNode root = tree.GetRoot(cancellationToken);
        foreach (SyntaxTrivia trivia in root.GetLeadingTrivia())
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                continue;
            }

            string text = trivia.ToString();
            if (text.IndexOf("<auto-generated", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("<autogenerated", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIncluded(
        INamedTypeSymbol type,
        ImmutableArray<SyntaxReference> declarations,
        AnalyzerConfigOptionsProvider optionsProvider)
    {
        foreach (SyntaxReference declaration in declarations)
        {
            AnalyzerConfigOptions options = optionsProvider.GetOptions(declaration.SyntaxTree);
            bool useEffectiveSurface = type.ContainingType is not null
                && options.TryGetValue(EffectiveApiSurfaceOption, out _);
            ApiSurface configuredSurface = GetConfiguredApiSurface(
                options,
                useEffectiveSurface ? EffectiveApiSurfaceOption : ApiSurfaceOption);
            ApiSurface typeSurface = useEffectiveSurface
                ? GetEffectiveVisibility(type)
                : GetDeclaredVisibility(type);
            if ((configuredSurface & typeSurface) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private static ApiSurface GetDeclaredVisibility(INamedTypeSymbol type)
    {
        if (type.IsFileLocal)
        {
            return ApiSurface.File;
        }

        return type.DeclaredAccessibility switch
        {
            Accessibility.Private => ApiSurface.Private,
            Accessibility.Internal or Accessibility.ProtectedAndInternal => ApiSurface.Internal,
            _ => ApiSurface.Public
        };
    }

    private static ApiSurface GetEffectiveVisibility(INamedTypeSymbol type)
    {
        ApiSurface visibility = ApiSurface.Public;

        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Private:
                    return ApiSurface.Private;
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    visibility = ApiSurface.Internal;
                    break;
            }

            if (current.IsFileLocal)
            {
                return ApiSurface.File;
            }
        }

        return visibility;
    }

    private static ApiSurface GetConfiguredApiSurface(AnalyzerConfigOptions options, string option)
    {
        if (!options.TryGetValue(option, out string? configured)
            || string.IsNullOrWhiteSpace(configured))
        {
            return ApiSurface.All;
        }

        ApiSurface result = 0;
        int tokenStart = 0;

        while (tokenStart <= configured.Length)
        {
            int separator = configured.IndexOf(',', tokenStart);
            int tokenEnd = separator < 0 ? configured.Length : separator;

            while (tokenStart < tokenEnd && char.IsWhiteSpace(configured[tokenStart]))
            {
                tokenStart++;
            }

            while (tokenEnd > tokenStart && char.IsWhiteSpace(configured[tokenEnd - 1]))
            {
                tokenEnd--;
            }

            int tokenLength = tokenEnd - tokenStart;
            if (TokenEquals(configured, tokenStart, tokenLength, "all"))
            {
                result |= ApiSurface.All;
            }
            else if (TokenEquals(configured, tokenStart, tokenLength, "public"))
            {
                result |= ApiSurface.Public;
            }
            else if (TokenEquals(configured, tokenStart, tokenLength, "internal"))
            {
                result |= ApiSurface.Internal;
            }
            else if (TokenEquals(configured, tokenStart, tokenLength, "private"))
            {
                result |= ApiSurface.Private;
            }
            else if (TokenEquals(configured, tokenStart, tokenLength, "file"))
            {
                result |= ApiSurface.File;
            }
            else
            {
                return ApiSurface.All;
            }

            if (separator < 0)
            {
                break;
            }

            tokenStart = separator + 1;
        }

        return result == 0 ? ApiSurface.All : result;
    }

    private static bool TokenEquals(string value, int start, int length, string expected) =>
        length == expected.Length
        && string.Compare(value, start, expected, 0, length, StringComparison.OrdinalIgnoreCase) == 0;

    private static XmlDocumentationInfo GetDocumentation(
        ImmutableArray<SyntaxReference> declarations,
        CancellationToken cancellationToken)
    {
        XmlDocumentationInfo documentation = default;

        foreach (SyntaxReference declaration in declarations)
        {
            SyntaxNode syntax = declaration.GetSyntax(cancellationToken);
            documentation.AddDeclaration(syntax);
        }

        return documentation;
    }

    private static Location GetIdentifierLocation(MemberDeclarationSyntax declaration) =>
        declaration switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.GetLocation(),
            DelegateDeclarationSyntax @delegate => @delegate.Identifier.GetLocation(),
            _ => declaration.GetLocation()
        };

    private static bool IsEarlier(MemberDeclarationSyntax candidate, MemberDeclarationSyntax current)
    {
        int pathComparison = string.Compare(
            candidate.SyntaxTree.FilePath,
            current.SyntaxTree.FilePath,
            StringComparison.Ordinal);

        return pathComparison < 0
            || (pathComparison == 0 && candidate.SpanStart < current.SpanStart);
    }

    [Flags]
    private enum ApiSurface
    {
        Public = 1,
        Internal = 2,
        Private = 4,
        File = 8,
        All = Public | Internal | Private | File
    }
}