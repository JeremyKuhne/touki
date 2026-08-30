// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Evaluates compiled Git-ignore rules against canonical root-relative paths and classifies directories for pruning.
/// </summary>
internal sealed class GitIgnoreFileSystemMatcherSession : FileSystemMatcherSession
{
    private const int StackPathBufferSize = 256;

    private readonly GitIgnoreRules _rules;
    private readonly string _rootDirectory;
    private readonly int _rootPrefixLength;
    private readonly bool _matchIgnored;

    /// <summary>
    ///  Initializes a session for compiled Git-ignore rules and an enumeration root.
    /// </summary>
    /// <param name="rules">The compiled Git-ignore rules.</param>
    /// <param name="rootDirectory">The normalized enumeration root.</param>
    /// <param name="matchIgnored">Whether the session matches ignored paths instead of included paths.</param>
    public GitIgnoreFileSystemMatcherSession(
        GitIgnoreRules rules,
        string rootDirectory,
        bool matchIgnored)
    {
        _rules = rules;
        _rootDirectory = rootDirectory;
        _rootPrefixLength = rootDirectory.Length
            + (Path.EndsInDirectorySeparator(rootDirectory) ? 0 : 1);

        _matchIgnored = matchIgnored;
    }

    [SkipLocalsInit]
    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName)
    {
        ReadOnlySpan<char> relativeDirectory = GetRelativeDirectory(currentDirectory);
        if (relativeDirectory.IsEmpty)
        {
            bool ignored = _rules.IsIgnoredFileCore(fileName);
            return ignored == _matchIgnored;
        }

        int pathLength = checked(relativeDirectory.Length + 1 + fileName.Length);
        using BufferScope<char> buffer = new(stackalloc char[StackPathBufferSize], pathLength);
        Span<char> path = buffer[..pathLength];
        CopyCanonicalDirectory(relativeDirectory, path);
        path[relativeDirectory.Length] = '/';
        fileName.CopyTo(path[(relativeDirectory.Length + 1)..]);
        bool nestedIgnored = _rules.IsIgnoredFileCore(path);
        return nestedIgnored == _matchIgnored;
    }

    [SkipLocalsInit]
    public override DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        if (_rules.Count == 0)
        {
            return _matchIgnored
                ? DirectoryMatchType.NoDescendantFilesMatch
                : DirectoryMatchType.AllDescendantFilesMatch;
        }

        ReadOnlySpan<char> relativeDirectory = GetRelativeDirectory(currentDirectory);
        bool ignored;
        if (relativeDirectory.IsEmpty)
        {
            ignored = _rules.IsIgnoredDirectoryCore(directoryName);
        }
        else
        {
            int pathLength = checked(relativeDirectory.Length + 1 + directoryName.Length);
            using BufferScope<char> buffer = new(stackalloc char[StackPathBufferSize], pathLength);
            Span<char> path = buffer[..pathLength];
            CopyCanonicalDirectory(relativeDirectory, path);
            path[relativeDirectory.Length] = '/';
            directoryName.CopyTo(path[(relativeDirectory.Length + 1)..]);
            ignored = _rules.IsIgnoredDirectoryCore(path);
        }

        if (ignored)
        {
            return _matchIgnored
                ? DirectoryMatchType.AllDescendantFilesMatch
                : DirectoryMatchType.NoDescendantFilesMatch;
        }

        return DirectoryMatchType.MayContainMatchingFiles;
    }

    private ReadOnlySpan<char> GetRelativeDirectory(ReadOnlySpan<char> currentDirectory)
    {
        if (currentDirectory.Equals(_rootDirectory, StringComparison.Ordinal))
        {
            return default;
        }

        return currentDirectory.Length >= _rootPrefixLength
            ? currentDirectory[_rootPrefixLength..]
            : default;
    }

    private static void CopyCanonicalDirectory(
        ReadOnlySpan<char> source,
        Span<char> destination)
    {
        char primarySeparator = Path.DirectorySeparatorChar;
        char alternateSeparator = Path.AltDirectorySeparatorChar;
        ref char sourceReference = ref MemoryMarshal.GetReference(source);
        ref char destinationReference = ref MemoryMarshal.GetReference(destination);
        for (int index = 0; index < source.Length; index++)
        {
            char character = Unsafe.Add(ref sourceReference, index);
            Unsafe.Add(ref destinationReference, index) =
                character == primarySeparator || character == alternateSeparator
                    ? '/'
                    : character;
        }
    }
}