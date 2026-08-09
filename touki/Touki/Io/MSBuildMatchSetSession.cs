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

    public MSBuildMatchSetSession(IFileSystemMatcherSession include)
    {
        ArgumentNullException.ThrowIfNull(include);
        _include = include;
    }

    public void AddExclude(IFileSystemMatcherSession exclude)
    {
        ArgumentNullException.ThrowIfNull(exclude);
        ObjectDisposedException.ThrowIf(Disposed, this);
        _excludes ??= [];
        _excludes.Add(exclude);
    }

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