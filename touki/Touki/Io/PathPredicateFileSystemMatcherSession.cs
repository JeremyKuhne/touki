// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class PathPredicateFileSystemMatcherSession : FileSystemMatcherSession, ICanonicalPathMatcherSession
{
    private const int StackPathBufferSize = 256;

    private readonly int _rootPrefixLength;
    private readonly PathMatchPredicate _predicate;

    public PathPredicateFileSystemMatcherSession(
        string rootDirectory,
        PathMatchPredicate predicate)
    {
        _rootPrefixLength = rootDirectory.Length
            + (Path.EndsInDirectorySeparator(rootDirectory) ? 0 : 1);
        _predicate = predicate;
    }

    [SkipLocalsInit]
    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        using CanonicalPathScope path = new(
            stackalloc char[StackPathBufferSize],
            _rootPrefixLength,
            currentDirectory,
            fileName);
        return MatchesPath(path.Value);
    }

    public bool MatchesPath(ReadOnlySpan<char> rootRelativePath) => _predicate(rootRelativePath);
}