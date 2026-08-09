// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class ExclusionWinsFileSystemMatcher(
    IFileSystemMatcher[] includes,
    IFileSystemMatcher[] excludes) : IFileSystemMatcher
{
    internal IFileSystemMatcher[] Includes => includes;

    internal IFileSystemMatcher[] Excludes => excludes;

    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        if (FileSystemMatcherSessionFactory.ContainsFrameworkComposition(includes)
            || FileSystemMatcherSessionFactory.ContainsFrameworkComposition(excludes))
        {
            return CompositeFileSystemMatcherSession.Create(this, rootDirectory);
        }

        IFileSystemMatcherSession[] includeSessions =
            FileSystemMatcherSessionFactory.CreateSessions(includes, rootDirectory);
        try
        {
            IFileSystemMatcherSession[] excludeSessions =
                FileSystemMatcherSessionFactory.CreateSessions(
                    excludes,
                    rootDirectory,
                    includeSessions);
            return HasPathSessions(includeSessions) || HasPathSessions(excludeSessions)
                ? new PathAwareExclusionWinsFileSystemMatcherSession(
                    includeSessions,
                    excludeSessions,
                    rootDirectory)
                : new ExclusionWinsFileSystemMatcherSession(includeSessions, excludeSessions);
        }
        catch (Exception exception)
        {
            FileSystemMatcherSessionFactory.DisposeSessionsSuppressExceptions(includeSessions);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private static bool HasPathSessions(IFileSystemMatcherSession[] sessions)
    {
        for (int index = 0; index < sessions.Length; index++)
        {
            if (sessions[index] is ICanonicalPathMatcherSession)
            {
                return true;
            }
        }

        return false;
    }
}