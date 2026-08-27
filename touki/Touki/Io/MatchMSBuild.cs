// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

namespace Touki.Io;

/// <summary>
///  Represents a specification for matching files and directories in an MSBuild project.
/// </summary>
internal sealed partial class MatchMSBuild : DisposableBase, IFileSystemMatcherSession
{
    private readonly StringSegment _fixedPath;
    private readonly StringSegment _fileName;
    private readonly int _startDirectoryLength;
    private readonly MatchType _directoryMatchType;
    private readonly MatchCasing _matchCasing;
    private readonly MatchCasing _fileNameMatchCasing;
    private readonly MSBuildFileNamePattern _fileNamePattern;

    /// <summary>
    ///  Represents a simple, recursive match all files of a given pattern. (e.g. "**/*.cs").
    /// </summary>
    public bool AlwaysRecurse { get; }

    /// <summary>
    ///  Directory portion of the spec ends with "**" indicating it can match any directory.
    /// </summary>
    public bool EndsInAnyDirectory { get; }

    // Cache for current directory being processed - valid until OnDirectoryFinished is called
    private bool _cacheValid;
    private bool _cachedFullyMatches;

    private readonly ContiguousList<SpecSegment> _specSegments;
    private readonly int _anyDirectoryCount;
    private readonly int _singleAnyDirectoryIndex;

    /// <summary>
    ///  Constructs a new <see cref="MatchMSBuild"/> from a parsed, fully qualified <see cref="MSBuildSpecification"/>.
    /// </summary>
    /// <param name="specification">The parsed, fully qualified specification.</param>
    /// <param name="matchType">The type of matching to use for the specification.</param>
    /// <param name="matchCasing">The case sensitivity to use.</param>
    public MatchMSBuild(MSBuildSpecification specification, MatchType matchType, MatchCasing matchCasing)
        : this(specification, matchType, matchCasing, forceLogicalSemantics: false)
    {
    }

    /// <summary>
    ///  Constructs a matcher from a parsed specification with optional logical file-system semantics.
    /// </summary>
    /// <param name="specification">The parsed, fully qualified specification.</param>
    /// <param name="matchType">The type of matching to use for the specification.</param>
    /// <param name="matchCasing">The case sensitivity to use.</param>
    /// <param name="forceLogicalSemantics">Whether to bypass file-system filename semantics.</param>
    internal MatchMSBuild(
        MSBuildSpecification specification,
        MatchType matchType,
        MatchCasing matchCasing,
        bool forceLogicalSemantics)
        : this(
            specification.FixedPath,
            specification.WildPath,
            specification.FileName,
            matchType,
            matchCasing,
            forceLogicalSemantics)
    {
    }

    /// <summary>
    ///  Constructs a new <see cref="MatchMSBuild"/> from a fixed path, wild path, and filename.
    /// </summary>
    /// <param name="fixedPath">
    ///  The fixed part of the path, Must be normalized for the current platform (e.g., using <see cref="Path.GetFullPath(string)"/>).
    /// </param>
    /// <param name="wildPath">
    ///  The wild part of the path, if any (the first directory segment with wild characters up to the filename).
    /// </param>
    /// <param name="fileName">The file name specification</param>
    /// <param name="matchType">The type of matching to use for the specification.</param>
    /// <param name="matchCasing">The case sensitivity to use.</param>
    private MatchMSBuild(
        StringSegment fixedPath,
        StringSegment wildPath,
        StringSegment fileName,
        MatchType matchType,
        MatchCasing matchCasing,
        bool forceLogicalSemantics)
    {
        bool isFileSystemSearchShape = wildPath.IsEmpty
            || wildPath == "**"
            || IsOptimizedDirectoryPattern(wildPath);
        bool useFileSystemFileNameSemantics = !forceLogicalSemantics
            && UsesFileSystemFileNameSemantics(wildPath);
        bool useRawLogicalFileNameSemantics = isFileSystemSearchShape
            && !useFileSystemFileNameSemantics;
    #if NETFRAMEWORK
        _directoryMatchType = useFileSystemFileNameSemantics
            ? matchType
            : MatchType.Simple;
    #else
        _directoryMatchType = MatchType.Simple;
    #endif
        _matchCasing = Paths.GetFinalCasing(matchCasing);
        _fileNameMatchCasing = useFileSystemFileNameSemantics
            ? _matchCasing
            : MatchCasing.CaseInsensitive;

        // Directories are returned without trailing separators
        _fixedPath = fixedPath.TrimEnd(Path.DirectorySeparatorChar);
        _startDirectoryLength = fixedPath.Length;
        _fileName = fileName;
        _fileNamePattern = new(
            fileName,
            matchType,
            useFileSystemSemantics: useFileSystemFileNameSemantics,
            useRawLogicalSemantics: useRawLogicalFileNameSemantics);

        // Build directory spec segments from the spec's WildPath
        if (!wildPath.IsEmpty)
        {
            bool ignoreCase = forceLogicalSemantics;
            PathSegmentEnumerator enumerator = new(wildPath);
            while (enumerator.MoveNext())
            {
                StringSegment segment = new(enumerator.Current.ToString());
                _specSegments ??= new SingleOptimizedList<SpecSegment, ArrayPoolList<SpecSegment>>();
                if (_specSegments.Count == 0 || !segment.Equals("**") || !_specSegments[^1].IsAnyDirectory)
                {
                    SpecSegment specSegment = new(segment, ignoreCase);
                    _specSegments.Add(specSegment);
                    ignoreCase |= specSegment.IsAnyDirectory;
                }
            }
        }

        _specSegments ??= EmptyList<SpecSegment>.Instance;

        AlwaysRecurse = wildPath == "**";

        bool endsInAny = false;
        int anyDirectoryCount = 0;
        int singleAnyDirectoryIndex = -1;
        if (_specSegments.Count > 0)
        {
            endsInAny = _specSegments[^1].IsAnyDirectory;
            for (int index = 0; index < _specSegments.Count; index++)
            {
                if (_specSegments[index].IsAnyDirectory)
                {
                    anyDirectoryCount++;
                    singleAnyDirectoryIndex = index;
                }
            }
        }

        EndsInAnyDirectory = endsInAny;
        _anyDirectoryCount = anyDirectoryCount;
        _singleAnyDirectoryIndex = singleAnyDirectoryIndex;
    }

