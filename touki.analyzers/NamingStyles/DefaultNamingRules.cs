// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  The naming rules that are always in effect, whether or not the project configures any of its own.
/// </summary>
/// <remarks>
///  <para>
///   These mirror the naming conventions the C# compiler applies when no <c>dotnet_naming_rule</c> is
///   configured: types and non-field members are <c>PascalCase</c>, interfaces start with <c>I</c>, and type
///   parameters start with <c>T</c>.
///  </para>
///  <para>
///   Defining a single custom <c>dotnet_naming_rule</c> makes IDE1006 drop all of these, so a project that
///   only wanted to add a field convention silently stops checking types, methods, properties, events and
///   interfaces. See dotnet/roslyn#71414. Here the defaults are always appended after the configured rules
///   instead of being replaced by them. A project that wants different behavior overrides a default by
///   configuring an equally or more specific rule of its own, including one with
///   <c>severity = none</c>.
///  </para>
/// </remarks>
internal static class DefaultNamingRules
{
    // Declared before All because static initializers run in declaration order and All's initializer reads it.
    // A NamingStyle read too early is the default struct, whose Prefix and Suffix are null.
    private static NamingStyle PascalCase { get; } =
        new("built_in_pascal_case", capitalizationScheme: Capitalization.PascalCase);

    /// <summary>
    ///  The always-present rules, in no particular order. Precedence is assigned when they are merged with the
    ///  configured rules.
    /// </summary>
    internal static ImmutableArray<NamingRule> All { get; } =
    [
        // Interfaces come before the general type rule because they are more specific.
        Create(
            "built_in_interfaces_start_with_i",
            [new SymbolSpecification.SymbolKindOrTypeKind(TypeKind.Interface)],
            new NamingStyle("built_in_i_pascal_case", prefix: "I", capitalizationScheme: Capitalization.PascalCase)),

        Create(
            "built_in_type_parameters_start_with_t",
            [new SymbolSpecification.SymbolKindOrTypeKind(SymbolKind.TypeParameter)],
            new NamingStyle("built_in_t_pascal_case", prefix: "T", capitalizationScheme: Capitalization.PascalCase)),

        Create(
            "built_in_types_are_pascal_case",
            [
                new SymbolSpecification.SymbolKindOrTypeKind(TypeKind.Class),
                new SymbolSpecification.SymbolKindOrTypeKind(TypeKind.Struct),
                new SymbolSpecification.SymbolKindOrTypeKind(TypeKind.Enum),
                new SymbolSpecification.SymbolKindOrTypeKind(TypeKind.Delegate)
            ],
            PascalCase),

        Create(
            "built_in_non_field_members_are_pascal_case",
            [
                new SymbolSpecification.SymbolKindOrTypeKind(SymbolKind.Property),
                new SymbolSpecification.SymbolKindOrTypeKind(SymbolKind.Method),
                new SymbolSpecification.SymbolKindOrTypeKind(SymbolKind.Event)
            ],
            PascalCase)
    ];

    private static NamingRule Create(
        string name,
        ImmutableArray<SymbolSpecification.SymbolKindOrTypeKind> kinds,
        NamingStyle style) =>
        new(
            name,
            [new SymbolSpecification(name, kinds, [], [], [], [], [])],
            style,

            // ReportDiagnostic.Default leaves the severity to the descriptor and to
            // dotnet_diagnostic.TOUKI0041.severity, rather than pinning it here.
            ReportDiagnostic.Default,
            isBuiltIn: true);
}
