// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  An immutable-after-publication string table.
/// </summary>
internal sealed class StringResourceTable
{
    private readonly Dictionary<string, string> _strings;

    /// <summary>
    ///  Initializes a resource table from its string values.
    /// </summary>
    /// <param name="strings">The string values.</param>
    internal StringResourceTable(Dictionary<string, string> strings)
    {
        _strings = strings;
    }

    /// <summary>
    ///  The string values in the table.
    /// </summary>
    internal Dictionary<string, string> Strings => _strings;
}