    private static bool UsesFileSystemFileNameSemantics(StringSegment wildPath)
    {
        if (wildPath.IsEmpty || wildPath == "**")
        {
            return true;
        }

        bool optimizedDirectoryPattern = IsOptimizedDirectoryPattern(wildPath);

        #if NETFRAMEWORK
            return optimizedDirectoryPattern;
        #else
            return optimizedDirectoryPattern && !OperatingSystem.IsLinux();
        #endif
    }

    private static bool IsOptimizedDirectoryPattern(StringSegment wildPath)
    {
        char separator = Path.DirectorySeparatorChar;
        return wildPath.Length >= 7
            && wildPath[0] == '*'
            && wildPath[1] == '*'
            && wildPath[2] == separator
            && wildPath[^3] == separator
            && wildPath[^2] == '*'
            && wildPath[^1] == '*'
            && wildPath[3..^3].IndexOf(separator) < 0;
    }

    /// <inheritdoc/>
    public void DirectoryFinished(ReadOnlySpan<char> directory)
    {
        // Invalidate the cache when we finish processing a directory
        _cacheValid = false;
    }

    /// <inheritdoc/>
    public DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        if (AlwaysRecurse)
        {
            // Optimized case for "**/*.cs"
            return _fileName == "*"
                ? DirectoryMatchType.AllDescendantFilesMatch
                : DirectoryMatchType.MayContainMatchingFiles;
        }

        if (_specSegments.Count == 0)
        {
            // No directory segments to match.
            return DirectoryMatchType.NoDescendantFilesMatch;
        }

        // Validate that the current directory is at or under the fixed path portion of the spec
        bool ignoreCase = _matchCasing == MatchCasing.CaseInsensitive;
        if (!_fixedPath.IsEmpty && !Paths.IsSameOrSubdirectory(_fixedPath, currentDirectory, ignoreCase))
        {
            return Paths.CandidateIsSameOrAncestorOf(
                _fixedPath,
                currentDirectory,
                directoryName,
                ignoreCase)
                    ? DirectoryMatchType.MayContainMatchingFiles
                    : DirectoryMatchType.NoDescendantFilesMatch;
        }

        // Get the relative path from start directory to this directory
        ReadOnlySpan<char> relativePath = GetRelativeDirectoryPath(currentDirectory);

        if (!_cacheValid)
        {
            UpdateCachedMatchState(relativePath);
        }

        if (_cachedFullyMatches && EndsInAnyDirectory)
        {
            // If the current directory fully matches the pattern and it ends with "**", we should always recurse.
            return _fileName == "*"
                ? DirectoryMatchType.AllDescendantFilesMatch
                : DirectoryMatchType.MayContainMatchingFiles;
        }

        // Check if recursing into this directory would match the pattern
        // Combine relative path and entry filename into a VirtualPath to avoid string allocation

        PathSegmentEnumerator virtualPath = new(relativePath, directoryName);

