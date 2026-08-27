// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Adapts a root-relative path predicate into a matcher that creates canonical-path sessions.
/// </summary>
/// <param name="predicate">The path predicate.</param>
internal sealed class PathPredicateFileSystemMatcher(PathMatchPredicate predicate) : IFileSystemMatcher
{
    /// <summary>
    ///  Creates a matcher session bound to <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <returns>A new matcher session owned by the caller.</returns>
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return new PathPredicateFileSystemMatcherSession(rootDirectory, predicate);
    }
}