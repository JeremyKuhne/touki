// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Represents a specification for matching files and directories in an MSBuild project.
/// </summary>
public class MSBuildSpec
{
    private readonly StringSegment _directorySpec;
    private readonly StringSegment _fileNameSpec;
    private readonly int _startDirectoryLength;
    private readonly MatchType _matchType;
    private readonly MatchCasing _matchCasing;

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

    private readonly List<SpecSegment> _specSegments = [];

    /// <summary>
    ///  Constructs a new <see cref="MSBuildSpec"/> from a full path specification, start directory, match type, and match casing.
    /// </summary>
    /// <param name="fullPathSpec">
    ///  The full path specification to match against, which can include wildcards. Must be normalized for the current
    ///  platform (e.g., using <see cref="Path.GetFullPath(string)"/>).
    /// </param>
    /// <param name="startDirectory">
    ///  The directory within the <paramref name="fullPathSpec"/> to start matching from. This should be up to the point
    ///  where the specification starts to contain wildcards.
    /// </param>
    /// <param name="matchType">The type of matching to use for the specification.</param>
    /// <param name="matchCasing">The case sensitivity to use.</param>
    public MSBuildSpec(string fullPathSpec, string startDirectory, MatchType matchType, MatchCasing matchCasing)
    {
        _startDirectoryLength = startDirectory.Length;
        _matchType = matchType;
        _matchCasing = matchCasing;

        // Parse pattern segments
        _directorySpec = new(fullPathSpec, _startDirectoryLength + 1);
        StringSegment remainingSpec = _directorySpec;

        while (true)
        {
            int nextSeparator = remainingSpec.IndexOf(Path.DirectorySeparatorChar);
            if (nextSeparator < 0)
            {
                _fileNameSpec = remainingSpec;
                break;
            }

            StringSegment segment = remainingSpec[..nextSeparator];
            if (_specSegments.Count == 0 || !_specSegments[^1].IsAnyDirectory || !segment.Equals("**"))
            {
                _specSegments.Add(new(segment));
            }

            remainingSpec = remainingSpec[(nextSeparator + 1)..];
        }

        if (_fileNameSpec.Equals("**"))
        {
            _fileNameSpec = "*";
            if (_specSegments.Count == 0 || !_specSegments[^1].IsAnyDirectory)
            {
                _specSegments.Add(new("**"));
            }
        }

        if (_specSegments.Count > 0 && _specSegments[^1].IsAnyDirectory)
        {
            // Spec ends with "**"; always recurse only when it's the sole segment
            AlwaysRecurse = _specSegments.Count == 1;
            EndsInAnyDirectory = true;
        }
    }

