// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Provides normalized Boolean operations over directory match classifications used to combine matcher results.
/// </summary>
internal static class DirectoryMatchTypeOperations
{
    /// <summary>
    ///  Normalizes a directory match classification for Boolean composition.
    /// </summary>
    /// <param name="matchType">The directory match classification.</param>
    /// <returns>The normalized classification.</returns>
    public static DirectoryMatchType Normalize(DirectoryMatchType matchType) => matchType switch
    {
        DirectoryMatchType.NoDescendantFilesMatch => matchType,
        DirectoryMatchType.AllDescendantFilesMatch => matchType,
        _ => DirectoryMatchType.MayContainMatchingFiles
    };

    /// <summary>
    ///  Computes the logical complement of a directory match classification.
    /// </summary>
    /// <param name="matchType">The directory match classification.</param>
    /// <returns>The complemented classification.</returns>
    public static DirectoryMatchType Not(DirectoryMatchType matchType) => Normalize(matchType) switch
    {
        DirectoryMatchType.NoDescendantFilesMatch => DirectoryMatchType.AllDescendantFilesMatch,
        DirectoryMatchType.AllDescendantFilesMatch => DirectoryMatchType.NoDescendantFilesMatch,
        _ => DirectoryMatchType.MayContainMatchingFiles
    };

    /// <summary>
    ///  Computes the logical union of two directory match classifications.
    /// </summary>
    /// <param name="left">The first classification.</param>
    /// <param name="right">The second classification.</param>
    /// <returns>The combined classification.</returns>
    public static DirectoryMatchType Or(
        DirectoryMatchType left,
        DirectoryMatchType right)
    {
        left = Normalize(left);
        right = Normalize(right);
        if (left == DirectoryMatchType.AllDescendantFilesMatch
            || right == DirectoryMatchType.AllDescendantFilesMatch)
        {
            return DirectoryMatchType.AllDescendantFilesMatch;
        }

        return left == DirectoryMatchType.NoDescendantFilesMatch
            && right == DirectoryMatchType.NoDescendantFilesMatch
                ? DirectoryMatchType.NoDescendantFilesMatch
                : DirectoryMatchType.MayContainMatchingFiles;
    }

    /// <summary>
    ///  Computes the logical intersection of two directory match classifications.
    /// </summary>
    /// <param name="left">The first classification.</param>
    /// <param name="right">The second classification.</param>
    /// <returns>The combined classification.</returns>
    public static DirectoryMatchType And(
        DirectoryMatchType left,
        DirectoryMatchType right)
    {
        left = Normalize(left);
        right = Normalize(right);
        if (left == DirectoryMatchType.NoDescendantFilesMatch
            || right == DirectoryMatchType.NoDescendantFilesMatch)
        {
            return DirectoryMatchType.NoDescendantFilesMatch;
        }

        return left == DirectoryMatchType.AllDescendantFilesMatch
            && right == DirectoryMatchType.AllDescendantFilesMatch
                ? DirectoryMatchType.AllDescendantFilesMatch
                : DirectoryMatchType.MayContainMatchingFiles;
    }
}