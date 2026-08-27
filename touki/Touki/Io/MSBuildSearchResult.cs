// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  A valid lazy MSBuild-compatible file-system search.
/// </summary>
public sealed class MSBuildSearchResult : MSBuildEnumerationResult
{
    /// <summary>
    ///  Initializes a valid lazy search result.
    /// </summary>
    /// <param name="enumerator">The lazy enumerator owned by the caller.</param>
    /// <param name="invalidExcludeSpecifications">The invalid excludes retained as literal filters.</param>
    internal MSBuildSearchResult(MSBuildEnumerator enumerator, string[] invalidExcludeSpecifications)
    {
        ArgumentNullException.ThrowIfNull(enumerator);
        ArgumentNullException.ThrowIfNull(invalidExcludeSpecifications);
        for (int index = 0; index < invalidExcludeSpecifications.Length; index++)
        {
            ArgumentNullException.ThrowIfNull(invalidExcludeSpecifications[index]);
        }

        Enumerator = enumerator;
        InvalidExcludeSpecifications = invalidExcludeSpecifications.Length == 0
            ? Array.Empty<string>()
            : Array.AsReadOnly(invalidExcludeSpecifications);
    }

    /// <summary>
    ///  Gets the lazy enumerator owned by the caller, who must dispose it.
    /// </summary>
    public MSBuildEnumerator Enumerator { get; }

    /// <summary>
    ///  Gets invalid exclude specifications retained as literal result filters, in source order.
    /// </summary>
    public IReadOnlyList<string> InvalidExcludeSpecifications { get; }
}