    /// <summary>
    ///  Invalidates the cache. This should be called whenever the current directory being
    ///  processed changes.
    /// </summary>
    public void InvalidateCache()
    {
        // Invalidate the cache when we finish processing a directory
        _cacheValid = false;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the specified <paramref name="directoryName"/> should be recursed into from
    ///  the specified <paramref name="currentDirectory"/>.
    /// </summary>
    public bool ShouldRecurseIntoDirectory(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> directoryName)
    {
        if (AlwaysRecurse)
        {
            // Optimized case for "**/*.cs"
            return true;
        }

        if (_specSegments.Count == 0)
        {
            // No directory segments to match.
            return false;
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
            return true;
        }

        // Check if recursing into this directory would match the pattern
        // Combine relative path and entry filename into a VirtualPath to avoid string allocation

        VirtualPath virtualPath = new(relativePath, directoryName);
        return MatchDirectorySegments(ref virtualPath);
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the specified <paramref name="fileName"/> should be included in the results.
    /// </summary>
    public bool ShouldIncludeFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        // Get the relative path from start directory to this file's directory
        ReadOnlySpan<char> relativePath = GetRelativeDirectoryPath(currentDirectory);

        if (!_cacheValid)
        {
            UpdateCachedMatchState(relativePath);
        }

        // Check if the current directory fully matches the pattern and the file name matches
        // Use cached result since we'll be called multiple times for files in the same directory
        return _cachedFullyMatches && MatchSpec(fileName, _fileNameSpec);
    }

    private void UpdateCachedMatchState(ReadOnlySpan<char> relativePath)
    {
        if (_specSegments.Count == 0)
        {
            _cachedFullyMatches = AlwaysRecurse || relativePath.IsEmpty;
        }
        else
        {
            VirtualPath virtualPath = new(relativePath);
            MatchSegments(ref virtualPath, out _cachedFullyMatches);
        }

        _cacheValid = true;
    }

    private ReadOnlySpan<char> GetRelativeDirectoryPath(ReadOnlySpan<char> fullDirectory) =>
        // Remove the start directory prefix to get the relative path
        fullDirectory.Length <= _startDirectoryLength ? default : fullDirectory[(_startDirectoryLength + 1)..];

    private bool MatchDirectorySegments(ref VirtualPath virtualPath)
    {
        if (virtualPath.Length == 0)
        {
            // No relative directory path, match if there are no spec segments or if the first segment is "**".
            return _specSegments.Count == 0 || _specSegments[0].IsAnyDirectory;
        }

        return MatchSegments(ref virtualPath, out _);
    }

    private bool MatchSegments(ref VirtualPath virtualPath, out bool fullMatch)
    {
        int specIndex = 0;

        while (specIndex < _specSegments.Count && virtualPath.MoveNextSegment())
        {
            SpecSegment currentSpec = _specSegments[specIndex];

            if (currentSpec.IsAnyDirectory)
            {
                // Current is match any, need to greedily look forward for a valid match for the next (not "**") spec.
                specIndex++;

                if (specIndex >= _specSegments.Count)
                {
                    // Already at the end.
                    break;
                }

                SpecSegment nextSpec = _specSegments[specIndex];

                bool matched = false;
                do
                {
                    if (MatchSpec(virtualPath.CurrentSegment, nextSpec))
                    {
                        // Found one, match it and continue.
                        matched = true;
                        specIndex++;
                        break;
                    }
                } while (virtualPath.MoveNextSegment());

                if (!matched)
                {
                    // No match found for the next non "**" spec, which means we can't fully match this path.
                    // If we are at the end of the path, we can still consider it a partial match.
                    fullMatch = false;
                    return virtualPath.End;
                }
            }
            else
            {
                // Regular match, not "**".
                if (!MatchSpec(virtualPath.CurrentSegment, currentSpec))
                {
                    fullMatch = false;
                    return false;
                }

                specIndex++;
            }
        }

        // We've successfully matched all segments that we could.

        bool specsRemaining = specIndex < _specSegments.Count;
        bool pathRemaining = !virtualPath.End;

        // We're fully matched if the spec ends in "**" and we've matched all segments, otherwise
        // we need to have matched both the path and spec segments completely.
        fullMatch = EndsInAnyDirectory
            ? specIndex >= _specSegments.Count - 1
            : !pathRemaining && !specsRemaining;

        // We have a partial, *possible* full match when we've matched all of the path segments.
        return !pathRemaining;
    }

    private bool MatchSpec(ReadOnlySpan<char> name, ReadOnlySpan<char> spec) => _matchType switch
    {
        MatchType.Win32 => FileSystemName.MatchesWin32Expression(
            spec,
            name,
            ignoreCase: _matchCasing == MatchCasing.CaseInsensitive),
        _ => FileSystemName.MatchesSimpleExpression(
            spec,
            name,
            ignoreCase: _matchCasing == MatchCasing.CaseInsensitive)
    };

    [DebuggerDisplay("{Spec}")]
    private readonly struct SpecSegment
    {
        public StringSegment Spec { get; }
        public bool IsAnyDirectory { get; }

        /// <summary>
        ///  Implicitly converts a <see cref="SpecSegment"/> to a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
        /// </summary>
        /// <param name="segment">The segment to convert.</param>
        public static implicit operator ReadOnlySpan<char>(SpecSegment segment) => segment.Spec;

        public SpecSegment(StringSegment spec)
        {
            Spec = spec;
            IsAnyDirectory = spec.Equals("**");
        }
    }
}
