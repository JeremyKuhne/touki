// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections.Immutable;

namespace Touki.Resources.Generator;

/// <summary>
///  Represents the string entries parsed from a resource file.
/// </summary>
internal sealed class ParsedResource
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="ParsedResource"/> class.
    /// </summary>
    /// <param name="entries">The parsed resource entries.</param>
    internal ParsedResource(ImmutableArray<ResourceEntry> entries) => Entries = entries;

    /// <summary>
    ///  Gets the parsed resource entries.
    /// </summary>
    internal ImmutableArray<ResourceEntry> Entries { get; }
}