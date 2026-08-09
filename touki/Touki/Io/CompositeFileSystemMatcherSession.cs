// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class CompositeFileSystemMatcherSession : FileSystemMatcherSession
{
    private const int StackResultBufferSize = 256;
    private const int StackPathBufferSize = 256;

    private readonly CompositeMatcherNode[] _nodes;
    private readonly int[] _edges;
    private readonly FileSystemMatchAction[] _actions;
    private readonly IFileSystemMatcherSession[] _sessions;
    private readonly int _rootNode;
    private readonly bool _hasPathSessions;
    private readonly int _rootPrefixLength;
    private int _disposed;

    private CompositeFileSystemMatcherSession(
        CompositeMatcherNode[] nodes,
        int[] edges,
        FileSystemMatchAction[] actions,
        IFileSystemMatcherSession[] sessions,
        int rootNode,
        string rootDirectory)
    {
        _nodes = nodes;
        _edges = edges;
        _actions = actions;
        _sessions = sessions;
        _rootNode = rootNode;
        _hasPathSessions = HasPathSessions(sessions);
        _rootPrefixLength = rootDirectory.Length
            + (Path.EndsInDirectorySeparator(rootDirectory) ? 0 : 1);
    }

    public static IFileSystemMatcherSession Create(
        IFileSystemMatcher matcher,
        string rootDirectory)
    {
        List<CompositeMatcherNode> nodes = [];
        List<int> edges = [];
        List<FileSystemMatchAction> actions = [];
        List<IFileSystemMatcherSession> sessions = [];
        HashSet<IFileSystemMatcherSession> sessionIdentities = new(MatcherSessionReferenceComparer.Instance);
        Stack<CompositeMatcherWorkItem> work = new();
        Stack<int> results = new();
        work.Push(new(matcher, expanded: false));

        try
        {
            while (work.Count > 0)
            {
                CompositeMatcherWorkItem item = work.Pop();
                if (item.Matcher is ExclusionWinsFileSystemMatcher exclusionWins)
                {
                    if (!item.Expanded)
                    {
                        work.Push(new(item.Matcher, expanded: true));
                        PushChildren(work, exclusionWins.Includes, exclusionWins.Excludes);
                        continue;
                    }

                    int childCount = exclusionWins.Includes.Length + exclusionWins.Excludes.Length;
                    int firstEdge = AppendChildResults(edges, actions, results, childCount);
                    nodes.Add(new(
                        CompositeMatcherNodeKind.ExclusionWins,
                        firstEdge,
                        childCount,
                        exclusionWins.Includes.Length,
                        includeUnmatched: false));
                    results.Push(nodes.Count - 1);
                    continue;
                }

                if (item.Matcher is OrderedFileSystemMatcher ordered)
                {
                    if (!item.Expanded)
                    {
                        work.Push(new(item.Matcher, expanded: true));
                        for (int index = ordered.Rules.Length - 1; index >= 0; index--)
                        {
                            work.Push(new(ordered.Rules[index].Matcher, expanded: false));
                        }

                        continue;
                    }

                    int firstEdge = AppendChildResults(
                        edges,
                        actions,
                        results,
                        ordered.Rules.Length);
                    for (int index = 0; index < ordered.Rules.Length; index++)
                    {
                        actions[firstEdge + index] = ordered.Rules[index].Action;
                    }

                    nodes.Add(new(
                        CompositeMatcherNodeKind.Ordered,
                        firstEdge,
                        ordered.Rules.Length,
                        includeCount: 0,
                        ordered.IncludeUnmatched));
                    results.Push(nodes.Count - 1);
                    continue;
                }

                IFileSystemMatcherSession session = item.Matcher.CreateSession(rootDirectory)
                    ?? throw new InvalidOperationException("A matcher returned a null session.");
                if (!sessionIdentities.Add(session))
                {
                    throw new InvalidOperationException("Matcher definitions returned the same session instance.");
                }

                sessions.Add(session);
                nodes.Add(new(sessions.Count - 1));
                results.Push(nodes.Count - 1);
            }

            if (results.Count != 1)
            {
                throw new InvalidOperationException("The matcher composition graph is invalid.");
            }

            return new CompositeFileSystemMatcherSession(
                [.. nodes],
                [.. edges],
                [.. actions],
                [.. sessions],
                results.Pop(),
                rootDirectory);
        }
        catch
        {
            FileSystemMatcherSessionFactory.DisposeSessionsSuppressExceptions([.. sessions]);
            throw;
        }
    }

    [SkipLocalsInit]
    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        if (!_hasPathSessions)
        {
            return MatchesFileCore(currentDirectory, fileName, default);
        }

        using CanonicalPathScope path = new(
            stackalloc char[StackPathBufferSize],
            _rootPrefixLength,
            currentDirectory,
            fileName);
        return MatchesFileCore(currentDirectory, fileName, path.Value);
    }

    [SkipLocalsInit]
    public override DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        using BufferScope<byte> buffer = new(
            stackalloc byte[StackResultBufferSize],
            _nodes.Length);
        Span<byte> results = buffer[.._nodes.Length];
        for (int nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
        {
            CompositeMatcherNode node = _nodes[nodeIndex];
            DirectoryMatchType result;
            if (node.Kind == CompositeMatcherNodeKind.Leaf)
            {
                result = DirectoryMatchTypeOperations.Normalize(
                    _sessions[node.SessionIndex].MatchesDirectory(currentDirectory, directoryName));
            }
            else if (node.Kind == CompositeMatcherNodeKind.ExclusionWins)
            {
                result = EvaluateExclusionWinsDirectory(node, results);
            }
            else
            {
                result = EvaluateOrderedDirectory(node, results);
            }

            results[nodeIndex] = (byte)result;
        }

        return (DirectoryMatchType)results[_rootNode];
    }

    public override void DirectoryFinished(ReadOnlySpan<char> directory)
    {
        for (int index = 0; index < _sessions.Length; index++)
        {
            _sessions[index].DirectoryFinished(directory);
        }
    }

    public override void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            FileSystemMatcherSessionFactory.DisposeSessions(_sessions);
        }
    }

    [SkipLocalsInit]
    private bool MatchesFileCore(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName,
        ReadOnlySpan<char> canonicalPath)
    {
        using BufferScope<byte> buffer = new(
            stackalloc byte[StackResultBufferSize],
            _nodes.Length);
        Span<byte> results = buffer[.._nodes.Length];
        for (int nodeIndex = 0; nodeIndex < _nodes.Length; nodeIndex++)
        {
            CompositeMatcherNode node = _nodes[nodeIndex];
            bool result;
            if (node.Kind == CompositeMatcherNodeKind.Leaf)
            {
                IFileSystemMatcherSession session = _sessions[node.SessionIndex];
                result = session is ICanonicalPathMatcherSession pathSession
                    ? pathSession.MatchesPath(canonicalPath)
                    : session.MatchesFile(currentDirectory, fileName);
            }
            else if (node.Kind == CompositeMatcherNodeKind.ExclusionWins)
            {
                result = EvaluateExclusionWinsFile(node, results);
            }
            else
            {
                result = EvaluateOrderedFile(node, results);
            }

            results[nodeIndex] = result ? (byte)1 : (byte)0;
        }

        return results[_rootNode] != 0;
    }

    private bool EvaluateExclusionWinsFile(CompositeMatcherNode node, ReadOnlySpan<byte> results)
    {
        int excludesStart = node.FirstEdge + node.IncludeCount;
        int end = node.FirstEdge + node.ChildCount;
        for (int edgeIndex = excludesStart; edgeIndex < end; edgeIndex++)
        {
            if (results[_edges[edgeIndex]] != 0)
            {
                return false;
            }
        }

        for (int edgeIndex = node.FirstEdge; edgeIndex < excludesStart; edgeIndex++)
        {
            if (results[_edges[edgeIndex]] != 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool EvaluateOrderedFile(CompositeMatcherNode node, ReadOnlySpan<byte> results)
    {
        bool included = node.IncludeUnmatched;
        int end = node.FirstEdge + node.ChildCount;
        for (int edgeIndex = node.FirstEdge; edgeIndex < end; edgeIndex++)
        {
            if (results[_edges[edgeIndex]] != 0)
            {
                included = _actions[edgeIndex] == FileSystemMatchAction.Include;
            }
        }

        return included;
    }

    private DirectoryMatchType EvaluateExclusionWinsDirectory(
        CompositeMatcherNode node,
        ReadOnlySpan<byte> results)
    {
        DirectoryMatchType includeResult = DirectoryMatchType.NoDescendantFilesMatch;
        int excludesStart = node.FirstEdge + node.IncludeCount;
        for (int edgeIndex = node.FirstEdge; edgeIndex < excludesStart; edgeIndex++)
        {
            includeResult = DirectoryMatchTypeOperations.Or(
                includeResult,
                (DirectoryMatchType)results[_edges[edgeIndex]]);
        }

        DirectoryMatchType excludeResult = DirectoryMatchType.NoDescendantFilesMatch;
        int end = node.FirstEdge + node.ChildCount;
        for (int edgeIndex = excludesStart; edgeIndex < end; edgeIndex++)
        {
            excludeResult = DirectoryMatchTypeOperations.Or(
                excludeResult,
                (DirectoryMatchType)results[_edges[edgeIndex]]);
        }

        return DirectoryMatchTypeOperations.And(
            includeResult,
            DirectoryMatchTypeOperations.Not(excludeResult));
    }

    private DirectoryMatchType EvaluateOrderedDirectory(
        CompositeMatcherNode node,
        ReadOnlySpan<byte> results)
    {
        bool canInclude = node.IncludeUnmatched;
        bool canExclude = !node.IncludeUnmatched;
        int end = node.FirstEdge + node.ChildCount;
        for (int edgeIndex = node.FirstEdge; edgeIndex < end; edgeIndex++)
        {
            DirectoryMatchType matchType = DirectoryMatchTypeOperations.Normalize(
                (DirectoryMatchType)results[_edges[edgeIndex]]);
            bool include = _actions[edgeIndex] == FileSystemMatchAction.Include;
            if (matchType == DirectoryMatchType.AllDescendantFilesMatch)
            {
                canInclude = include;
                canExclude = !include;
            }
            else if (matchType == DirectoryMatchType.MayContainMatchingFiles)
            {
                canInclude |= include;
                canExclude |= !include;
            }
        }

        if (canInclude && canExclude)
        {
            return DirectoryMatchType.MayContainMatchingFiles;
        }

        return canInclude
            ? DirectoryMatchType.AllDescendantFilesMatch
            : DirectoryMatchType.NoDescendantFilesMatch;
    }

    private static int AppendChildResults(
        List<int> edges,
        List<FileSystemMatchAction> actions,
        Stack<int> results,
        int childCount)
    {
        int firstEdge = edges.Count;
        for (int index = 0; index < childCount; index++)
        {
            edges.Add(0);
            actions.Add(default);
        }

        for (int index = childCount - 1; index >= 0; index--)
        {
            edges[firstEdge + index] = results.Pop();
        }

        return firstEdge;
    }

    private static void PushChildren(
        Stack<CompositeMatcherWorkItem> work,
        IFileSystemMatcher[] includes,
        IFileSystemMatcher[] excludes)
    {
        for (int index = excludes.Length - 1; index >= 0; index--)
        {
            work.Push(new(excludes[index], expanded: false));
        }

        for (int index = includes.Length - 1; index >= 0; index--)
        {
            work.Push(new(includes[index], expanded: false));
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
