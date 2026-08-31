// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

internal static partial class StatementBreakFormatting
{
    /// <summary>
    ///  Identifies the indentation category for a governed operator.
    /// </summary>
    private enum OperatorPrecedence
    {
        None,
        Primary,
        Range,
        Multiplicative,
        Additive,
        Shift,
        Relational,
        Equality,
        LogicalAnd,
        LogicalXor,
        LogicalOr,
        ConditionalAnd,
        ConditionalOr,
        NullCoalescing,
        Conditional,
        Assignment,
        PatternRelational,
        PatternAnd,
        PatternOr
    }
}