        if (virtualPath.Length == 0)
        {
            // No relative directory path, recurse if there are no spec segments or if the first segment is "**".
            return _specSegments.Count == 0 || _specSegments[0].IsAnyDirectory
                ? DirectoryMatchType.MayContainMatchingFiles
                : DirectoryMatchType.NoDescendantFilesMatch;
        }

        return MatchSegments(ref virtualPath) switch
        {
            PathMatchState.FullMatch when EndsInAnyDirectory && _fileName == "*" =>
                DirectoryMatchType.AllDescendantFilesMatch,
            PathMatchState.FullMatch or PathMatchState.PartialMatch => DirectoryMatchType.MayContainMatchingFiles,
            _ => DirectoryMatchType.NoDescendantFilesMatch
        };
    }

    /// <inheritdoc/>
    public bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        // Validate that the current directory is at or under the fixed path portion of the spec
        bool ignoreCase = _matchCasing == MatchCasing.CaseInsensitive;
        if (!_fixedPath.IsEmpty && !Paths.IsSameOrSubdirectory(_fixedPath, currentDirectory, ignoreCase))
        {
            return false;
        }

        // Get the relative path from start directory to this file's directory
        ReadOnlySpan<char> relativePath = GetRelativeDirectoryPath(currentDirectory);

        if (!_cacheValid)
        {
            UpdateCachedMatchState(relativePath);
        }

        // Check if the current directory fully matches the pattern and the file name matches
        // Use cached result since we'll be called multiple times for files in the same directory
        return _cachedFullyMatches
            && _fileNamePattern.Matches(fileName, _fileNameMatchCasing);
    }

    private void UpdateCachedMatchState(ReadOnlySpan<char> relativePath)
    {
        if (_specSegments.Count == 0)
        {
            _cachedFullyMatches = AlwaysRecurse || relativePath.IsEmpty;
        }
        else
        {
            PathSegmentEnumerator virtualPath = new(relativePath);
            _cachedFullyMatches = MatchSegments(ref virtualPath) == PathMatchState.FullMatch;
        }

        _cacheValid = true;
    }

    private ReadOnlySpan<char> GetRelativeDirectoryPath(ReadOnlySpan<char> fullDirectory) =>
        // Remove the start directory prefix to get the relative path
        fullDirectory.Length <= _startDirectoryLength ? default : fullDirectory[(_startDirectoryLength)..];

    /// <summary>
    ///  Matches the given path segments against the specification segments.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> for partial match if the current directory does not fully match the specification, but
    ///  could match on a subdirectory. <see langword="true"/> for full match if the current directory fully matches
    ///  the specification (and therefore, the file names should be matched).
    /// </returns>
    private PathMatchState MatchSegments(ref PathSegmentEnumerator pathSegments)
    {
        return _anyDirectoryCount switch
        {
            0 => MatchWithoutAnyDirectory(ref pathSegments),
            1 => MatchSingleAnyDirectory(ref pathSegments),
            2 when _specSegments.Count == 3
                && _specSegments[0].IsAnyDirectory
                && _specSegments[2].IsAnyDirectory => MatchAnyDirectoryAnchor(ref pathSegments),
            _ => MatchMultipleAnyDirectories(ref pathSegments)
        };
    }

    private PathMatchState MatchWithoutAnyDirectory(ref PathSegmentEnumerator pathSegments)
    {
        int specIndex = 0;
        while (pathSegments.MoveNext())
        {
            if (specIndex >= _specSegments.Count
                || !MatchesSegment(pathSegments.Current, _specSegments[specIndex]))
            {
                return PathMatchState.NoMatch;
            }

            specIndex++;
        }

        return specIndex == _specSegments.Count
            ? PathMatchState.FullMatch
            : PathMatchState.PartialMatch;
    }

    private PathMatchState MatchSingleAnyDirectory(ref PathSegmentEnumerator pathSegments)
    {
        if (_singleAnyDirectoryIndex == 0)
        {
            ReversePathSegmentEnumerator reversePath = new(
                pathSegments.FirstPath,
                pathSegments.SecondPath);
            for (int specIndex = _specSegments.Count - 1; specIndex > 0; specIndex--)
            {
                if (!reversePath.MovePrevious()
                    || !MatchesSegment(reversePath.Current, _specSegments[specIndex]))
                {
                    return PathMatchState.PartialMatch;
                }
            }

            return PathMatchState.FullMatch;
        }

        if (_singleAnyDirectoryIndex == _specSegments.Count - 1)
        {
            for (int specIndex = 0; specIndex < _singleAnyDirectoryIndex; specIndex++)
            {
                if (!pathSegments.MoveNext())
                {
                    return PathMatchState.PartialMatch;
                }

                if (!MatchesSegment(pathSegments.Current, _specSegments[specIndex]))
                {
                    return PathMatchState.NoMatch;
                }
            }

            return PathMatchState.FullMatch;
        }

        PathSegmentEnumerator counter = pathSegments;
        int pathSegmentCount = 0;
        while (counter.MoveNext())
        {
            pathSegmentCount++;
        }

        for (int specIndex = 0; specIndex < _singleAnyDirectoryIndex; specIndex++)
        {
            if (!pathSegments.MoveNext())
            {
                return PathMatchState.PartialMatch;
            }

            if (!MatchesSegment(pathSegments.Current, _specSegments[specIndex]))
            {
                return PathMatchState.NoMatch;
            }
        }

        int suffixSegmentCount = _specSegments.Count - _singleAnyDirectoryIndex - 1;
        int remainingPathSegmentCount = pathSegmentCount - _singleAnyDirectoryIndex;
        if (remainingPathSegmentCount < suffixSegmentCount)
        {
            return PathMatchState.PartialMatch;
        }

        int globstarSegmentCount = remainingPathSegmentCount - suffixSegmentCount;
        for (int index = 0; index < globstarSegmentCount; index++)
        {
            bool moved = pathSegments.MoveNext();
            Debug.Assert(moved);
        }

        for (int suffixIndex = 0; suffixIndex < suffixSegmentCount; suffixIndex++)
        {
            bool moved = pathSegments.MoveNext();
            Debug.Assert(moved);

            if (!MatchesSegment(
                pathSegments.Current,
                _specSegments[_singleAnyDirectoryIndex + suffixIndex + 1]))
            {
                return PathMatchState.PartialMatch;
            }
        }

        return PathMatchState.FullMatch;
    }

    private PathMatchState MatchAnyDirectoryAnchor(ref PathSegmentEnumerator pathSegments)
    {
        SpecSegment anchor = _specSegments[1];
        while (pathSegments.MoveNext())
        {
            if (MatchesSegment(pathSegments.Current, anchor))
            {
                return PathMatchState.FullMatch;
            }
        }

        return PathMatchState.PartialMatch;
    }

    private PathMatchState MatchMultipleAnyDirectories(ref PathSegmentEnumerator pathSegments)
    {
        int stateCount = _specSegments.Count + 1;
        using BufferScope<byte> stateBuffer = new(stackalloc byte[256], checked(stateCount * 2));
        Span<byte> activeStates = stateBuffer[..stateCount];
        Span<byte> nextStates = stateBuffer.Slice(stateCount, stateCount);
        activeStates.Clear();
        nextStates.Clear();

        activeStates[0] = 1;
        ApplyAnyDirectoryClosure(activeStates);

        while (pathSegments.MoveNext())
        {
            nextStates.Clear();
            bool hasActiveState = false;

            for (int specIndex = 0; specIndex < _specSegments.Count; specIndex++)
            {
                if (activeStates[specIndex] == 0)
                {
                    continue;
                }

                SpecSegment currentSpec = _specSegments[specIndex];
                if (currentSpec.IsAnyDirectory)
                {
                    nextStates[specIndex] = 1;
                    hasActiveState = true;
                }
                else if (MatchesSegment(pathSegments.Current, currentSpec))
                {
                    nextStates[specIndex + 1] = 1;
                    hasActiveState = true;
                }
            }

            if (!hasActiveState)
            {
                return PathMatchState.NoMatch;
            }

            ApplyAnyDirectoryClosure(nextStates);

            Span<byte> previousStates = activeStates;
            activeStates = nextStates;
            nextStates = previousStates;
        }

        if (activeStates[_specSegments.Count] != 0)
        {
            return PathMatchState.FullMatch;
        }

        for (int specIndex = 0; specIndex < _specSegments.Count; specIndex++)
        {
            if (activeStates[specIndex] != 0)
            {
                return PathMatchState.PartialMatch;
            }
        }

        return PathMatchState.NoMatch;
    }

    private void ApplyAnyDirectoryClosure(Span<byte> states)
    {
        for (int specIndex = 0; specIndex < _specSegments.Count; specIndex++)
        {
            if (states[specIndex] != 0 && _specSegments[specIndex].IsAnyDirectory)
            {
                states[specIndex + 1] = 1;
            }
        }
    }

    private bool MatchesSegment(ReadOnlySpan<char> segment, SpecSegment specification) =>
        Paths.MatchesExpression(
            segment,
            specification,
            specification.IgnoreCase ? MatchCasing.CaseInsensitive : _matchCasing,
            _directoryMatchType);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _specSegments.Dispose();
        }
    }
}
