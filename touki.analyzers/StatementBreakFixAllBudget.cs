// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Analyzers;

/// <summary>
///  Bounds diagnostics and generated replacement text across one statement-break Fix All operation.
/// </summary>
internal struct StatementBreakFixAllBudget
{
    public const int MaximumDiagnostics = 65_536;
    public const long MaximumReplacementCharacters = 4 * 1024 * 1024;

    private int _diagnostics;
    private long _replacementCharacters;

    public bool TryReserveDiagnostics(int count)
    {
        if (count < 0 || count > MaximumDiagnostics - _diagnostics)
        {
            return false;
        }

        _diagnostics += count;
        return true;
    }

    public bool TryReserveReplacementCharacters(long count)
    {
        if (count < 0 || count > MaximumReplacementCharacters - _replacementCharacters)
        {
            return false;
        }

        _replacementCharacters += count;
        return true;
    }
}