// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Adapts a directory and file-name predicate to the file-system matcher contract.
/// </summary>
/// <param name="predicate">The file predicate.</param>
internal sealed class PredicateFileSystemMatcher(FileSystemMatchPredicate predicate) : IFileSystemMatcher
{
    /// <summary>
    ///  Creates a matcher session for the predicate.
    /// </summary>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <returns>A new matcher session owned by the caller.</returns>
    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        return new PredicateFileSystemMatcherSession(predicate);
    }
}