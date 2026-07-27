// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Touki.Analyzers.NamingStyles;

/// <summary>
///  A naming rule: the symbols it applies to, the style they must follow, and how hard to complain.
/// </summary>
/// <remarks>
///  <para>
///   Ported from <c>Microsoft.CodeAnalysis.Diagnostics.Analyzers.NamingStyles.NamingRule</c> in dotnet/roslyn.
///   Upstream binds one rule to exactly one symbol group and chains rules through a parent link; here a rule
///   carries the full list of groups it was configured with, so a single rule can name several groups. See
///   dotnet/roslyn#20891.
///  </para>
/// </remarks>
internal readonly struct NamingRule(
    string name,
    ImmutableArray<SymbolSpecification> symbolSpecifications,
    NamingStyle namingStyle,
    ReportDiagnostic severity,
    bool isBuiltIn)
{
    /// <summary>
    ///  The name of the rule as written in the <c>.editorconfig</c>.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    ///  The symbol groups the rule applies to. The rule applies when any one of them matches.
    /// </summary>
    public ImmutableArray<SymbolSpecification> SymbolSpecifications { get; } = symbolSpecifications;

    /// <summary>
    ///  The style matching symbols must follow.
    /// </summary>
    public NamingStyle NamingStyle { get; } = namingStyle;

    /// <summary>
    ///  How a violation of this rule is reported.
    /// </summary>
    public ReportDiagnostic Severity { get; } = severity;

    /// <summary>
    ///  Whether the rule is one of the built-in defaults rather than one the user configured.
    /// </summary>
    public bool IsBuiltIn { get; } = isBuiltIn;

    /// <summary>
    ///  Returns <see langword="true"/> when this rule governs <paramref name="symbol"/>.
    /// </summary>
    public bool AppliesTo(ISymbol symbol)
    {
        foreach (SymbolSpecification specification in SymbolSpecifications)
        {
            if (specification.AppliesTo(symbol))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  How narrowly the rule is scoped. Rules with a higher score are consulted first so that a rule about
    ///  <c>[ThreadStatic]</c> fields wins over one about static fields, which in turn wins over one about all
    ///  fields.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Upstream instead sorts rules with a set of subset comparers. That ordering is partial - two rules can
    ///   be mutually incomparable - which makes the final order depend on the order the rules happened to be
    ///   read in. A single total score keeps the outcome the same no matter how the file is written.
    ///  </para>
    ///  <para>
    ///   A rule naming several symbol groups is only as narrow as its broadest group, because it applies to
    ///   the union of them.
    ///  </para>
    /// </remarks>
    public int Specificity
    {
        get
        {
            int specificity = int.MaxValue;

            foreach (SymbolSpecification specification in SymbolSpecifications)
            {
                int score = ((specification.RequiredAttributeList.Length
                    + specification.ExcludedAttributeList.Length) * 1000)
                    + ((specification.RequiredModifierList.Length
                        + specification.ExcludedModifierList.Length) * 100)
                    + (specification.ApplicableAccessibilityList.IsEmpty ? 0 : 10)
                    + (specification.ApplicableSymbolKindList.IsEmpty ? 0 : 1);

                if (score < specificity)
                {
                    specificity = score;
                }
            }

            return specificity == int.MaxValue ? 0 : specificity;
        }
    }
}
