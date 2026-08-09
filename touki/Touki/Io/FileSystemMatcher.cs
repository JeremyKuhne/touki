// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Creates reusable file-system matcher definitions.
/// </summary>
public static partial class FileSystemMatcher
{
    /// <summary>
    ///  Creates a callback-native matcher that forwards directory and file-name spans without joining them.
    /// </summary>
    public static IFileSystemMatcher Create(FileSystemMatchPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new PredicateFileSystemMatcher(predicate);
    }

    /// <summary>
    ///  Creates a matcher that joins each file into a canonical root-relative path before invoking
    ///  <paramref name="predicate"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Canonical paths use <c>/</c> separators and have no leading separator. Short paths use stack
    ///   storage; longer paths use a temporary pooled buffer.
    ///  </para>
    /// </remarks>
    public static IFileSystemMatcher CreatePath(PathMatchPredicate predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new PathPredicateFileSystemMatcher(predicate);
    }
}