// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Provides indexed string lookup over an owned resource backing.
/// </summary>
internal interface IStringResourceReader : IDisposable
{
    /// <summary>
    ///  The number of resources in the table.
    /// </summary>
    int ResourceCount { get; }

    /// <summary>
    ///  Gets the stored type of a resource without materializing its name or value.
    /// </summary>
    /// <param name="index">The zero-based resource index.</param>
    /// <returns>The stored resource type.</returns>
    ResourceTypeCode GetResourceTypeCode(int index);

    /// <summary>
    ///  Decodes the resource name at an index.
    /// </summary>
    /// <param name="index">The zero-based resource index.</param>
    /// <returns>The resource name.</returns>
    string GetResourceName(int index);

    /// <summary>
    ///  Decodes the intrinsic string at an index.
    /// </summary>
    /// <param name="index">The zero-based resource index.</param>
    /// <returns>The string value.</returns>
    string GetString(int index);

    /// <summary>
    ///  Looks up and decodes one intrinsic string.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="value">The decoded string when found.</param>
    /// <returns>The lookup result.</returns>
    StringResourceLookupKind Lookup(string name, out string? value);
}
