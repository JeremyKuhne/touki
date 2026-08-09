// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class PredicateFileSystemMatcher(FileSystemMatchPredicate predicate) : IFileSystemMatcher
{
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return new PredicateFileSystemMatcherSession(predicate);
    }
}