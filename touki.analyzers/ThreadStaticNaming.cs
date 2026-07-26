// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

/// <summary>
///  Shared thread-static naming decisions, so that <see cref="ThreadStaticNamingAnalyzer"/> and
///  <see cref="ThreadStaticNamingSuppressor"/> cannot drift apart. Whatever the analyzer accepts is
///  exactly what the suppressor stays silent about.
/// </summary>
internal static class ThreadStaticNaming
{
    /// <summary>
    ///  Metadata name of the attribute that gives a static field one slot per thread.
    /// </summary>
    internal const string ThreadStaticAttributeMetadataName = "System.ThreadStaticAttribute";

    private const string AttributeSuffix = "Attribute";

    /// <summary>
    ///  Returns the configured thread-static prefix, or <see cref="ThreadStaticNamingAnalyzer.DefaultPrefix"/>
    ///  when none is set.
    /// </summary>
    internal static string GetPrefix(AnalyzerConfigOptions options)
    {
        if (options.TryGetValue(ThreadStaticNamingAnalyzer.PrefixOption, out string? value))
        {
            string prefix = value.Trim();

            // An empty value is far more likely to be a mistake than a deliberate "require no prefix".
            if (prefix.Length > 0)
            {
                return prefix;
            }
        }

        return ThreadStaticNamingAnalyzer.DefaultPrefix;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="field"/> is shaped like a thread-static field:
    ///  a non-constant static that carries at least one attribute.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is the filter that keeps the rule cheap. It is what every field in a compilation is measured
    ///   against, and it runs before any <c>.editorconfig</c> lookup.
    ///  </para>
    /// </remarks>
    internal static bool CouldBeThreadStatic(IFieldSymbol field) =>
        field.IsStatic && !field.IsConst && !field.GetAttributes().IsEmpty;

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="field"/> gets one slot per thread.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Only a <see langword="static"/> field has a per-thread slot to name. The attribute has no effect on
    ///   an instance field, which CA2259 already reports, and such a field is still named as an ordinary
    ///   instance field.
    ///  </para>
    /// </remarks>
    internal static bool IsThreadStatic(
        IFieldSymbol field,
        INamedTypeSymbol? threadStaticAttribute,
        AnalyzerConfigOptions options)
    {
        if (!CouldBeThreadStatic(field))
        {
            return false;
        }

        ImmutableArray<AttributeData> attributes = field.GetAttributes();

        foreach (AttributeData attribute in attributes)
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, threadStaticAttribute))
            {
                return true;
            }
        }

        string additional = GetAdditionalAttributes(options);

        if (additional.Length == 0)
        {
            return false;
        }

        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass is { } attributeClass && MatchesByName(attributeClass.Name, additional))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="name"/> carries <paramref name="prefix"/> and is
    ///  camel cased after it.
    /// </summary>
    internal static bool IsConforming(string name, string prefix) =>
        name.Length > prefix.Length
        && name.StartsWith(prefix, StringComparison.Ordinal)
        && char.IsLower(name[prefix.Length]);

    /// <summary>
    ///  Returns what <paramref name="name"/> should be called: <paramref name="prefix"/> followed by the
    ///  name with any instance, static, or thread-static prefix it already carries replaced.
    /// </summary>
    internal static string SuggestedName(string name, string prefix)
    {
        string core = StripLeadingPrefix(name, prefix);

        // A name that is nothing but a prefix leaves nothing to build on.
        return core.Length == 0
            ? prefix + name
            : prefix + char.ToLowerInvariant(core[0]) + core.Substring(1);
    }

    /// <summary>
    ///  Removes a known field prefix from <paramref name="name"/>: the configured thread-static prefix, the
    ///  default thread-static prefix, the <c>s_</c> static prefix, or the <c>_</c> instance prefix. Anything
    ///  else is left alone rather than guessed at, so a name like <c>x_ray</c> keeps both of its parts.
    /// </summary>
    private static string StripLeadingPrefix(string name, string prefix)
    {
        if (name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return name.Substring(prefix.Length);
        }

        // The default prefix is still a thread-static marker when a different one is configured, so a
        // 't_value' renamed under 'tl_' becomes 'tl_value' rather than 'tl_t_value'.
        if (name.StartsWith(ThreadStaticNamingAnalyzer.DefaultPrefix, StringComparison.Ordinal))
        {
            return name.Substring(ThreadStaticNamingAnalyzer.DefaultPrefix.Length);
        }

        if (name.StartsWith("s_", StringComparison.Ordinal))
        {
            return name.Substring(2);
        }

        if (name.StartsWith("_", StringComparison.Ordinal))
        {
            return name.Substring(1);
        }

        return name;
    }

    /// <summary>
    ///  Returns the comma-separated attribute names configured as additionally marking a thread-static field.
    /// </summary>
    private static string GetAdditionalAttributes(AnalyzerConfigOptions options) =>
        options.TryGetValue(ThreadStaticNamingAnalyzer.AdditionalAttributesOption, out string? value)
            ? value.Trim()
            : string.Empty;

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="attributeTypeName"/> is one of the comma-separated
    ///  names in <paramref name="configured"/>, written with or without the <c>Attribute</c> suffix.
    /// </summary>
    private static bool MatchesByName(string attributeTypeName, string configured)
    {
        foreach (string candidate in configured.Split(','))
        {
            string trimmed = candidate.Trim();

            if (trimmed.Length == 0)
            {
                continue;
            }

            if (string.Equals(attributeTypeName, trimmed, StringComparison.Ordinal))
            {
                return true;
            }

            // Allow the attribute to be named the way it is written in source, without the suffix.
            if (attributeTypeName.Length == trimmed.Length + AttributeSuffix.Length
                && attributeTypeName.StartsWith(trimmed, StringComparison.Ordinal)
                && attributeTypeName.EndsWith(AttributeSuffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
