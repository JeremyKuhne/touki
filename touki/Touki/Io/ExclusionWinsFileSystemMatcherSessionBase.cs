// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal abstract class ExclusionWinsFileSystemMatcherSessionBase(
    IFileSystemMatcherSession[] includes,
    IFileSystemMatcherSession[] excludes) : FileSystemMatcherSession
{
    private int _disposed;

    protected IFileSystemMatcherSession[] Includes { get; } = includes;

    protected IFileSystemMatcherSession[] Excludes { get; } = excludes;

    public override DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        DirectoryMatchType includeResult = DirectoryMatchType.NoDescendantFilesMatch;
        for (int index = 0; index < Includes.Length; index++)
        {
            includeResult = DirectoryMatchTypeOperations.Or(
                includeResult,
                Includes[index].MatchesDirectory(currentDirectory, directoryName));
        }

        DirectoryMatchType excludeResult = DirectoryMatchType.NoDescendantFilesMatch;
        for (int index = 0; index < Excludes.Length; index++)
        {
            excludeResult = DirectoryMatchTypeOperations.Or(
                excludeResult,
                Excludes[index].MatchesDirectory(currentDirectory, directoryName));
        }

        return DirectoryMatchTypeOperations.And(
            includeResult,
            DirectoryMatchTypeOperations.Not(excludeResult));
    }

    public override void DirectoryFinished(ReadOnlySpan<char> directory)
    {
        for (int index = 0; index < Excludes.Length; index++)
        {
            Excludes[index].DirectoryFinished(directory);
        }

        for (int index = 0; index < Includes.Length; index++)
        {
            Includes[index].DirectoryFinished(directory);
        }
    }

    public override void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Exception? firstException = null;
        try
        {
            FileSystemMatcherSessionFactory.DisposeSessions(Includes);
        }
        catch (Exception exception)
        {
            firstException = exception;
        }

        try
        {
            FileSystemMatcherSessionFactory.DisposeSessions(Excludes);
        }
        catch (Exception exception)
        {
            firstException ??= exception;
        }

        if (firstException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }
}
