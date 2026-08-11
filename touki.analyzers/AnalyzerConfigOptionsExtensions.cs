// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Touki.Analyzers;

internal static class AnalyzerConfigOptionsExtensions
{
    /// <summary>
    ///  Attempts to read the value for <paramref name="key"/> as a positive integer.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> when the option contains a positive integer; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetPositiveInteger(this AnalyzerConfigOptions options, string key, out int value)
    {
        if (options.TryGetValue(key, out string? raw)
            && int.TryParse(raw.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value > 0)
        {
            return true;
        }

        value = 0;
        return false;
    }
}