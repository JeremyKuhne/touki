// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Adapts a root-relative path predicate into a matcher that creates canonical-path sessions.
/// </summary>
internal sealed class PathPredicateFileSystemMatcher(PathMatchPredicate predicate) : IFileSystemMatcher
{
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return new PathPredicateFileSystemMatcherSession(rootDirectory, predicate);
    }
}