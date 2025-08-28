// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

namespace Touki.Io;

/// <summary>
///  Represents a specification for matching files and directories in an MSBuild project.
/// </summary>
public class MatchMSBuild : DisposableBase, IEnumerationMatcher
{
    private readonly MSBuildSpecification _spec;
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

    private readonly ContiguousList<SpecSegment> _specSegments;

    /// <summary>
    ///  Constructs a new <see cref="MatchMSBuild"/> from a full path specification, start directory, match type, and match casing.
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
    public MatchMSBuild(string fullPathSpec, string startDirectory, MatchType matchType, MatchCasing matchCasing)
        : this(new MSBuildSpecification(fullPathSpec), startDirectory, matchType, matchCasing)
    {
    }

    /// <summary>
    ///  Constructs a new <see cref="MatchMSBuild"/> from a parsed <see cref="MSBuildSpecification"/>, start directory, match type, and match casing.
    /// </summary>
    /// <param name="spec">The parsed specification.</param>
    /// <param name="startDirectory">The directory within the <paramref name="spec"/> to start matching from.</param>
    /// <param name="matchType">The type of matching to use for the specification.</param>
    /// <param name="matchCasing">The case sensitivity to use.</param>
    public MatchMSBuild(MSBuildSpecification spec, string startDirectory, MatchType matchType, MatchCasing matchCasing)
    {
        _spec = spec;
        _startDirectoryLength = startDirectory.Length;
        _matchType = matchType;
        _matchCasing = Paths.GetFinalCasing(matchCasing);

        // Build directory spec segments from the spec's WildPath
        if (!_spec.WildPath.IsEmpty)
        {
            PathSegmentEnumerator enumerator = new(_spec.WildPath);
            while (enumerator.MoveNext())
            {
                StringSegment segment = new(enumerator.Current.ToString());
                _specSegments ??= new SingleOptimizedList<SpecSegment>();
                if (_specSegments.Count == 0 || !segment.Equals("**") || !_specSegments[^1].IsAnyDirectory)
                {
                    _specSegments.Add(new(segment));
                }
            }
        }

        _specSegments ??= EmptyList<SpecSegment>.Instance;

        // Determine recursion flags from the parsed spec
        AlwaysRecurse = _spec.IsSimpleRecursiveMatch;

        bool endsInAny = false;
        if (_specSegments.Count > 0)
        {
            endsInAny = _specSegments[^1].IsAnyDirectory;
        }
        EndsInAnyDirectory = endsInAny;
    }

    /// <inheritdoc/>
    public void DirectoryFinished()
    {
        // Invalidate the cache when we finish processing a directory
        _cacheValid = false;
    }

    /// <inheritdoc/>
    public bool MatchesDirectory(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> directoryName)
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

        // Validate that the current directory is at or under the fixed path portion of the spec
        bool ignoreCase = _matchCasing == MatchCasing.CaseInsensitive;
        if (!_spec.FixedPath.IsEmpty && !Paths.IsSameOrSubdirectory(_spec.FixedPath, currentDirectory, ignoreCase))
        {
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

        PathSegmentEnumerator virtualPath = new(relativePath, directoryName);

        if (virtualPath.Length == 0)
        {
            // No relative directory path, recurse if there are no spec segments or if the first segment is "**".
            return _specSegments.Count == 0 || _specSegments[0].IsAnyDirectory;
        }

        // Should recurse if we've fully or partially matched.
        return MatchSegments(ref virtualPath) != PathMatchState.NoMatch;
    }

    /// <inheritdoc/>
    public bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName)
    {
        // Validate that the current directory is at or under the fixed path portion of the spec
        bool ignoreCase = _matchCasing == MatchCasing.CaseInsensitive;
        if (!_spec.FixedPath.IsEmpty && !Paths.IsSameOrSubdirectory(_spec.FixedPath, currentDirectory, ignoreCase))
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
        return _cachedFullyMatches && Paths.MatchesExpression(fileName, _spec.FileName, _matchCasing, _matchType);
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
        fullDirectory.Length <= _startDirectoryLength ? default : fullDirectory[(_startDirectoryLength + 1)..];

    private enum PathMatchState
    {
        /// <summary>
        ///  The path does not match the specification, and there is no possibility of a match in a subdirectory.
        /// </summary>
        NoMatch,

        /// <summary>
        ///  The path partially matches the specification, meaning it could match in a subdirectory, but files within
        ///  the specified path do not match.
        /// </summary>
        PartialMatch,

        /// <summary>
        ///  The path fully matches the specification, meaning it matches all segments and files within the
        ///  specified path.
        /// </summary>
        FullMatch
    }

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
        int specIndex = 0;

        while (specIndex < _specSegments.Count && pathSegments.MoveNext())
        {
            SpecSegment currentSpec = _specSegments[specIndex];

            if (!currentSpec.IsAnyDirectory)
            {
                // Regular match, not "**". We have to exactly match to the current segment.
                if (!Paths.MatchesExpression(pathSegments.Current, currentSpec, _matchCasing, _matchType))
                {
                    return PathMatchState.NoMatch;
                }

                specIndex++;
                continue;
            }

            // Current is match any ("**"), need to greedily look forward for a valid match for the next (not "**") spec.
            // For example, if the specification is "a/**/b/c.txt", we need to move to the "b" segment to attempt to
            // *fully* satisfy the specification.
            if (++specIndex == _specSegments.Count)
            {
                // Already at the end.
                break;
            }

            currentSpec = _specSegments[specIndex];
            Debug.Assert(!currentSpec.IsAnyDirectory, "Duplicate ** should have been filtered out.");

            bool matched = false;
            do
            {
                if (Paths.MatchesExpression(pathSegments.Current, currentSpec, _matchCasing, _matchType))
                {
                    // Found a match in the path for the current specification segment, need to continue to the next
                    // specification segment.
                    matched = true;
                    specIndex++;
                    break;
                }
            } while (pathSegments.MoveNext());

            if (!matched)
            {
                // No match found for the next non "**" spec, which means we can't *fully* match this path. We might
                // be able to fully match on a subdirectory of the current path. With the prior example of "a/**/b/c.txt",
                // if the current directory is "a/c/d", we don't have a match for the current directory, but we will
                // be able to match "a/c/d/b/c.txt" if we recurse into "a/c/d/b".
                return PathMatchState.PartialMatch;
            }
        }

        // We've successfully matched all segments that we could. We have a few states at this point.
        //
        //  Matched Spec | Matched Path |                 |
        //  Segments     | Segments     | Partial Match   | Full Match
        //  -------------|--------------|-----------------|----------------
        //  All          | All          | true            | true
        //  All          | Some         | spec ends in ** | spec ends in **
        //  Some         | All          | true            | false
        //  Some         | Some         | false           | false
        //
        // "All" for matched spec segments means everything up to a final "**" segment, if any.

        bool specsRemaining = specIndex < _specSegments.Count;
        bool pathRemaining = !pathSegments.End;

        // We're fully matched if the specification ends in "**" and we've matched all other specification segments
        // (as *anything* matches from that point). If it doesn't end in "**", then we need to have matched all path
        // segments to all specification segments.
        bool fullMatch = EndsInAnyDirectory
            ? specIndex >= _specSegments.Count - 1
            : !pathRemaining && !specsRemaining;

        return fullMatch
            ? PathMatchState.FullMatch
            : pathRemaining
                ? PathMatchState.NoMatch
                : PathMatchState.PartialMatch;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _specSegments?.Dispose();
        }
    }

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

        public override string ToString() => Spec.ToString();
    }
}
