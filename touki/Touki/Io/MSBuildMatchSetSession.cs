// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using System.Runtime.ExceptionServices;

namespace Touki.Io;

/// <summary>
///  Owns one MSBuild include session and its applicable exclude sessions.
/// </summary>
internal sealed class MSBuildMatchSetSession : DisposableBase, IFileSystemMatcherSession
{
    private readonly IFileSystemMatcherSession _include;
    private SingleOptimizedList<IFileSystemMatcherSession, ArrayPoolList<IFileSystemMatcherSession>>? _excludes;

    /// <summary>
    ///  Initializes a match set with an owned include session.
    /// </summary>
    /// <param name="include">The include matcher session.</param>
    public MSBuildMatchSetSession(IFileSystemMatcherSession include)
    {
        ArgumentNullException.ThrowIfNull(include);
        _include = include;
    }

    /// <summary>
    ///  Adds an owned exclude matcher session.
    /// </summary>
    /// <param name="exclude">The exclude matcher session.</param>
    public void AddExclude(IFileSystemMatcherSession exclude)
    {
        ArgumentNullException.ThrowIfNull(exclude);
        ObjectDisposedException.ThrowIf(Disposed, this);
        _excludes ??= [];
        _excludes.Add(exclude);
    }

    /// <summary>
    ///  Determines whether a file matches the include session and no exclude session.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the file.</param>
    /// <param name="fileName">The filename.</param>
    /// <returns><see langword="true"/> if the file matches; otherwise <see langword="false"/>.</returns>
    public bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        if (_excludes is { } excludes)
        {
#if NETFRAMEWORK
            int count = excludes.Count;
            for (int index = 0; index < count; index++)
            {
                if (excludes[index].MatchesFile(currentDirectory, fileName))
                {
                    return false;
                }
            }
#else
            foreach (IFileSystemMatcherSession exclude in excludes)
            {
                if (exclude.MatchesFile(currentDirectory, fileName))
                {
                    return false;
                }
            }
#endif
        }

        return _include.MatchesFile(currentDirectory, fileName);
    }

    /// <summary>
    ///  Combines include and exclude classifications for a candidate directory.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the candidate directory.</param>
    /// <param name="directoryName">The candidate directory name.</param>
    /// <returns>The combined match classification for the candidate directory and its descendants.</returns>
    public DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        bool excludeMayMatch = false;
        if (_excludes is { } excludes)
        {
#if NETFRAMEWORK
            int count = excludes.Count;
            for (int index = 0; index < count; index++)
            {
                DirectoryMatchType excludeResult = excludes[index].MatchesDirectory(
                    currentDirectory,
                    directoryName);
                if (excludeResult == DirectoryMatchType.AllDescendantFilesMatch)
                {
                    return DirectoryMatchType.NoDescendantFilesMatch;
                }

                excludeMayMatch |= excludeResult == DirectoryMatchType.MayContainMatchingFiles;
            }
#else
            foreach (IFileSystemMatcherSession exclude in excludes)
            {
                DirectoryMatchType excludeResult = exclude.MatchesDirectory(currentDirectory, directoryName);
                if (excludeResult == DirectoryMatchType.AllDescendantFilesMatch)
                {
                    return DirectoryMatchType.NoDescendantFilesMatch;
                }

                excludeMayMatch |= excludeResult == DirectoryMatchType.MayContainMatchingFiles;
            }
#endif
        }

        DirectoryMatchType includeResult = _include.MatchesDirectory(currentDirectory, directoryName);
        return excludeMayMatch && includeResult != DirectoryMatchType.NoDescendantFilesMatch
            ? DirectoryMatchType.MayContainMatchingFiles
            : includeResult;
    }

            /// <summary>
            ///  Forwards completion of a directory to the include and exclude sessions.
            /// </summary>
            /// <param name="directory">The completed directory.</param>
    public void DirectoryFinished(ReadOnlySpan<char> directory)
    {
        if (_excludes is { } excludes)
        {
#if NETFRAMEWORK
            int count = excludes.Count;
            for (int index = 0; index < count; index++)
            {
                excludes[index].DirectoryFinished(directory);
            }
#else
            foreach (IFileSystemMatcherSession exclude in excludes)
            {
                exclude.DirectoryFinished(directory);
            }
#endif
        }

        _include.DirectoryFinished(directory);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Exception? firstException = null;
        try
        {
            _include.Dispose();
        }
        catch (Exception exception)
        {
            firstException = exception;
        }

        if (_excludes is { } excludes)
        {
            foreach (IFileSystemMatcherSession exclude in excludes)
            {
                try
                {
                    exclude.Dispose();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }
        }

        try
        {
            _excludes?.Dispose();
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
}