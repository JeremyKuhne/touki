// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.ExceptionServices;

namespace Touki.Io;

internal static class FileSystemMatcherSessionFactory
{
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