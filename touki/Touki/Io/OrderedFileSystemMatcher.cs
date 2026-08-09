// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class OrderedFileSystemMatcher(
    FileSystemMatchRule[] rules,
    bool includeUnmatched) : IFileSystemMatcher
{
    internal FileSystemMatchRule[] Rules => rules;

    internal bool IncludeUnmatched => includeUnmatched;

    public IFileSystemMatcherSession CreateSession(string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(rootDirectory);
        if (FileSystemMatcherSessionFactory.ContainsFrameworkComposition(rules))
        {
            return CompositeFileSystemMatcherSession.Create(this, rootDirectory);
        }

        IFileSystemMatcherSession[] sessions =
            FileSystemMatcherSessionFactory.CreateSessions(rules, rootDirectory);
        return HasPathSessions(sessions)
            ? new PathAwareOrderedFileSystemMatcherSession(
                rules,
                sessions,
                includeUnmatched,
                rootDirectory)
            : new OrderedFileSystemMatcherSession(rules, sessions, includeUnmatched);
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