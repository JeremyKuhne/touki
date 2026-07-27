// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  Reads <c>touki_naming_rule</c>, <c>touki_naming_symbols</c> and <c>touki_naming_style</c> entries out of an
///  <c>.editorconfig</c>.
/// </summary>
/// <remarks>
///  <para>
///   Modelled on <c>EditorConfigNamingStyleParser</c> in dotnet/roslyn. The key prefixes are
///   <c>touki_naming_*</c> rather than <c>dotnet_naming_*</c> so that a project can configure this analyzer
///   without also feeding IDE1006, which reads the <c>dotnet_naming_*</c> keys.
///  </para>
/// </remarks>
internal static class EditorConfigNamingStyleParser
{
    private const string RulePrefix = "touki_naming_rule.";
    private const string SymbolsPrefix = "touki_naming_symbols.";
    private const string StylePrefix = "touki_naming_style.";

    /// <summary>
    ///  Parses every complete naming rule out of <paramref name="options"/>, most specific rule first.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Incomplete or unparseable rules are skipped rather than reported. An analyzer cannot report a
    ///   diagnostic against an <c>.editorconfig</c>, and failing the whole compilation over a typo in a naming
    ///   preference would be worse than ignoring it.
    ///  </para>
    /// </remarks>
    public static ImmutableArray<NamingRule> ParseRules(AnalyzerConfigOptions options)
    {
        if (!TryCollectNames(
            options,
            out List<string> ruleNames,
            out HashSet<string> definedSymbolGroups,
            out HashSet<string> definedStyles))
        {
            return [];
        }

        List<NamingRule> rules = [];

        foreach (string ruleName in ruleNames)
        {
            if (TryGetRule(options, ruleName, definedSymbolGroups, definedStyles, out NamingRule rule))
            {
                rules.Add(rule);
            }
        }

        return SortByPrecedence(rules);
    }

    /// <summary>
    ///  Orders rules so that the most narrowly scoped is consulted first, and so that a user rule beats a
    ///  built-in rule of the same scope.
    /// </summary>
    public static ImmutableArray<NamingRule> SortByPrecedence(List<NamingRule> rules)
    {
        // A stable sort keyed on a total ordering: specificity, then user rules before built-in rules, then
        // rule name. Rules are few and this runs once per .editorconfig, so an insertion sort is plenty.
        NamingRule[] sorted = [.. rules];

        for (int i = 1; i < sorted.Length; i++)
        {
            NamingRule current = sorted[i];
            int j = i - 1;

            while (j >= 0 && ComparePrecedence(sorted[j], current) > 0)
            {
                sorted[j + 1] = sorted[j];
                j--;
            }

            sorted[j + 1] = current;
        }

        return [.. sorted];
    }

    private static int ComparePrecedence(NamingRule left, NamingRule right)
    {
        int comparison = right.Specificity.CompareTo(left.Specificity);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.IsBuiltIn.CompareTo(right.IsBuiltIn);
        return comparison != 0 ? comparison : string.CompareOrdinal(left.Name, right.Name);
    }

    /// <summary>
    ///  Walks the configuration keys once, collecting the names of every rule, symbol group and style that has
    ///  at least one key. Returns <see langword="false"/> when there is nothing to parse.
    /// </summary>
    private static bool TryCollectNames(
        AnalyzerConfigOptions options,
        out List<string> ruleNames,
        out HashSet<string> symbolGroups,
        out HashSet<string> styles)
    {
        ruleNames = [];
        symbolGroups = [];
        styles = [];

        // AnalyzerConfigOptions.Keys is virtual and its base implementation throws, so a host that supplies
        // its own options type without overriding it would otherwise take the whole analyzer down with an
        // AD0001 on every compilation. Degrade to "nothing configured" instead; the built-in rules still apply.
        IEnumerable<string> keys;
        try
        {
            keys = options.Keys;
        }
        catch (NotImplementedException)
        {
            return false;
        }

        HashSet<string> seenRules = [];

        foreach (string key in keys)
        {
            if (TryGetEntryName(key, RulePrefix, out string name))
            {
                if (seenRules.Add(name))
                {
                    ruleNames.Add(name);
                }
            }
            else if (TryGetEntryName(key, SymbolsPrefix, out name))
            {
                symbolGroups.Add(name);
            }
            else if (TryGetEntryName(key, StylePrefix, out name))
            {
                styles.Add(name);
            }
        }

        return ruleNames.Count > 0;
    }

