// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.ExceptionServices;

namespace Touki.Io;

/// <summary>
///  Creates, validates unique ownership of, and disposes matcher session sets used by composed matchers.
/// </summary>
internal static class FileSystemMatcherSessionFactory
{
    /// <summary>
    ///  Determines whether any matcher is a framework composition.
    /// </summary>
    /// <param name="matchers">The matcher definitions to inspect.</param>
    /// <returns>
    ///  <see langword="true"/> if a matcher is a framework composition; otherwise <see langword="false"/>.
    /// </returns>
    public static bool ContainsFrameworkComposition(IFileSystemMatcher[] matchers)
    {
        for (int index = 0; index < matchers.Length; index++)
        {
            if (matchers[index] is ExclusionWinsFileSystemMatcher or OrderedFileSystemMatcher)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Determines whether any rule uses a framework composition.
    /// </summary>
    /// <param name="rules">The match rules to inspect.</param>
    /// <returns>
    ///  <see langword="true"/> if a rule uses a framework composition; otherwise <see langword="false"/>.
    /// </returns>
    public static bool ContainsFrameworkComposition(FileSystemMatchRule[] rules)
    {
        for (int index = 0; index < rules.Length; index++)
        {
            if (rules[index].Matcher is ExclusionWinsFileSystemMatcher or OrderedFileSystemMatcher)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Creates one independently owned session for each matcher definition.
    /// </summary>
    /// <param name="matchers">The matcher definitions.</param>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <param name="existingSessions">Existing sessions that the new matchers must not reuse.</param>
    /// <returns>The newly created matcher sessions.</returns>
    public static IFileSystemMatcherSession[] CreateSessions(
        IFileSystemMatcher[] matchers,
        string rootDirectory,
        IFileSystemMatcherSession[]? existingSessions = null)
    {
        IFileSystemMatcherSession[] sessions = new IFileSystemMatcherSession[matchers.Length];
        int created = 0;
        try
        {
            for (; created < matchers.Length; created++)
            {
                IFileSystemMatcherSession session = matchers[created].CreateSession(rootDirectory)
                    ?? throw new InvalidOperationException("A matcher returned a null session.");
                if (ContainsReference(sessions, created, session)
                    || existingSessions is not null
                        && ContainsReference(existingSessions, existingSessions.Length, session))
                {
                    throw new InvalidOperationException("Matcher definitions returned the same session instance.");
                }

                sessions[created] = session;
            }

            return sessions;
        }
        catch (Exception exception)
        {
            DisposeCreatedSessions(sessions, created);
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    /// <summary>
    ///  Creates one independently owned session for each match rule.
    /// </summary>
    /// <param name="rules">The match rules.</param>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <returns>The newly created matcher sessions.</returns>
    public static IFileSystemMatcherSession[] CreateSessions(
        FileSystemMatchRule[] rules,
        string rootDirectory)
    {
        IFileSystemMatcherSession[] sessions = new IFileSystemMatcherSession[rules.Length];
        int created = 0;
        try
        {
            for (; created < rules.Length; created++)
            {
                IFileSystemMatcherSession session = rules[created].Matcher.CreateSession(rootDirectory)
                    ?? throw new InvalidOperationException("A matcher returned a null session.");
                if (ContainsReference(sessions, created, session))
                {
                    throw new InvalidOperationException("Matcher definitions returned the same session instance.");
                }

                sessions[created] = session;
            }

            return sessions;
        }
        catch (Exception exception)
        {
            DisposeCreatedSessions(sessions, created);
            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    /// <summary>
    ///  Disposes every session and rethrows the first disposal exception after all sessions have been attempted.
    /// </summary>
    /// <param name="sessions">The sessions to dispose.</param>
    public static void DisposeSessions(IFileSystemMatcherSession[] sessions)
    {
        Exception? firstException = null;
        for (int index = 0; index < sessions.Length; index++)
        {
            try
            {
                sessions[index].Dispose();
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo.Capture(firstException).Throw();
        }
    }

    /// <summary>
    ///  Disposes every session and suppresses disposal exceptions.
    /// </summary>
    /// <param name="sessions">The sessions to dispose.</param>
    public static void DisposeSessionsSuppressExceptions(IFileSystemMatcherSession[] sessions) =>
        DisposeCreatedSessions(sessions, sessions.Length);

    private static void DisposeCreatedSessions(
        IFileSystemMatcherSession[] sessions,
        int count)
    {
        for (int index = 0; index < count; index++)
        {
            try
            {
                sessions[index].Dispose();
            }
            catch
            {
            }
        }
    }

    private static bool ContainsReference(
        IFileSystemMatcherSession[] sessions,
        int count,
        IFileSystemMatcherSession candidate)
    {
        for (int index = 0; index < count; index++)
        {
            if (ReferenceEquals(sessions[index], candidate))
            {
                return true;
            }
        }

        return false;
    }
}