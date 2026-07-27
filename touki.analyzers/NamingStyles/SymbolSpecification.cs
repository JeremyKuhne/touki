// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  Describes the set of symbols a <see cref="NamingRule"/> applies to.
/// </summary>
/// <remarks>
///  <para>
///   Ported from <c>Microsoft.CodeAnalysis.NamingStyles.SymbolSpecification</c> in dotnet/roslyn, with the
///   option serialization members dropped and three capabilities added that upstream does not have:
///   excluded modifiers (dotnet/roslyn#18354), attribute matching (dotnet/roslyn#32955), and a wider set of
///   modifiers (dotnet/roslyn#13250).
///  </para>
/// </remarks>
internal sealed class SymbolSpecification(
    string name,
    ImmutableArray<SymbolSpecification.SymbolKindOrTypeKind> symbolKinds,
    ImmutableArray<Accessibility> accessibilities,
    ImmutableArray<SymbolSpecification.ModifierKind> requiredModifiers,
    ImmutableArray<SymbolSpecification.ModifierKind> excludedModifiers,
    ImmutableArray<string> requiredAttributes,
    ImmutableArray<string> excludedAttributes)
{
    private const string AttributeSuffix = "Attribute";

    /// <summary>
    ///  The name of the symbol group as written in the <c>.editorconfig</c>.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///  The symbol kinds this specification applies to. Empty matches every kind.
    /// </summary>
    public ImmutableArray<SymbolKindOrTypeKind> ApplicableSymbolKindList { get; } = symbolKinds;

    /// <summary>
    ///  The accessibilities this specification applies to. Empty matches every accessibility.
    /// </summary>
    public ImmutableArray<Accessibility> ApplicableAccessibilityList { get; } = accessibilities;

    /// <summary>
    ///  Modifiers a symbol must have for this specification to apply.
    /// </summary>
    public ImmutableArray<ModifierKind> RequiredModifierList { get; } = requiredModifiers;

    /// <summary>
    ///  Modifiers that prevent this specification from applying.
    /// </summary>
    public ImmutableArray<ModifierKind> ExcludedModifierList { get; } = excludedModifiers;

    /// <summary>
    ///  Attributes a symbol must carry for this specification to apply.
    /// </summary>
    public ImmutableArray<string> RequiredAttributeList { get; } = requiredAttributes;

    /// <summary>
    ///  Attributes that prevent this specification from applying.
    /// </summary>
    public ImmutableArray<string> ExcludedAttributeList { get; } = excludedAttributes;

    /// <summary>
    ///  Returns <see langword="true"/> when this specification applies to <paramref name="symbol"/>.
    /// </summary>
    public bool AppliesTo(ISymbol symbol) =>
        AnyMatches(ApplicableSymbolKindList, symbol)
        && AllModifiersMatch(RequiredModifierList, symbol, required: true)
        && AllModifiersMatch(ExcludedModifierList, symbol, required: false)
        && AccessibilityMatches(symbol)
        && AttributesMatch(symbol);

    private static bool AnyMatches(ImmutableArray<SymbolKindOrTypeKind> kinds, ISymbol symbol)
    {
        if (kinds.IsEmpty)
        {
            return true;
        }

        foreach (SymbolKindOrTypeKind kind in kinds)
        {
            if (kind.MatchesSymbol(symbol))
            {
                return true;
            }
        }

        return false;
    }

    private bool AccessibilityMatches(ISymbol symbol)
    {
        if (ApplicableAccessibilityList.IsEmpty)
        {
            return true;
        }

        Accessibility accessibility = symbol.DeclaredAccessibility;

        foreach (Accessibility applicable in ApplicableAccessibilityList)
        {
            if (applicable == accessibility)
            {
                return true;
            }
        }

        return false;
    }

    private static bool AllModifiersMatch(ImmutableArray<ModifierKind> modifiers, ISymbol symbol, bool required)
    {
        foreach (ModifierKind modifier in modifiers)
        {
            if (HasModifier(symbol, modifier) != required)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasModifier(ISymbol symbol, ModifierKind modifier) => modifier switch
    {
        // DEVIATION from dotnet/roslyn: a const field reports IsStatic and IsReadOnly as true because const
        // implies both in the language, so upstream's `required_modifiers = static` silently matches const
        // fields and forces the s_ prefix onto them. See dotnet/roslyn#23884, dotnet/roslyn#15428 and
        // dotnet/roslyn#23391. Here `const` is only ever matched by `const`.
        ModifierKind.Static => symbol.IsStatic && !IsConst(symbol),
        ModifierKind.ReadOnly => IsReadOnly(symbol) && !IsConst(symbol),
        ModifierKind.Const => IsConst(symbol),
        ModifierKind.Abstract => symbol.IsAbstract,
        ModifierKind.Async => symbol is IMethodSymbol { IsAsync: true },
        ModifierKind.Sealed => symbol.IsSealed,
        ModifierKind.Virtual => symbol.IsVirtual,
        ModifierKind.Override => symbol.IsOverride,
        ModifierKind.Extern => symbol.IsExtern,
        ModifierKind.Volatile => symbol is IFieldSymbol { IsVolatile: true },
        ModifierKind.Required => symbol switch
        {
            IFieldSymbol field => field.IsRequired,
            IPropertySymbol property => property.IsRequired,
            _ => false
        },
        _ => false
    };

    private static bool IsConst(ISymbol symbol) => symbol switch
    {
        IFieldSymbol field => field.IsConst,
        ILocalSymbol local => local.IsConst,
        _ => false
    };

    private static bool IsReadOnly(ISymbol symbol) => symbol switch
    {
        IFieldSymbol field => field.IsReadOnly,
        IPropertySymbol property => property.IsReadOnly,
        IMethodSymbol method => method.IsReadOnly,
        INamedTypeSymbol type => type.IsReadOnly,
        _ => false
    };

    private bool AttributesMatch(ISymbol symbol)
    {
        if (RequiredAttributeList.IsEmpty && ExcludedAttributeList.IsEmpty)
        {
            // Nothing to check. Deliberately avoids calling GetAttributes, which materializes the attribute bag.
            return true;
        }

        ImmutableArray<AttributeData> attributes = symbol.GetAttributes();

        foreach (string required in RequiredAttributeList)
        {
            if (!HasAttribute(attributes, required))
            {
                return false;
            }
        }

        foreach (string excluded in ExcludedAttributeList)
        {
            if (HasAttribute(attributes, excluded))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasAttribute(ImmutableArray<AttributeData> attributes, string name)
    {
        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass is { } attributeClass && MatchesAttributeName(attributeClass, name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Matches an attribute class against a configured name, which may be written with or without the
    ///  namespace and with or without the <c>Attribute</c> suffix.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Compared segment by segment rather than against <c>ToDisplayString</c>, which would allocate a string
    ///   for every attribute on every candidate symbol.
    ///  </para>
    /// </remarks>
    private static bool MatchesAttributeName(INamedTypeSymbol attributeClass, string name)
    {
        int lastSeparator = name.LastIndexOf('.');

        // The simple name is the cheapest discriminator, so test it before walking namespaces.
        if (!SimpleNameMatches(attributeClass.Name, name, lastSeparator + 1, name.Length - lastSeparator - 1))
        {
            return false;
        }

        return lastSeparator < 0 || NamespaceMatches(attributeClass.ContainingNamespace, name, lastSeparator);
    }

    private static bool SimpleNameMatches(string candidate, string name, int start, int length)
    {
        if (length == 0)
        {
            return false;
        }

        // The configured name may omit the Attribute suffix that the class itself carries.
        if (candidate.Length != length
            && (candidate.Length != length + AttributeSuffix.Length
                || !candidate.EndsWith(AttributeSuffix, StringComparison.Ordinal)))
        {
            return false;
        }

        return string.CompareOrdinal(candidate, 0, name, start, length) == 0;
    }

    /// <summary>
    ///  Compares the dot-separated namespace written in <paramref name="name"/> before
    ///  <paramref name="end"/> against <paramref name="namespaceSymbol"/>, innermost segment first.
    /// </summary>
    private static bool NamespaceMatches(INamespaceSymbol? namespaceSymbol, string name, int end)
    {
        while (end > 0)
        {
            if (namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace)
            {
                return false;
            }

            int start = name.LastIndexOf('.', end - 1) + 1;
            int length = end - start;

            if (namespaceSymbol.Name.Length != length
                || string.CompareOrdinal(namespaceSymbol.Name, 0, name, start, length) != 0)
            {
                return false;
            }

            end = start - 1;
            namespaceSymbol = namespaceSymbol.ContainingNamespace;
        }

        return namespaceSymbol is null || namespaceSymbol.IsGlobalNamespace;
    }

    /// <summary>
    ///  A modifier a symbol can be required to have or required not to have.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Upstream supports only <c>abstract</c>, <c>async</c>, <c>const</c>, <c>readonly</c> and
    ///   <c>static</c>. See dotnet/roslyn#13250, closed as not planned.
    ///  </para>
    /// </remarks>
    internal enum ModifierKind
    {
        /// <summary>The <c>abstract</c> modifier.</summary>
        Abstract,

        /// <summary>The <c>async</c> modifier.</summary>
        Async,

        /// <summary>The <c>const</c> modifier.</summary>
        Const,

        /// <summary>The <c>readonly</c> modifier.</summary>
        ReadOnly,

        /// <summary>The <c>static</c> modifier.</summary>
        Static,

        /// <summary>The <c>sealed</c> modifier.</summary>
        Sealed,

        /// <summary>The <c>virtual</c> modifier.</summary>
        Virtual,

        /// <summary>The <c>override</c> modifier.</summary>
        Override,

        /// <summary>The <c>extern</c> modifier.</summary>
        Extern,

        /// <summary>The <c>volatile</c> modifier.</summary>
        Volatile,

        /// <summary>The <c>required</c> modifier.</summary>
        Required
    }

    /// <summary>
    ///  A symbol kind, or a more specific type or method kind, that a specification can apply to.
    /// </summary>
    internal readonly struct SymbolKindOrTypeKind
    {
        private readonly SymbolKind? _symbolKind;
        private readonly TypeKind? _typeKind;
        private readonly MethodKind? _methodKind;

        /// <summary>
        ///  Initializes a new instance that matches a <see cref="Microsoft.CodeAnalysis.SymbolKind"/>.
        /// </summary>
        public SymbolKindOrTypeKind(SymbolKind symbolKind)
        {
            _symbolKind = symbolKind;
            _typeKind = null;
            _methodKind = null;
        }

        /// <summary>
        ///  Initializes a new instance that matches a <see cref="Microsoft.CodeAnalysis.TypeKind"/>.
        /// </summary>
        public SymbolKindOrTypeKind(TypeKind typeKind)
        {
            _symbolKind = null;
            _typeKind = typeKind;
            _methodKind = null;
        }

        /// <summary>
        ///  Initializes a new instance that matches a <see cref="Microsoft.CodeAnalysis.MethodKind"/>.
        /// </summary>
        public SymbolKindOrTypeKind(MethodKind methodKind)
        {
            _symbolKind = null;
            _typeKind = null;
            _methodKind = methodKind;
        }

        /// <summary>
        ///  Returns <see langword="true"/> when <paramref name="symbol"/> is of this kind.
        /// </summary>
        public bool MatchesSymbol(ISymbol symbol)
        {
            if (_symbolKind.HasValue)
            {
                // A local function is an IMethodSymbol, but `method` in an .editorconfig means an ordinary
                // method. Local functions have their own kind.
                if (_symbolKind.Value == SymbolKind.Method && symbol is IMethodSymbol method)
                {
                    return method.MethodKind is MethodKind.Ordinary or MethodKind.ReducedExtension;
                }

                return symbol.Kind == _symbolKind.Value;
            }

            if (_typeKind.HasValue)
            {
                return symbol is INamedTypeSymbol namedType && namedType.TypeKind == _typeKind.Value;
            }

            return symbol is IMethodSymbol methodSymbol && methodSymbol.MethodKind == _methodKind!.Value;
        }
    }
}
