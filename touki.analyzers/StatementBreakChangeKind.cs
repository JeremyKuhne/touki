// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

/// <summary>
///  Identifies the text transformation required to format a statement break.
/// </summary>
internal enum StatementBreakChangeKind
{
    Indentation,
    LeadingOperator,
    TrailingOperator,
    BreakBeforeOperator,
    BreakAfterOperator
}