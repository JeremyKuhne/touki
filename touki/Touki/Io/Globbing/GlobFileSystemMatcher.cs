// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Adapts a compiled glob specification to the file-system matcher session contract.
/// </summary>
/// <param name="specification">The compiled glob specification.</param>
internal sealed class GlobFileSystemMatcher(GlobSpecification specification) : IFileSystemMatcher
{
    /// <summary>
    ///  Creates a matcher session bound to <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <returns>A new matcher session owned by the caller.</returns>
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return specification.CreateSession(rootDirectory);
    }
}