// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

public sealed partial class SatelliteStringResourceManager
{
    /// <summary>
    ///  An immutable snapshot of the localized table chain for one culture and cache generation.
    /// </summary>
    private sealed class CultureCache
    {
        internal CultureCache(
            string cultureName,
            int generation,
            IndexedStringResourceTable[] tables)
        {
            CultureName = cultureName;
            Generation = generation;
            Tables = tables;
        }

        internal string CultureName { get; }

        internal int Generation { get; }

        internal IndexedStringResourceTable[] Tables { get; }
    }
}
