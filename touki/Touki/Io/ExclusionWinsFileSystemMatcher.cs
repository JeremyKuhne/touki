// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Defines a matcher composition in which any matching exclusion overrides all matching includes.
/// </summary>
/// <param name="includes">The matchers that include paths.</param>
/// <param name="excludes">The matchers that exclude paths.</param>
internal sealed class ExclusionWinsFileSystemMatcher(
    IFileSystemMatcher[] includes,
    IFileSystemMatcher[] excludes) : IFileSystemMatcher
{
    /// <summary>
    ///  Gets the matchers that include paths.
    /// </summary>
    internal IFileSystemMatcher[] Includes => includes;

    /// <summary>
    ///  Gets the matchers that exclude paths.
    /// </summary>
    internal IFileSystemMatcher[] Excludes => excludes;

    /// <summary>
    ///  Creates a matcher session bound to <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <returns>A new matcher session owned by the caller.</returns>
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