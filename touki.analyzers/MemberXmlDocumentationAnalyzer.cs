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
///  Requires XML documentation for members, parameters, and return values on configured accessibilities.
/// </summary>
/// <remarks>
///  <para>
///   Methods, constructors, operators, properties, indexers, fields, enum values, and events are analyzed.
///   Delegate signatures and primary-constructor parameters use the documentation attached to their type declaration.
///   Named C# extension-block receivers use documentation attached to the extension block, whose XML elements also
///   apply to the contained extension members.
///  </para>
///  <para>
///   Overrides and explicit interface implementations inherit documentation when a documented member can be found in
///   their hierarchy. A fully inspectable source hierarchy with no documentation requires local documentation; a
///   metadata hierarchy whose documentation is unavailable is left alone.
///  </para>
///  <para>
///   A top-level <c>&lt;inheritdoc&gt;</c> is valid only when its explicit <c>cref</c>, or the member's natural
///   override or interface target, resolves through any further inheritance to a top-level summary. Inheritdoc
///   elements with a <c>path</c> filter do not satisfy this rule.
///  </para>
///  <para>
///   <c>dotnet_code_quality.TOUKI0026.api_surface</c> filters on each member's declared accessibility.
///   For members declared in nested types, <c>dotnet_code_quality.TOUKI0026.effective_api_surface</c> can specify
///   a different set based on visibility through the containing-type hierarchy. Extension blocks use the combined
///   surface of their contained extension members.
///  </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MemberXmlDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    ///  The diagnostic identifier for incomplete member XML documentation.
    /// </summary>
    public const string DiagnosticId = "TOUKI0026";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls which declared member accessibilities are analyzed.
    /// </summary>
    public const string ApiSurfaceOption = "dotnet_code_quality.TOUKI0026.api_surface";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls effective accessibilities for members declared in nested types.
    /// </summary>
    public const string EffectiveApiSurfaceOption =
        "dotnet_code_quality.TOUKI0026.effective_api_surface";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls whether parameters require <c>&lt;param&gt;</c> elements.
    /// </summary>
    public const string RequireParameterDocumentationOption =
        "dotnet_code_quality.TOUKI0026.require_parameter_documentation";

    /// <summary>
    ///  The <c>.editorconfig</c> key that controls whether non-void returns require a <c>&lt;returns&gt;</c> element.
    /// </summary>
    public const string RequireReturnDocumentationOption =
        "dotnet_code_quality.TOUKI0026.require_return_documentation";

    private const string GeneratedCodeOption = "generated_code";

    private static readonly DiagnosticDescriptor s_rule = new(
        id: DiagnosticId,
        title: "Document members",
        messageFormat: "Member '{0}' documentation is incomplete: {1}",
        category: "Maintainability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Members on the configured API surface should document the member, its parameters, and its return value.",
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
                symbolContext => AnalyzeSymbol(
                    symbolContext,
                    compilationContext.Compilation,
                    generatedCodeAttribute,
                    compilerGeneratedAttribute),
                SymbolKind.Method,
                SymbolKind.Property,
                SymbolKind.Field,
                SymbolKind.Event,
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeSymbol(
        SymbolAnalysisContext context,
        Compilation compilation,
        INamedTypeSymbol? generatedCodeAttribute,
        INamedTypeSymbol? compilerGeneratedAttribute)
    {
        ISymbol symbol = context.Symbol;
        if (!IsCandidate(symbol) || IsPartialImplementation(symbol))
        {
            return;
        }

        ImmutableArray<SyntaxReference> declarations = GetDeclarations(symbol);
        bool isPrimaryConstructor = IsPrimaryConstructor(symbol, declarations, context.CancellationToken);
        bool isDelegate = symbol is INamedTypeSymbol { TypeKind: TypeKind.Delegate };
        bool isExtensionBlock = symbol is INamedTypeSymbol { IsExtension: true };
        if (symbol.IsImplicitlyDeclared && !isPrimaryConstructor)
        {
            return;
        }

        MemberDeclarationSyntax? reportDeclaration = null;
        XmlDocumentationInfo documentation = default;
        bool included = false;
        bool requireParameters = false;
        bool requireReturns = false;
        SyntaxNode? firstUserDeclaration = null;
        List<SyntaxNode>? additionalUserDeclarations = null;

        foreach (SyntaxReference reference in declarations)
        {
            SyntaxNode syntax = reference.GetSyntax(context.CancellationToken);
            SyntaxNode owner = XmlDocumentationInfo.GetDocumentationOwner(syntax);
            if (owner is not MemberDeclarationSyntax declaration
                || IsGeneratedDeclaration(
                    symbol,
                    declaration,
                    compilation,
                    context.Options.AnalyzerConfigOptionsProvider,
                    generatedCodeAttribute,
                    compilerGeneratedAttribute,
                    context.CancellationToken))
            {
                continue;
            }

            documentation.AddDeclaration(declaration);
            if (firstUserDeclaration is null)
            {
                firstUserDeclaration = declaration;
            }
            else
            {
                (additionalUserDeclarations ??= []).Add(declaration);
            }

            if (reportDeclaration is null || IsEarlier(declaration, reportDeclaration))
            {
                reportDeclaration = declaration;
            }

            AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(
                declaration.SyntaxTree);
            included |= IsIncluded(symbol, options);
            requireParameters |= GetBooleanOption(options, RequireParameterDocumentationOption, defaultValue: true);
            requireReturns |= GetBooleanOption(options, RequireReturnDocumentationOption, defaultValue: true);
        }

        if (reportDeclaration is null || firstUserDeclaration is null || !included)
        {
            return;
        }

        string displayName = GetDisplayName(symbol);
        Location memberLocation = GetMemberLocation(symbol, reportDeclaration);
        bool signatureOnly = isDelegate || isPrimaryConstructor || isExtensionBlock;
        ISymbol inheritanceSymbol = isPrimaryConstructor ? symbol.ContainingType : symbol;
        DocumentationAvailability inheritedSummary = documentation.HasInheritdoc
            ? GetInheritdocDocumentation(
            inheritanceSymbol,
                documentation,
                compilation,
                context.Options.AnalyzerConfigOptionsProvider,
                context.CancellationToken)
            : DocumentationAvailability.Undocumented;

        if (!signatureOnly && documentation.SummaryCount == 0)
        {
            if (inheritedSummary is DocumentationAvailability.Documented or DocumentationAvailability.Unknown)
            {
                return;
            }

            if (!documentation.HasInheritdoc)
            {
                DocumentationAvailability hierarchy = GetHierarchyDocumentation(
                    symbol,
                    compilation,
                    context.Options.AnalyzerConfigOptionsProvider,
                    context.CancellationToken);
                if (hierarchy is DocumentationAvailability.Documented or DocumentationAvailability.Unknown)
                {
                    return;
                }
            }

            string problem = documentation.HasInheritdoc
                ? "<inheritdoc> does not resolve to a top-level <summary>"
                : "missing <summary> or <inheritdoc>";
            context.ReportDiagnostic(Diagnostic.Create(s_rule, memberLocation, displayName, problem));
        }

        if (inheritedSummary is DocumentationAvailability.Documented or DocumentationAvailability.Unknown)
        {
            return;
        }

        if (requireParameters)
        {
            ImmutableArray<IParameterSymbol> parameters = GetParameters(symbol);
            for (int ordinal = 0; ordinal < parameters.Length; ordinal++)
            {
                IParameterSymbol parameter = parameters[ordinal];
                string parameterName = GetParameterName(firstUserDeclaration, ordinal, parameter.Name);
                if (parameterName.Length == 0)
                {
                    continue;
                }

                if (!HasParameterDocumentation(
                    firstUserDeclaration,
                    additionalUserDeclarations,
                    parameterName,
                    ordinal))
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            s_rule,
                            GetParameterLocation(firstUserDeclaration, ordinal)
                                ?? GetSourceLocation(parameter)
                                ?? memberLocation,
                            displayName,
                            $"missing <param> for parameter '{parameterName}'"));
                }
            }
        }

        if (requireReturns && HasReturnValue(symbol) && !documentation.HasReturns)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(s_rule, memberLocation, displayName, "missing <returns>"));
        }
    }

    private static bool IsCandidate(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol type => type.TypeKind == TypeKind.Delegate || type.IsExtension,
        IMethodSymbol method => method.MethodKind is
            MethodKind.Ordinary or
            MethodKind.Constructor or
            MethodKind.StaticConstructor or
            MethodKind.ExplicitInterfaceImplementation or
            MethodKind.UserDefinedOperator or
            MethodKind.Conversion,
        IPropertySymbol => true,
        IFieldSymbol field => field.AssociatedSymbol is null,
        IEventSymbol => true,
        _ => false
    };

    private static bool IsPartialImplementation(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.PartialDefinitionPart is not null,
        IPropertySymbol property => property.PartialDefinitionPart is not null,
        IEventSymbol @event => @event.PartialDefinitionPart is not null,
        _ => false
    };

    private static ImmutableArray<SyntaxReference> GetDeclarations(ISymbol symbol)
    {
        ImmutableArray<SyntaxReference> declarations = symbol.DeclaringSyntaxReferences;
        ISymbol? implementation = symbol switch
        {
            IMethodSymbol method => method.PartialImplementationPart,
            IPropertySymbol property => property.PartialImplementationPart,
            IEventSymbol @event => @event.PartialImplementationPart,
            _ => null
        };

        return implementation is null
            ? declarations
            : declarations.AddRange(implementation.DeclaringSyntaxReferences);
    }

    private static bool IsPrimaryConstructor(
        ISymbol symbol,
        ImmutableArray<SyntaxReference> declarations,
        CancellationToken cancellationToken)
    {
        if (symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor })
        {
            return false;
        }

        foreach (SyntaxReference declaration in declarations)
        {
            if (declaration.GetSyntax(cancellationToken) is TypeDeclarationSyntax { ParameterList: not null })
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<IParameterSymbol> GetParameters(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.Parameters,
        IPropertySymbol property => property.Parameters,
        INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: IMethodSymbol invoke } =>
            invoke.Parameters,
        INamedTypeSymbol { IsExtension: true, ExtensionParameter: IParameterSymbol receiver } => [receiver],
        _ => []
    };

    private static bool HasReturnValue(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => !method.ReturnsVoid,
        INamedTypeSymbol { TypeKind: TypeKind.Delegate, DelegateInvokeMethod: IMethodSymbol invoke } =>
            !invoke.ReturnsVoid,
        _ => false
    };

    private static bool HasParameterDocumentation(
        SyntaxNode firstDeclaration,
        List<SyntaxNode>? additionalDeclarations,
        string parameterName,
        int ordinal)
    {
        if (HasParameterDocumentation(firstDeclaration, parameterName, ordinal))
        {
            return true;
        }

        if (additionalDeclarations is not null)
        {
            foreach (SyntaxNode declaration in additionalDeclarations)
            {
                if (HasParameterDocumentation(declaration, parameterName, ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasParameterDocumentation(
        SyntaxNode declaration,
        string parameterName,
        int ordinal)
    {
        XmlDocumentationInfo documentation = default;
        documentation.AddDeclaration(declaration);
        SeparatedSyntaxList<ParameterSyntax> parameters = declaration switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
            IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList.Parameters,
            TypeDeclarationSyntax { ParameterList: ParameterListSyntax list } => list.Parameters,
            _ => default
        };
        string declarationParameterName = ordinal < parameters.Count
            ? parameters[ordinal].Identifier.ValueText
            : parameterName;
        return documentation.HasParameter(declarationParameterName);
    }

    private static Location? GetParameterLocation(SyntaxNode declaration, int ordinal)
    {
        SeparatedSyntaxList<ParameterSyntax> parameters = GetParameterSyntaxes(declaration);
        return ordinal < parameters.Count ? parameters[ordinal].Identifier.GetLocation() : null;
    }

    private static string GetParameterName(SyntaxNode declaration, int ordinal, string fallback)
    {
        SeparatedSyntaxList<ParameterSyntax> parameters = GetParameterSyntaxes(declaration);
        return ordinal < parameters.Count ? parameters[ordinal].Identifier.ValueText : fallback;
    }

    private static SeparatedSyntaxList<ParameterSyntax> GetParameterSyntaxes(SyntaxNode declaration) =>
        declaration switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
            IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
            DelegateDeclarationSyntax @delegate => @delegate.ParameterList.Parameters,
            TypeDeclarationSyntax { ParameterList: ParameterListSyntax list } => list.Parameters,
            _ => default
        };

    private static DocumentationAvailability GetInheritdocDocumentation(
        ISymbol symbol,
        XmlDocumentationInfo documentation,
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken) =>
        DocumentationInheritanceResolver.GetInheritdocDocumentation(
            symbol,
            documentation,
            compilation,
            (target, declaration, declaringCompilation) => !IsGeneratedDeclaration(
                target,
                declaration,
                declaringCompilation,
                optionsProvider,
                declaringCompilation.GetTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute"),
                declaringCompilation.GetTypeByMetadataName(
                    "System.Runtime.CompilerServices.CompilerGeneratedAttribute"),
                cancellationToken),
            cancellationToken);

    private static DocumentationAvailability GetHierarchyDocumentation(
        ISymbol symbol,
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        CancellationToken cancellationToken) =>
        DocumentationInheritanceResolver.GetHierarchyDocumentation(
            symbol,
            compilation,
            (target, declaration, declaringCompilation) => !IsGeneratedDeclaration(
                target,
                declaration,
                declaringCompilation,
                optionsProvider,
                declaringCompilation.GetTypeByMetadataName("System.CodeDom.Compiler.GeneratedCodeAttribute"),
                declaringCompilation.GetTypeByMetadataName(
                    "System.Runtime.CompilerServices.CompilerGeneratedAttribute"),
                cancellationToken),
            cancellationToken);

    /// <summary>
    ///  Attempts to classify bounded metadata XML documentation for security and boundary tests.
    /// </summary>
    /// <param name="xml">The metadata XML fragment to inspect.</param>
    /// <param name="cancellationToken">A token that can cancel parsing.</param>
    /// <param name="hasDocumentation">
    ///  <see langword="true"/> when a top-level summary or inheritdoc element was found.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> when the input was within bounds and well formed; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryHasMetadataDocumentation(
        string xml,
        CancellationToken cancellationToken,
        out bool hasDocumentation) =>
        DocumentationInheritanceResolver.TryHasMetadataDocumentation(
            xml,
            cancellationToken,
            out hasDocumentation);

    private static bool IsGeneratedDeclaration(
        ISymbol symbol,
        MemberDeclarationSyntax declaration,
        Compilation compilation,
        AnalyzerConfigOptionsProvider optionsProvider,
        INamedTypeSymbol? generatedCodeAttribute,
        INamedTypeSymbol? compilerGeneratedAttribute,
        CancellationToken cancellationToken)
    {
        ISymbol attributeSymbol = declaration is TypeDeclarationSyntax && symbol is IMethodSymbol
            ? symbol.ContainingType
            : symbol;
        if (HasGeneratedAttribute(
            attributeSymbol,
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

        INamedTypeSymbol? containingType = declaration is TypeDeclarationSyntax && symbol is IMethodSymbol
            ? symbol.ContainingType?.ContainingType
            : symbol.ContainingType;
        if (containingType?.IsExtension == true)
        {
            containingType = containingType.ContainingType;
        }

        MemberDeclarationSyntax? containingDeclaration = GetContainingTypeDeclaration(declaration);
        while (containingType is not null && containingDeclaration is not null)
        {
            if (HasGeneratedAttribute(
                    containingType,
                    containingDeclaration,
                    generatedCodeAttribute,
                    compilerGeneratedAttribute,
                    cancellationToken)
                || containingDeclaration.SyntaxTree is CSharpSyntaxTree containingTree
                    && containingTree.GetLineVisibility(
                        containingDeclaration.SpanStart,
                        cancellationToken) == LineVisibility.Hidden)
            {
                return true;
            }

            containingType = containingType.ContainingType;
            containingDeclaration = GetContainingTypeDeclaration(containingDeclaration);
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
            if (current is BaseTypeDeclarationSyntax containingType
                && !containingType.Identifier.IsKind(SyntaxKind.None))
            {
                return containingType;
            }
        }

        return null;
    }

    private static bool HasGeneratedAttribute(
        ISymbol symbol,
        MemberDeclarationSyntax declaration,
        INamedTypeSymbol? generatedCodeAttribute,
        INamedTypeSymbol? compilerGeneratedAttribute,
        CancellationToken cancellationToken)
    {
        if (declaration.AttributeLists.Count == 0
            || (generatedCodeAttribute is null && compilerGeneratedAttribute is null))
        {
            return false;
        }

        foreach (AttributeData attribute in symbol.GetAttributes())
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

    private static bool IsIncluded(ISymbol symbol, AnalyzerConfigOptions options)
    {
        bool useEffectiveSurface = IsDeclaredInNestedType(symbol)
            && options.TryGetValue(EffectiveApiSurfaceOption, out _);
        ApiSurface configuredSurface = GetConfiguredApiSurface(
            options,
            useEffectiveSurface ? EffectiveApiSurfaceOption : ApiSurfaceOption);
        ApiSurface symbolSurface = useEffectiveSurface
            ? GetEffectiveApiSurface(symbol)
            : GetDeclaredApiSurface(symbol);
        return (configuredSurface & symbolSurface) != 0;
    }

    private static bool IsDeclaredInNestedType(ISymbol symbol)
    {
        INamedTypeSymbol? declaredType = symbol switch
        {
            INamedTypeSymbol { IsExtension: true } extension => extension.ContainingType,
            INamedTypeSymbol type => type,
            _ when symbol.ContainingType?.IsExtension == true => symbol.ContainingType.ContainingType,
            _ => symbol.ContainingType
        };

        return declaredType?.ContainingType is not null;
    }

    private static ApiSurface GetConfiguredApiSurface(AnalyzerConfigOptions options, string option)
    {
        if (!options.TryGetValue(option, out string? configured)
            || string.IsNullOrWhiteSpace(configured))
        {
            return ApiSurface.Default;
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

            int length = tokenEnd - tokenStart;
            if (TokenEquals(configured, tokenStart, length, "all"))
            {
                result |= ApiSurface.All;
            }
            else if (TokenEquals(configured, tokenStart, length, "public"))
            {
                result |= ApiSurface.Public;
            }
            else if (TokenEquals(configured, tokenStart, length, "internal"))
            {
                result |= ApiSurface.Internal;
            }
            else if (TokenEquals(configured, tokenStart, length, "private"))
            {
                result |= ApiSurface.Private;
            }
            else
            {
                return ApiSurface.Default;
            }

            if (separator < 0)
            {
                break;
            }

            tokenStart = separator + 1;
        }

        return result == 0 ? ApiSurface.Default : result;
    }

    private static ApiSurface GetDeclaredApiSurface(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol { IsExtension: true } extension)
        {
            ApiSurface extensionSurface = 0;
            foreach (ISymbol member in extension.GetMembers())
            {
                if (!member.IsImplicitlyDeclared && IsCandidate(member))
                {
                    extensionSurface |= GetDeclaredApiSurface(member);
                }
            }

            return extensionSurface;
        }

        return symbol.DeclaredAccessibility switch
        {
            Accessibility.Private => ApiSurface.Private,
            Accessibility.Internal or Accessibility.ProtectedAndInternal => ApiSurface.Internal,
            _ => ApiSurface.Public
        };
    }

    private static ApiSurface GetEffectiveApiSurface(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol { IsExtension: true } extension)
        {
            ApiSurface extensionSurface = 0;
            foreach (ISymbol member in extension.GetMembers())
            {
                if (!member.IsImplicitlyDeclared && IsCandidate(member))
                {
                    extensionSurface |= GetEffectiveApiSurface(member);
                }
            }

            return extensionSurface;
        }

        ApiSurface surface = ApiSurface.Public;

        for (ISymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Private:
                    return ApiSurface.Private;
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    surface = ApiSurface.Internal;
                    break;
            }
        }

        return surface;
    }

    private static bool GetBooleanOption(
        AnalyzerConfigOptions options,
        string key,
        bool defaultValue) =>
        options.TryGetValue(key, out string? configured) && bool.TryParse(configured.Trim(), out bool value)
            ? value
            : defaultValue;

    private static bool TokenEquals(string value, int start, int length, string expected) =>
        length == expected.Length
        && string.Compare(value, start, expected, 0, length, StringComparison.OrdinalIgnoreCase) == 0;

    private static string GetDisplayName(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol { IsExtension: true, ExtensionParameter: IParameterSymbol receiver } =>
            receiver.Name.Length == 0 ? "extension" : $"extension({receiver.Name})",
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } method =>
            method.ContainingType.Name,
        IPropertySymbol { IsIndexer: true } => "this[]",
        _ => symbol.Name
    };

    private static Location GetMemberLocation(ISymbol symbol, MemberDeclarationSyntax declaration)
    {
        foreach (Location location in symbol.Locations)
        {
            if (location.SourceTree == declaration.SyntaxTree
                && declaration.FullSpan.Contains(location.SourceSpan))
            {
                return location;
            }
        }

        return declaration.GetLocation();
    }

    private static Location? GetSourceLocation(ISymbol symbol)
    {
        foreach (Location location in symbol.Locations)
        {
            if (location.IsInSource)
            {
                return location;
            }
        }

        return null;
    }

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
        Default = Public | Internal,
        All = Default | Private
    }
}