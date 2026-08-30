// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Defines an ordered include and exclude composition in which the last matching rule determines the result.
/// </summary>
/// <param name="rules">The ordered match rules.</param>
/// <param name="includeUnmatched">Whether paths that match no rule are included.</param>
internal sealed class OrderedFileSystemMatcher(
    FileSystemMatchRule[] rules,
    bool includeUnmatched) : IFileSystemMatcher
{
    /// <summary>
    ///  Gets the ordered match rules.
    /// </summary>
    internal FileSystemMatchRule[] Rules => rules;

    /// <summary>
    ///  Gets whether paths that match no rule are included.
    /// </summary>
    internal bool IncludeUnmatched => includeUnmatched;

    /// <summary>
    ///  Creates a matcher session bound to <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <returns>A new matcher session owned by the caller.</returns>
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