    /// <summary>
    ///  Extracts the entry name from <c>&lt;prefix&gt;&lt;name&gt;.&lt;property&gt;</c>.
    /// </summary>
    private static bool TryGetEntryName(string key, string prefix, out string name)
    {
        name = string.Empty;

        if (!key.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        int lastSeparator = key.LastIndexOf('.');
        if (lastSeparator <= prefix.Length)
        {
            return false;
        }

        name = key.Substring(prefix.Length, lastSeparator - prefix.Length);
        return true;
    }

    private static bool TryGetRule(
        AnalyzerConfigOptions options,
        string ruleName,
        HashSet<string> definedSymbolGroups,
        HashSet<string> definedStyles,
        out NamingRule rule)
    {
        rule = default;

        if (!options.TryGetValue($"{RulePrefix}{ruleName}.symbols", out string? symbolNames)
            || !options.TryGetValue($"{RulePrefix}{ruleName}.style", out string? styleName)
            || !options.TryGetValue($"{RulePrefix}{ruleName}.severity", out string? severityName)
            || !TryParseSeverity(severityName, out ReportDiagnostic severity)
            || !definedStyles.Contains(styleName.Trim())
            || !TryGetNamingStyle(options, styleName.Trim(), out NamingStyle style))
        {
            return false;
        }

        // A rule may name more than one symbol group. See dotnet/roslyn#20891.
        ImmutableArray<SymbolSpecification>.Builder specifications =
            ImmutableArray.CreateBuilder<SymbolSpecification>();

        foreach (string symbolName in SplitList(symbolNames))
        {
            // A group with no keys at all would parse into a specification whose every list is empty, which
            // matches every symbol in the compilation. A misspelled group name must drop the rule, not turn it
            // into a catch-all.
            if (!definedSymbolGroups.Contains(symbolName)
                || !TryGetSymbolSpecification(options, symbolName, out SymbolSpecification specification))
            {
                return false;
            }

            specifications.Add(specification);
        }

        if (specifications.Count == 0)
        {
            return false;
        }

        rule = new NamingRule(ruleName, specifications.ToImmutable(), style, severity, isBuiltIn: false);
        return true;
    }

    private static bool TryGetSymbolSpecification(
        AnalyzerConfigOptions options,
        string symbolName,
        out SymbolSpecification specification)
    {
        specification = null!;
        string prefix = $"{SymbolsPrefix}{symbolName}.";

        if (!TryParseSymbolKinds(
                GetValue(options, prefix + "applicable_kinds"),
                out ImmutableArray<SymbolSpecification.SymbolKindOrTypeKind> kinds)
            || !TryParseAccessibilities(
                GetValue(options, prefix + "applicable_accessibilities"),
                out ImmutableArray<Accessibility> accessibilities)
            || !TryParseModifiers(
                GetValue(options, prefix + "required_modifiers"),
                out ImmutableArray<SymbolSpecification.ModifierKind> required)
            || !TryParseModifiers(
                GetValue(options, prefix + "excluded_modifiers"),
                out ImmutableArray<SymbolSpecification.ModifierKind> excluded))
        {
            return false;
        }

        specification = new SymbolSpecification(
            symbolName,
            kinds,
            accessibilities,
            required,
            excluded,
            ParseNames(GetValue(options, prefix + "required_attributes")),
            ParseNames(GetValue(options, prefix + "excluded_attributes")));

        return true;
    }

    private static bool TryGetNamingStyle(AnalyzerConfigOptions options, string styleName, out NamingStyle style)
    {
        style = default;
        string prefix = $"{StylePrefix}{styleName}.";

        if (!TryParseCapitalization(
            GetValue(options, prefix + "capitalization"),
            out Capitalization capitalization))
        {
            return false;
        }

        style = new NamingStyle(
            styleName,
            GetValue(options, prefix + "required_prefix")?.Trim(),
            GetValue(options, prefix + "required_suffix")?.Trim(),
            GetValue(options, prefix + "word_separator")?.Trim(),
            capitalization);

        return true;
    }

    private static string? GetValue(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out string? value) ? value : null;

    private static ImmutableArray<string> ParseNames(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        ImmutableArray<string>.Builder names = ImmutableArray.CreateBuilder<string>();

        foreach (string name in SplitList(value!))
        {
            names.Add(name);
        }

        return names.ToImmutable();
    }

    private static List<string> SplitList(string value)
    {
        List<string> items = [];

        foreach (string item in value.Split(','))
        {
            string trimmed = item.Trim();
            if (trimmed.Length > 0)
            {
                items.Add(trimmed);
            }
        }

        return items;
    }

    private static bool TryParseSeverity(string value, out ReportDiagnostic severity)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "none":
                severity = ReportDiagnostic.Suppress;
                return true;
            case "silent":
            case "refactoring":
                severity = ReportDiagnostic.Hidden;
                return true;
            case "suggestion":
                severity = ReportDiagnostic.Info;
                return true;
            case "warning":
                severity = ReportDiagnostic.Warn;
                return true;
            case "error":
                severity = ReportDiagnostic.Error;
                return true;
            default:
                severity = ReportDiagnostic.Default;
                return false;
        }
    }

    private static bool TryParseCapitalization(string? value, out Capitalization capitalization)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "pascal_case":
                capitalization = Capitalization.PascalCase;
                return true;
            case "camel_case":
                capitalization = Capitalization.CamelCase;
                return true;
            case "first_word_upper":
                capitalization = Capitalization.FirstUpper;
                return true;
            case "all_upper":
                capitalization = Capitalization.AllUpper;
                return true;
            case "all_lower":
                capitalization = Capitalization.AllLower;
                return true;
            default:
                capitalization = default;
                return false;
        }
    }

    private static bool TryParseSymbolKinds(string? value, out ImmutableArray<SymbolSpecification.SymbolKindOrTypeKind> kinds)
    {
        kinds = [];

        if (value is null)
        {
            return true;
        }

        ImmutableArray<SymbolSpecification.SymbolKindOrTypeKind>.Builder builder =
            ImmutableArray.CreateBuilder<SymbolSpecification.SymbolKindOrTypeKind>();

        foreach (string item in SplitList(value))
        {
            switch (item.ToLowerInvariant())
            {
                case "*":
                    // Matches everything, which an empty list already does.
                    kinds = [];
                    return true;
                case "namespace":
                    builder.Add(new(SymbolKind.Namespace));
                    break;
                case "class":
                    builder.Add(new(TypeKind.Class));
                    break;
                case "struct":
                    builder.Add(new(TypeKind.Struct));
                    break;
                case "interface":
                    builder.Add(new(TypeKind.Interface));
                    break;
                case "enum":
                    builder.Add(new(TypeKind.Enum));
                    break;
                case "delegate":
                    builder.Add(new(TypeKind.Delegate));
                    break;
                case "property":
                    builder.Add(new(SymbolKind.Property));
                    break;
                case "method":
                    builder.Add(new(SymbolKind.Method));
                    break;
                case "local_function":
                    builder.Add(new(MethodKind.LocalFunction));
                    break;
                case "field":
                    builder.Add(new(SymbolKind.Field));
                    break;
                case "event":
                    builder.Add(new(SymbolKind.Event));
                    break;
                case "parameter":
                    builder.Add(new(SymbolKind.Parameter));
                    break;
                case "type_parameter":
                    builder.Add(new(SymbolKind.TypeParameter));
                    break;
                case "local":
                    builder.Add(new(SymbolKind.Local));
                    break;
                default:
                    return false;
            }
        }

        kinds = builder.ToImmutable();
        return true;
    }

    private static bool TryParseAccessibilities(string? value, out ImmutableArray<Accessibility> accessibilities)
    {
        accessibilities = [];

        if (value is null)
        {
            return true;
        }

        ImmutableArray<Accessibility>.Builder builder = ImmutableArray.CreateBuilder<Accessibility>();

        foreach (string item in SplitList(value))
        {
            switch (item.ToLowerInvariant())
            {
                case "*":
                    accessibilities = [];
                    return true;
                case "public":
                    builder.Add(Accessibility.Public);
                    break;
                case "internal":
                case "friend":
                    builder.Add(Accessibility.Internal);
                    break;
                case "private":
                    builder.Add(Accessibility.Private);
                    break;
                case "protected":
                    builder.Add(Accessibility.Protected);
                    break;
                case "protected_internal":
                case "protected_friend":
                    builder.Add(Accessibility.ProtectedOrInternal);
                    break;
                case "private_protected":
                    builder.Add(Accessibility.ProtectedAndInternal);
                    break;
                case "local":
                    builder.Add(Accessibility.NotApplicable);
                    break;
                default:
                    return false;
            }
        }

        accessibilities = builder.ToImmutable();
        return true;
    }

    private static bool TryParseModifiers(
        string? value,
        out ImmutableArray<SymbolSpecification.ModifierKind> modifiers)
    {
        modifiers = [];

        if (value is null)
        {
            return true;
        }

        ImmutableArray<SymbolSpecification.ModifierKind>.Builder builder =
            ImmutableArray.CreateBuilder<SymbolSpecification.ModifierKind>();

        foreach (string item in SplitList(value))
        {
            switch (item.ToLowerInvariant())
            {
                case "abstract":
                    builder.Add(SymbolSpecification.ModifierKind.Abstract);
                    break;
                case "async":
                    builder.Add(SymbolSpecification.ModifierKind.Async);
                    break;
                case "const":
                    builder.Add(SymbolSpecification.ModifierKind.Const);
                    break;
                case "readonly":
                    builder.Add(SymbolSpecification.ModifierKind.ReadOnly);
                    break;
                case "static":
                    builder.Add(SymbolSpecification.ModifierKind.Static);
                    break;
                case "sealed":
                    builder.Add(SymbolSpecification.ModifierKind.Sealed);
                    break;
                case "virtual":
                    builder.Add(SymbolSpecification.ModifierKind.Virtual);
                    break;
                case "override":
                    builder.Add(SymbolSpecification.ModifierKind.Override);
                    break;
                case "extern":
                    builder.Add(SymbolSpecification.ModifierKind.Extern);
                    break;
                case "volatile":
                    builder.Add(SymbolSpecification.ModifierKind.Volatile);
                    break;
                case "required":
                    builder.Add(SymbolSpecification.ModifierKind.Required);
                    break;
                default:
                    return false;
            }
        }

        modifiers = builder.ToImmutable();
        return true;
    }
}
