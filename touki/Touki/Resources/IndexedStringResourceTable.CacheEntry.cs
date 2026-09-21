// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

internal sealed partial class IndexedStringResourceTable
{
    /// <summary>
    ///  An atomically published decoded string cache entry.
    /// </summary>
    private sealed class CacheEntry
    {
        internal CacheEntry(string name, string value)
        {
            Name = name;
            Value = value;
        }

        internal string Name { get; }

        internal string Value { get; }
    }
}