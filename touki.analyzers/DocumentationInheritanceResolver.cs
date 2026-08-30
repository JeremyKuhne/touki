// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Touki.Analyzers;

/// <summary>
///  Resolves XML documentation inheritance to declarations that contain required top-level elements.
/// </summary>
internal static partial class DocumentationInheritanceResolver
{
    private const int MaximumMetadataDocumentationLength = 1024 * 1024;
    private const int MaximumMetadataDocumentationNodes = 4096;
    private const int MaximumMetadataDocumentationDepth = 128;
    private const int MaximumDocumentationIdLength = 4096;
    private const int MaximumDocumentationIdDepth = 128;
    private const int MaximumDocumentationIdContexts = 4;
    private const int MaximumDocumentationIdDelimiters = 256;

    public static DocumentationAvailability GetInheritdocDocumentation(
        ISymbol symbol,
        XmlDocumentationInfo documentation,
        Compilation compilation,
        Func<ISymbol, MemberDeclarationSyntax, Compilation, bool>? includeSourceDeclaration,
        CancellationToken cancellationToken)
    {
        List<PendingSymbol> pending = [];
        AddInheritdocTargets(symbol, documentation, compilation, pending, cancellationToken);
        return GetDocumentation(pending, includeSourceDeclaration, cancellationToken);
    }

    public static DocumentationAvailability GetHierarchyDocumentation(
        ISymbol symbol,
        Compilation compilation,
        Func<ISymbol, MemberDeclarationSyntax, Compilation, bool>? includeSourceDeclaration,
        CancellationToken cancellationToken)
    {
        List<PendingSymbol> pending = [];
        AddNaturalTargets(
            symbol,
            compilation,
            pending,
            includeImplicitInterfaceTargets: false,
            cancellationToken);
        return GetDocumentation(
            pending,
            includeSourceDeclaration,
            cancellationToken);
    }

    private static DocumentationAvailability GetDocumentation(
        List<PendingSymbol> pending,
        Func<ISymbol, MemberDeclarationSyntax, Compilation, bool>? includeSourceDeclaration,
        CancellationToken cancellationToken)
    {
        HashSet<ISymbol> inspected = new(SymbolEqualityComparer.Default);
        HashSet<ISymbol> expandedHierarchies = new(SymbolEqualityComparer.Default);
        Dictionary<Compilation, Compilation> aliasNormalizedCompilations = new();
        bool unknown = false;

        for (int index = 0; index < pending.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PendingSymbol item = pending[index];
            ISymbol current = item.Symbol;

            if (inspected.Add(current))
            {
                SourceDocumentation sourceDocumentation = GetSourceDocumentation(
                    current,
                    item.Compilation,
                    includeSourceDeclaration,
                    cancellationToken);
                if (sourceDocumentation.HasCSharpDeclaration)
                {
                    if (sourceDocumentation.Documentation.SummaryCount > 0)
                    {
                        return DocumentationAvailability.Documented;
                    }

                    AddInheritdocTargets(
                        current,
                        sourceDocumentation.Documentation,
                        item.Compilation,
                        pending,
                        cancellationToken);
                }
                else
                {
                    string? xml = current.GetDocumentationCommentXml(
                        preferredCulture: null,
                        expandIncludes: false,
                        cancellationToken: cancellationToken);
                    if (xml is null || xml.Length == 0)
                    {
                        unknown |= IsDocumentationUnavailable(current, item.Compilation);
                    }
                    else if (!TryParseMetadataDocumentation(
                        xml,
                        cancellationToken,
                        out MetadataDocumentationInfo metadataDocumentation))
                    {
                        unknown = true;
                    }
                    else
                    {
                        if (metadataDocumentation.HasSummary)
                        {
                            return DocumentationAvailability.Documented;
                        }

                        unknown |= AddMetadataInheritdocTargets(
                            current,
                            metadataDocumentation,
                            item.Compilation,
                            aliasNormalizedCompilations,
                            pending,
                            cancellationToken);
                    }
                }
            }

            if (item.FollowHierarchy && expandedHierarchies.Add(current))
            {
                AddNaturalTargets(
                    current,
                    item.Compilation,
                    pending,
                    item.IncludeImplicitInterfaceTargets,
                    cancellationToken);
            }
        }

        return unknown
            ? DocumentationAvailability.Unknown
            : DocumentationAvailability.Undocumented;
    }

