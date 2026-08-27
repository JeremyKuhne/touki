// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Adapts a compiled glob specification to the file-system matcher session contract.
/// </summary>
internal sealed class GlobFileSystemMatcher(GlobSpecification specification) : IFileSystemMatcher
{
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return specification.CreateSession(rootDirectory);
    }
}