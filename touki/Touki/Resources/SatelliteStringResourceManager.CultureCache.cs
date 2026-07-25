// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Frozen;

namespace Touki.Resources;

public sealed partial class SatelliteStringResourceManager
{
    /// <summary>
    ///  An immutable snapshot of the resolution state for a single culture. It is published atomically
    ///  through the <see langword="volatile"/> <c>_cache</c> field, so a reader that reads the
    ///  reference sees a fully-initialized instance - the culture name, the neutral flag, and the table
    ///  are always mutually consistent (never torn), even on weak memory models.
    /// </summary>
    private sealed class CultureCache
    {
        internal CultureCache(string cultureName, bool isNeutral, FrozenDictionary<string, string>? strings)
        {
            CultureName = cultureName;
            IsNeutral = isNeutral;
            Strings = strings;
        }

        internal string CultureName { get; }

        internal bool IsNeutral { get; }

        internal FrozenDictionary<string, string>? Strings { get; }
    }
}
