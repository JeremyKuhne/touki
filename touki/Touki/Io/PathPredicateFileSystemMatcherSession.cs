// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Invokes a path predicate with a canonical root-relative file path.
/// </summary>
internal sealed class PathPredicateFileSystemMatcherSession : FileSystemMatcherSession, ICanonicalPathMatcherSession
{
    private const int StackPathBufferSize = 256;

    private readonly int _rootPrefixLength;
    private readonly PathMatchPredicate _predicate;

    /// <summary>
    ///  Initializes a path-predicate session for an enumeration root.
    /// </summary>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <param name="predicate">The path predicate.</param>
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

    /// <summary>
    ///  Invokes the predicate for a canonical root-relative path.
    /// </summary>
    /// <param name="rootRelativePath">The canonical root-relative path.</param>
    /// <returns><see langword="true"/> if the path matches; otherwise <see langword="false"/>.</returns>
    public bool MatchesPath(ReadOnlySpan<char> rootRelativePath) => _predicate(rootRelativePath);
}