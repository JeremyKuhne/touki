// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.ExceptionServices;

namespace Touki.Io.Globbing;

/// <summary>
///  Applies one include glob and a set of exclusion globs while preserving directory-pruning guarantees.
/// </summary>
internal sealed class GlobEnumeratorFileSystemMatcherSession(
    GlobMatch include,
    GlobMatch[] excludes) : FileSystemMatcherSession
{
    private readonly int _wholeSubtreeExcludeCount = CountWholeSubtreeExcludes(excludes);

    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        for (int index = 0; index < excludes.Length; index++)
        {
            if (excludes[index].MatchesFile(currentDirectory, fileName))
            {
                return false;
            }
        }

        return include.MatchesFile(currentDirectory, fileName);
    }

    public override DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        DirectoryMatchType includeResult;
        if (_wholeSubtreeExcludeCount == 0)
        {
            includeResult = DirectoryMatchTypeOperations.Normalize(
                include.MatchesDirectory(currentDirectory, directoryName));
            return includeResult == DirectoryMatchType.NoDescendantFilesMatch
                ? DirectoryMatchType.NoDescendantFilesMatch
                : DirectoryMatchType.MayContainMatchingFiles;
        }

        bool excludeMayMatch = _wholeSubtreeExcludeCount != excludes.Length;
        for (int index = 0; index < excludes.Length; index++)
        {
            GlobMatch exclude = excludes[index];
            if (!exclude.CanMatchWholeSubtree)
            {
                continue;
            }

            DirectoryMatchType excludeResult = DirectoryMatchTypeOperations.Normalize(
                exclude.MatchesDirectory(currentDirectory, directoryName));
            if (excludeResult == DirectoryMatchType.AllDescendantFilesMatch)
            {
                return DirectoryMatchType.NoDescendantFilesMatch;
            }

            excludeMayMatch |= excludeResult == DirectoryMatchType.MayContainMatchingFiles;
        }

        includeResult = DirectoryMatchTypeOperations.Normalize(
            include.MatchesDirectory(currentDirectory, directoryName));
        return excludeMayMatch && includeResult != DirectoryMatchType.NoDescendantFilesMatch
            ? DirectoryMatchType.MayContainMatchingFiles
            : includeResult;
    }

    public override void DirectoryFinished(ReadOnlySpan<char> directory)
    {
        for (int index = 0; index < excludes.Length; index++)
        {
            excludes[index].DirectoryFinished(directory);
        }

        include.DirectoryFinished(directory);
    }

    public override void Dispose()
    {
        Exception? firstException = null;
        try
        {
            include.Dispose();
        }
        catch (Exception exception)
        {
            firstException = exception;
        }

        try
        {
            FileSystemMatcherSessionFactory.DisposeSessions(excludes);
        }
        catch (Exception exception)
        {
            firstException ??= exception;
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    private static int CountWholeSubtreeExcludes(GlobMatch[] matcherSessions)
    {
        int count = 0;
        for (int index = 0; index < matcherSessions.Length; index++)
        {
            if (matcherSessions[index].CanMatchWholeSubtree)
            {
                count++;
            }
        }

        return count;
    }
}