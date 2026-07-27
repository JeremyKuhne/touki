// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  The ordered set of naming rules in effect for a file, and the lookup from a symbol to the rule that
///  governs it.
/// </summary>
/// <remarks>
///  <para>
///   Ported from <c>NamingStyleRules</c> in dotnet/roslyn. The built-in rules are always present, so a project
///   that configures one rule of its own does not lose the rest. See dotnet/roslyn#71414.
///  </para>
/// </remarks>
internal sealed class NamingStyleRules
{
    /// <summary>
    ///  The rules that apply when a file configures none of its own.
    /// </summary>
    public static NamingStyleRules Default { get; } = new([.. DefaultNamingRules.All]);

    private NamingStyleRules(List<NamingRule> rules) =>
        Rules = EditorConfigNamingStyleParser.SortByPrecedence(rules);

    /// <summary>
    ///  The rules in the order they are consulted, most specific first.
    /// </summary>
    public ImmutableArray<NamingRule> Rules { get; }

    /// <summary>
    ///  Builds the rule set for <paramref name="options"/>.
    /// </summary>
    public static NamingStyleRules Create(AnalyzerConfigOptions options)
    {
        ImmutableArray<NamingRule> configured = EditorConfigNamingStyleParser.ParseRules(options);
        if (configured.IsEmpty)
        {
            return Default;
        }

        List<NamingRule> rules = [.. configured, .. DefaultNamingRules.All];
        return new NamingStyleRules(rules);
    }

    /// <summary>
    ///  Finds the rule that governs <paramref name="symbol"/>, if any.
    /// </summary>
    public bool TryGetApplicableRule(ISymbol symbol, out NamingRule rule)
    {
        foreach (NamingRule candidate in Rules)
        {
            if (candidate.AppliesTo(symbol))
            {
                rule = candidate;
                return true;
            }
        }

        rule = default;
        return false;
    }
}
