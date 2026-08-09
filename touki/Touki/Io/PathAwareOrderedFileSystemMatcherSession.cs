// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal sealed class PathAwareOrderedFileSystemMatcherSession(
    FileSystemMatchRule[] rules,
    IFileSystemMatcherSession[] sessions,
    bool includeUnmatched,
    string rootDirectory) : OrderedFileSystemMatcherSessionBase(rules, sessions, includeUnmatched)
{
    private const int StackPathBufferSize = 256;

    private readonly int _rootPrefixLength = rootDirectory.Length
        + (Path.EndsInDirectorySeparator(rootDirectory) ? 0 : 1);

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
        bool included = IncludeUnmatched;
        for (int index = 0; index < Sessions.Length; index++)
        {
            IFileSystemMatcherSession session = Sessions[index];
            bool matches = session is ICanonicalPathMatcherSession pathSession
                ? pathSession.MatchesPath(path.Value)
                : session.MatchesFile(currentDirectory, fileName);
            if (matches)
            {
                included = Rules[index].Action == FileSystemMatchAction.Include;
            }
        }

        return included;
    }
}