    private static bool IsDocumentationUnavailable(ISymbol symbol, Compilation compilation)
    {
        if (!symbol.DeclaringSyntaxReferences.IsDefaultOrEmpty)
        {
            return false;
        }

        IAssemblySymbol? assembly = symbol.ContainingAssembly;
        return assembly is not null
            && !SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly)
                && compilation.GetMetadataReference(assembly) is not CompilationReference;
    }

    private static SourceDocumentation GetSourceDocumentation(
        ISymbol symbol,
        Compilation compilation,
        Func<ISymbol, MemberDeclarationSyntax, Compilation, bool>? includeSourceDeclaration,
        CancellationToken cancellationToken)
    {
        XmlDocumentationInfo documentation = default;
        bool hasCSharpDeclaration = false;

        foreach (SyntaxReference reference in GetDeclarations(symbol))
        {
            SyntaxNode syntax = reference.GetSyntax(cancellationToken);
            SyntaxNode owner = XmlDocumentationInfo.GetDocumentationOwner(syntax);
            if (owner is not MemberDeclarationSyntax declaration)
            {
                continue;
            }

            hasCSharpDeclaration = true;
            if (includeSourceDeclaration is not null
                && !includeSourceDeclaration(symbol, declaration, compilation))
            {
                continue;
            }

            documentation.AddDeclaration(declaration);
        }

        return new(documentation, hasCSharpDeclaration);
    }

    private static IEnumerable<SyntaxReference> GetDeclarations(ISymbol symbol)
    {
        foreach (SyntaxReference declaration in symbol.DeclaringSyntaxReferences)
        {
            yield return declaration;
        }

        ISymbol? implementation = symbol switch
        {
            IMethodSymbol method => method.PartialImplementationPart,
            IPropertySymbol property => property.PartialImplementationPart,
            IEventSymbol @event => @event.PartialImplementationPart,
            _ => null
        };

        if (implementation is not null)
        {
            foreach (SyntaxReference declaration in implementation.DeclaringSyntaxReferences)
            {
                yield return declaration;
            }
        }
    }

    private static void AddInheritdocTargets(
        ISymbol symbol,
        XmlDocumentationInfo documentation,
        Compilation compilation,
        List<PendingSymbol> pending,
        CancellationToken cancellationToken)
    {
        bool addedNaturalTargets = false;
        HashSet<ISymbol> addedExplicitTargets = new(SymbolEqualityComparer.Default);
        for (int index = 0; index < documentation.InheritdocCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InheritdocReference inheritdoc = documentation.GetInheritdoc(index);
            if (inheritdoc.HasPath)
            {
                continue;
            }

            if (inheritdoc.Target is null)
            {
                if (!addedNaturalTargets)
                {
                    ISymbol inheritanceSymbol = IsPrimaryConstructor(symbol, cancellationToken)
                        ? symbol.ContainingType
                        : symbol;
                    AddNaturalTargets(
                        inheritanceSymbol,
                        compilation,
                        pending,
                        includeImplicitInterfaceTargets: true,
                        cancellationToken);
                    addedNaturalTargets = true;
                }

                continue;
            }

            SemanticModel semanticModel = compilation.GetSemanticModel(inheritdoc.Target.SyntaxTree);
            ISymbol? target = semanticModel.GetSymbolInfo(inheritdoc.Target, cancellationToken).Symbol;
            if (target is not null && addedExplicitTargets.Add(target))
            {
                AddPending(target, compilation, pending, followHierarchy: false);
            }
        }
    }

    private static bool IsPrimaryConstructor(ISymbol symbol, CancellationToken cancellationToken)
    {
        if (symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor })
        {
            return false;
        }

        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(cancellationToken) is TypeDeclarationSyntax { ParameterList: not null })
            {
                return true;
            }
        }

        return false;
    }

    private static bool AddMetadataInheritdocTargets(
        ISymbol symbol,
        MetadataDocumentationInfo documentation,
        Compilation compilation,
        Dictionary<Compilation, Compilation> aliasNormalizedCompilations,
        List<PendingSymbol> pending,
        CancellationToken cancellationToken)
    {
        bool unknown = false;

        if (documentation.HasImplicitInheritdoc)
        {
            AddNaturalTargets(
                symbol,
                compilation,
                pending,
                includeImplicitInterfaceTargets: true,
                cancellationToken);
        }

        if (documentation.InheritdocReferences is null)
        {
            return false;
        }

        HashSet<string> resolvedDocumentationIds = new(StringComparer.Ordinal);
        foreach (MetadataInheritdocReference inheritdoc in documentation.InheritdocReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (inheritdoc.HasPath || !resolvedDocumentationIds.Add(inheritdoc.Target))
            {
                continue;
            }

            if (!IsSafeDocumentationId(inheritdoc.Target))
            {
                unknown = true;
                continue;
            }

            DocumentationTargetResolution resolution = ResolveMetadataDocumentationTarget(
                symbol,
                inheritdoc.Target,
                compilation,
                aliasNormalizedCompilations,
                out ISymbol? target,
                out Compilation resolutionCompilation);
            if (resolution == DocumentationTargetResolution.Resolved)
            {
                AddPending(target!, resolutionCompilation, pending, followHierarchy: false);
            }

            unknown |= resolution == DocumentationTargetResolution.Ambiguous;
        }

        return unknown;
    }

    private static DocumentationTargetResolution ResolveMetadataDocumentationTarget(
        ISymbol source,
        string documentationId,
        Compilation compilation,
        Dictionary<Compilation, Compilation> aliasNormalizedCompilations,
        out ISymbol? target,
        out Compilation resolutionCompilation)
    {
        resolutionCompilation = IncludeAliasOnlyReferences(compilation, aliasNormalizedCompilations);
        ImmutableArray<ISymbol> candidates = DocumentationCommentId.GetSymbolsForDeclarationId(
            documentationId,
            resolutionCompilation);
        if (candidates.IsEmpty)
        {
            candidates = DocumentationCommentId.GetSymbolsForReferenceId(
                documentationId,
                resolutionCompilation);
        }

        ISymbol? sameAssemblyTarget = null;
        bool sameAssemblyAmbiguous = false;
        target = null;
        bool ambiguous = false;
        foreach (ISymbol candidate in candidates)
        {
            if (target is null)
            {
                target = candidate;
            }
            else if (!SymbolEqualityComparer.Default.Equals(target, candidate))
            {
                ambiguous = true;
            }

            if (candidate.ContainingAssembly?.Identity.Equals(source.ContainingAssembly?.Identity) != true)
            {
                continue;
            }

            if (sameAssemblyTarget is null)
            {
                sameAssemblyTarget = candidate;
            }
            else if (!SymbolEqualityComparer.Default.Equals(sameAssemblyTarget, candidate))
            {
                sameAssemblyAmbiguous = true;
            }
        }

        if (sameAssemblyTarget is not null)
        {
            target = sameAssemblyAmbiguous ? null : sameAssemblyTarget;
            return sameAssemblyAmbiguous
                ? DocumentationTargetResolution.Ambiguous
                : DocumentationTargetResolution.Resolved;
        }

        return target is null
            ? DocumentationTargetResolution.Unresolved
            : ambiguous
                ? DocumentationTargetResolution.Ambiguous
                : DocumentationTargetResolution.Resolved;
    }

    private static Compilation IncludeAliasOnlyReferences(
        Compilation compilation,
        Dictionary<Compilation, Compilation> aliasNormalizedCompilations)
    {
        if (aliasNormalizedCompilations.TryGetValue(compilation, out Compilation? normalizedCompilation))
        {
            return normalizedCompilation;
        }

        normalizedCompilation = compilation;
        foreach (MetadataReference reference in compilation.References)
        {
            ImmutableArray<string> aliases = reference.Properties.Aliases;
            if (aliases.IsDefaultOrEmpty || aliases.Contains("global", StringComparer.Ordinal))
            {
                continue;
            }

            normalizedCompilation = normalizedCompilation.ReplaceReference(
                reference,
                reference.WithAliases(aliases.Add("global")));
        }

        aliasNormalizedCompilations.Add(compilation, normalizedCompilation);
        return normalizedCompilation;
    }

    internal static bool IsSafeDocumentationId(string value)
    {
        if (value.Length is 0 or > MaximumDocumentationIdLength)
        {
            return false;
        }

        int depth = 0;
        int contextCount = 0;
        int delimiterCount = 0;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            switch (character)
            {
                case '(':
                case '[':
                case '{':
                    delimiterCount++;
                    if (++depth > MaximumDocumentationIdDepth)
                    {
                        return false;
                    }

                    break;
                case ')':
                case ']':
                case '}':
                    delimiterCount++;
                    if (depth > 0)
                    {
                        depth--;
                    }

                    break;
                case ':':
                case '.':
                case ',':
                case '\'':
                case '@':
                case '*':
                case '`':
                case '~':
                    delimiterCount++;
                    break;
            }

            if (index + 1 < value.Length
                && (character is 'M' or 'T')
                && value[index + 1] == ':'
                && ++contextCount > MaximumDocumentationIdContexts)
            {
                return false;
            }

            if (delimiterCount > MaximumDocumentationIdDelimiters)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddNaturalTargets(
        ISymbol symbol,
        Compilation compilation,
        List<PendingSymbol> pending,
        bool includeImplicitInterfaceTargets,
        CancellationToken cancellationToken)
    {
        switch (symbol)
        {
            case INamedTypeSymbol type:
                AddTypeTargets(type, compilation, pending, includeImplicitInterfaceTargets, cancellationToken);
                break;
            case IMethodSymbol method:
                AddMemberTargets(
                    method,
                    method.OverriddenMethod,
                    method.ExplicitInterfaceImplementations,
                    compilation,
                    pending,
                    includeImplicitInterfaceTargets,
                    cancellationToken);
                break;
            case IPropertySymbol property:
                AddMemberTargets(
                    property,
                    property.OverriddenProperty,
                    property.ExplicitInterfaceImplementations,
                    compilation,
                    pending,
                    includeImplicitInterfaceTargets,
                    cancellationToken);
                break;
            case IEventSymbol @event:
                AddMemberTargets(
                    @event,
                    @event.OverriddenEvent,
                    @event.ExplicitInterfaceImplementations,
                    compilation,
                    pending,
                    includeImplicitInterfaceTargets,
                    cancellationToken);
                break;
        }
    }

    private static void AddTypeTargets(
        INamedTypeSymbol type,
        Compilation compilation,
        List<PendingSymbol> pending,
        bool includeImplicitInterfaceTargets,
        CancellationToken cancellationToken)
    {
        if (type.TypeKind is TypeKind.Enum or TypeKind.Delegate)
        {
            return;
        }

        bool hasCSharpDeclaration = false;
        foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reference.GetSyntax(cancellationToken) is not BaseTypeDeclarationSyntax declaration)
            {
                continue;
            }

            hasCSharpDeclaration = true;
            if (declaration.BaseList is null)
            {
                continue;
            }

            SemanticModel semanticModel = compilation.GetSemanticModel(declaration.SyntaxTree);
            foreach (BaseTypeSyntax baseTypeSyntax in declaration.BaseList.Types)
            {
                if (semanticModel.GetTypeInfo(baseTypeSyntax.Type, cancellationToken).Type
                    is INamedTypeSymbol target)
                {
                    AddPending(target, compilation, pending, followHierarchy: true, includeImplicitInterfaceTargets);
                }
            }
        }

        if (hasCSharpDeclaration)
        {
            return;
        }

        if (type.TypeKind == TypeKind.Class
            && type.BaseType is INamedTypeSymbol { SpecialType: not SpecialType.System_Object } baseType)
        {
            AddPending(baseType, compilation, pending, followHierarchy: true, includeImplicitInterfaceTargets);
        }

        foreach (INamedTypeSymbol @interface in type.Interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddPending(@interface, compilation, pending, followHierarchy: true, includeImplicitInterfaceTargets);
        }
    }

    private static void AddMemberTargets<TSymbol>(
        TSymbol symbol,
        TSymbol? overridden,
        IEnumerable<TSymbol> explicitImplementations,
        Compilation compilation,
        List<PendingSymbol> pending,
        bool includeImplicitInterfaceTargets,
        CancellationToken cancellationToken)
        where TSymbol : class, ISymbol
    {
        if (overridden is not null)
        {
            AddPending(overridden, compilation, pending, followHierarchy: true, includeImplicitInterfaceTargets);
        }

        foreach (TSymbol implementation in explicitImplementations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddPending(implementation, compilation, pending, followHierarchy: true, includeImplicitInterfaceTargets);
        }

        if (!includeImplicitInterfaceTargets || symbol.ContainingType is not INamedTypeSymbol containingType)
        {
            return;
        }

        foreach (INamedTypeSymbol @interface in containingType.AllInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (ISymbol interfaceMember in @interface.GetMembers(symbol.Name))
            {
                ISymbol? implementation = containingType.FindImplementationForInterfaceMember(interfaceMember);
                if (SymbolEqualityComparer.Default.Equals(implementation, symbol))
                {
                    AddPending(
                        interfaceMember,
                        compilation,
                        pending,
                        followHierarchy: true,
                        includeImplicitInterfaceTargets);
                }
            }
        }
    }

    private static void AddPending(
        ISymbol symbol,
        Compilation compilation,
        List<PendingSymbol> pending,
        bool followHierarchy,
        bool includeImplicitInterfaceTargets = false) =>
        pending.Add(
            new(
                symbol,
                GetOwningCompilation(symbol, compilation),
                followHierarchy,
                includeImplicitInterfaceTargets));

    private static Compilation GetOwningCompilation(ISymbol symbol, Compilation compilation)
    {
        IAssemblySymbol? assembly = symbol.ContainingAssembly;
        if (assembly is null || SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly))
        {
            return compilation;
        }

        return compilation.GetMetadataReference(assembly) is CompilationReference reference
            ? reference.Compilation
            : compilation;
    }

    internal static bool TryHasMetadataDocumentation(
        string xml,
        CancellationToken cancellationToken,
        out bool hasDocumentation)
    {
        bool parsed = TryParseMetadataDocumentation(
            xml,
            cancellationToken,
            out MetadataDocumentationInfo documentation);
        hasDocumentation = parsed && (documentation.HasSummary || documentation.HasInheritdoc);
        return parsed;
    }

    private static bool TryParseMetadataDocumentation(
        string xml,
        CancellationToken cancellationToken,
        out MetadataDocumentationInfo documentation)
    {
        documentation = default;
        if (xml.Length > MaximumMetadataDocumentationLength)
        {
            return false;
        }

        XmlReaderSettings settings = new()
        {
            ConformanceLevel = ConformanceLevel.Fragment,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = MaximumMetadataDocumentationLength,
            XmlResolver = null
        };

        try
        {
            using StringReader textReader = new(xml);
            using XmlReader reader = XmlReader.Create(textReader, settings);
            int nodeCount = 0;
            int memberDepth = -1;

            while (reader.Read())
            {
                if ((nodeCount++ & 0x3F) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                if (nodeCount > MaximumMetadataDocumentationNodes
                    || reader.Depth > MaximumMetadataDocumentationDepth)
                {
                    documentation = default;
                    return false;
                }

                if (reader.NodeType == XmlNodeType.Element && reader.Prefix.Length == 0)
                {
                    if (reader.LocalName == "member" && memberDepth < 0 && reader.Depth == 0)
                    {
                        memberDepth = reader.Depth;
                    }
                    else if (memberDepth < 0 && reader.Depth == 0
                        || memberDepth >= 0 && reader.Depth == memberDepth + 1)
                    {
                        switch (reader.LocalName)
                        {
                            case "summary":
                                documentation.HasSummary = true;
                                break;
                            case "inheritdoc":
                                documentation.HasInheritdoc = true;
                                string? cref = reader.GetAttribute("cref");
                                bool hasPath = reader.GetAttribute("path") is not null;
                                if (cref is null)
                                {
                                    if (!hasPath)
                                    {
                                        documentation.HasImplicitInheritdoc = true;
                                    }
                                }
                                else if (cref.Length > 0)
                                {
                                    (documentation.InheritdocReferences ??= []).Add(new(cref, hasPath));
                                }

                                break;
                        }
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement
                    && reader.Prefix.Length == 0
                    && reader.LocalName == "member"
                    && reader.Depth == memberDepth)
                {
                    memberDepth = -1;
                }
            }

            return true;
        }
        catch (XmlException)
        {
            documentation = default;
            return false;
        }
    }

}
