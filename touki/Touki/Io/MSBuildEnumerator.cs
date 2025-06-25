// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Enumerates files that match a glob pattern with zero allocations until matches are found.
/// </summary>
/// <remarks>
///  <para>The following wildcard patterns are supported:
///   <list type="table">
///    <listheader>
///     <term>Pattern</term>
///     <description>Description</description>
///    </listheader>
///    <item>
///     <term>*</term>
///     <description>Matches zero or more characters within a file or directory name</description>
///    </item>
///    <item>
///     <term>**</term>
///     <description>Matches zero or more directories (recursive wildcard)</description>
///    </item>
///    <item>
///     <term>?</term>
///     <description>Matches a single character</description>
///    </item>
///   </list>
///  </para>
/// </remarks>
public sealed class MSBuildEnumerator : FileSystemEnumerator<string>
{
    private static readonly EnumerationOptions s_defaultOptions = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        RecurseSubdirectories = true
    };

    private static readonly char[] s_wildcardChars = ['*', '?'];

    private readonly string _projectDirectory;
    private readonly bool _stripProjectDirectory;
    private readonly int _projectDirectoryLength;

    private readonly bool _alwaysRecurse;
    private readonly StringSegment _directorySpec;
    private readonly StringSegment _fileNameSpec;
    private readonly List<Segment> _specSegments = [];
    private readonly string _startDirectory;

    private readonly MatchType _matchType;
    private readonly MatchCasing _matchCasing;

    // Cache for current directory being processed - valid until OnDirectoryFinished is called
    private bool _cacheValid;
    private bool _cachedCanMatch;
    private bool _cachedFullyMatches;

    private readonly struct Segment
    {
        public StringSegment Spec { get; }
        public bool IsAnyDirectory { get; }

        public Segment(StringSegment spec)
        {
            Spec = spec;
            IsAnyDirectory = spec.Equals("**");
        }

        public static implicit operator ReadOnlySpan<char>(Segment segment) => segment.Spec.AsSpan();
        public static implicit operator Segment(StringSegment segment) => new(segment);
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="MSBuildEnumerator"/> class.
    /// </summary>
    private MSBuildEnumerator(
        string fileSpec,
        string fullPathSpec,
        string? projectDirectory,
        string startDirectory,
        EnumerationOptions? options = null)
        : base(startDirectory, options ?? s_defaultOptions)
    {
        _startDirectory = startDirectory;

        if (projectDirectory is null)
        {
            _stripProjectDirectory = false;
            _projectDirectory = string.Empty;
            _projectDirectoryLength = 0;
        }
        else
        {
            _projectDirectory = projectDirectory;
            _projectDirectoryLength = projectDirectory.Length;
            if (!Path.EndsInDirectorySeparator(_projectDirectory))
            {
                _projectDirectoryLength += 1;
            }

            // In MSBuild, this uses Path.IsRooted, which is not technically correct.
            // There is also a case if the fileSpec is something like "**\C:\foo\bar\baz.cs"
            // where it would not be stripped, but that doesn't seem to be by design.
            _stripProjectDirectory = !Path.IsPathFullyQualified(fileSpec);
        }

        _matchType = options?.MatchType ?? MatchType.Simple;
        _matchCasing = options?.MatchCasing ?? MatchCasing.PlatformDefault;

        if (_matchCasing == MatchCasing.PlatformDefault)
        {
#if NETFRAMEWORK
            _matchCasing = MatchCasing.CaseInsensitive;
#else
            _matchCasing = OperatingSystem.IsWindows()
                || OperatingSystem.IsMacOS()
                || OperatingSystem.IsIOS()
                || OperatingSystem.IsTvOS()
                || OperatingSystem.IsWatchOS()
                    ? MatchCasing.CaseInsensitive
                    : MatchCasing.CaseSensitive;
#endif
        }

        Debug.Assert(!Path.EndsInDirectorySeparator(startDirectory));

        // Don't want to include the trailing separator in the start of the file spec.
        _directorySpec = new(fullPathSpec, startDirectory.Length + 1);

        StringSegment remainingDirectorySpec = _directorySpec;

        // Split the remaining directory spec on the directory separator char and put the segments into _specSegments.

        int nextSeparator;
        while (true)
        {
            nextSeparator = remainingDirectorySpec.IndexOf(Path.DirectorySeparatorChar);
            if (nextSeparator < 0)
            {
                _fileNameSpec = remainingDirectorySpec;
                break;
            }

            StringSegment segment = remainingDirectorySpec[..nextSeparator];
            int currentCount = _specSegments.Count;
            if (currentCount == 0 || !_specSegments[currentCount - 1].Equals("**"))
            {
                // If the last segment was not a "**" or this is the first segment, we can just add it.
                _specSegments.Add(segment);
            }

            remainingDirectorySpec = remainingDirectorySpec[(nextSeparator + 1)..];
        }

        if (_specSegments.Count == 0)
        {
            if (_fileNameSpec.Equals("**"))
            {
                // Special case of "**" as the file spec, which means we match everything recursively.
                _alwaysRecurse = true;
                _fileNameSpec = "*";
            }

            return;
        }

        if (_specSegments.Count == 1 && _specSegments[0].IsAnyDirectory)
        {
            // If the only segment is "**", we should simply match anything recursively. Optimized case for "**/*.cs"
            _alwaysRecurse = true;
        }

        if (_fileNameSpec.Equals("**"))
        {
            _fileNameSpec = "*";
            if (!_specSegments[^1].IsAnyDirectory)
            {
                _specSegments.Add(new("**"));
            }
        }
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="MSBuildEnumerator"/> class.
    /// </summary>
    public static MSBuildEnumerator Create(string? projectDirectory, string fileSpec, EnumerationOptions? options = null)
    {
        ArgumentNull.ThrowIfNull(fileSpec);

        // Ensure we're fully normalized
        string rootDirectory = projectDirectory is null
            ? Path.GetFullPath(Environment.CurrentDirectory)
            : Path.GetFullPath(projectDirectory);

        string fullPathSpec = Path.GetFullPath(fileSpec, rootDirectory);

        ReadOnlySpan<char> fullPath = fullPathSpec.AsSpan();
        int firstWildcard = fullPath.IndexOfAny(s_wildcardChars);
        if (firstWildcard > 0)
        {
            fullPath = fullPath[..firstWildcard];
        }

        int lastSeparator = fullPath.LastIndexOf(Path.DirectorySeparatorChar);
        if (lastSeparator < 0)
        {
            throw new ArgumentException("Did not resolve to a full path.", nameof(fileSpec));
        }

        string startDirectory = fullPath[..lastSeparator].ToString();

        return new MSBuildEnumerator(fileSpec, fullPathSpec, projectDirectory, startDirectory, options);
    }

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory)
    {
        // Clear the cache when we finish processing a directory
        _cacheValid = false;
    }

    /// <inheritdoc/>
    protected override string TransformEntry(ref FileSystemEntry entry)
    {
        if (!_stripProjectDirectory)
        {
            // If we're not stripping the project directory, we can just return the full path.
            return entry.ToFullPath();
        }

        if (entry.Directory.Length <= _projectDirectoryLength)
        {
            // If the entry is in the base directory, we can just return the file name.
            return entry.FileName.ToString();
        }

        return $"{entry.Directory[_projectDirectoryLength..]}{Path.DirectorySeparatorChar}{entry.FileName}";
    }

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry)
    {
        if (_alwaysRecurse)
        {
            // Optimized case for "**/*.cs"
            return true;
        }

        // Get the relative path from start directory to this directory
        ReadOnlySpan<char> relativePath = GetRelativeDirectoryPath(entry.Directory);

        // Check if recursing into this directory would match the pattern
        // Pass the entry filename as the final segment to avoid string allocation
        return DoesDirectoryPathMatch(relativePath, entry.FileName, out _);
    }

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry)
    {
        if (entry.IsDirectory)
        {
            return false;
        }

        // Get the relative path from start directory to this file's directory
        ReadOnlySpan<char> relativePath = GetRelativeDirectoryPath(entry.Directory);

        // Check if the current directory fully matches the pattern and the file name matches
        // Use cached result since we'll be called multiple times for files in the same directory
        return GetCachedDirectoryMatchState(relativePath).FullyMatches && MatchSpec(entry.FileName, _fileNameSpec);
    }

    private ReadOnlySpan<char> GetRelativeDirectoryPath(ReadOnlySpan<char> fullDirectory) =>
        // Remove the start directory prefix to get the relative path
        fullDirectory.Length <= _startDirectory.Length ? default : fullDirectory[(_startDirectory.Length + 1)..];

    private bool DoesDirectoryPathMatch(ReadOnlySpan<char> relativePath, out bool fullyMatches)
        => DoesDirectoryPathMatch(relativePath, default, out fullyMatches);

    private bool DoesDirectoryPathMatch(ReadOnlySpan<char> relativePath, ReadOnlySpan<char> finalSegment, out bool fullyMatches)
    {
        if (_specSegments.Count == 0)
        {
            // No directory segments to match - this means the pattern was just a filename or "**"
            // If _alwaysRecurse is true (pattern was "**"), match all directories
            // Otherwise, only match if we're at the root
            fullyMatches = _alwaysRecurse || relativePath.IsEmpty;
            return true; // Always can match when no directory segments
        }

        return MatchDirectorySegments(relativePath, finalSegment, out fullyMatches);
    }

    private bool MatchDirectorySegments(ReadOnlySpan<char> relativePath, ReadOnlySpan<char> finalSegment, out bool fullyMatches)
    {
        // If no relative path, we're at the start directory
        if (relativePath.IsEmpty)
        {
            // If we have a final segment, we need to consider it for matching
            if (!finalSegment.IsEmpty)
            {
                return MatchSegments(finalSegment, default, out fullyMatches);
            }

            // We match if we have no segments or if the first segment is "**"
            bool canMatch = _specSegments.Count == 0 || _specSegments[0].IsAnyDirectory;
            fullyMatches = canMatch; // At root, can match and fully matches are the same
            return canMatch;
        }

        return MatchSegments(relativePath, finalSegment, out fullyMatches);
    }

    private bool MatchSegments(ReadOnlySpan<char> relativePath, ReadOnlySpan<char> finalSegment, out bool fullyMatches)
    {
        SpanReader<char> reader = new(relativePath);

        int specIndex = 0;

        while (specIndex < _specSegments.Count && !reader.End)
        {
            Segment currentSpec = _specSegments[specIndex];

            if (currentSpec.IsAnyDirectory)
            {
                // "**" matches zero or more directory segments
                specIndex++;

                if (specIndex >= _specSegments.Count)
                {
                    // "**" is the last segment, it matches everything remaining
                    fullyMatches = true;
                    return true;
                }

                // Try to match the next spec segment with any remaining path segment
                Segment nextSpec = _specSegments[specIndex];
                bool foundMatch = false;

                // Try matching from each remaining segment position
                while (!reader.End)
                {
                    if (reader.TrySplit(Path.DirectorySeparatorChar, out ReadOnlySpan<char> segment))
                    {
                        if (MatchSpec(segment, nextSpec.Spec.AsSpan()))
                        {
                            specIndex++;
                            foundMatch = true;
                            break;
                        }
                    }
                    else
                    {
                        // No more separators, check the remaining unread data
                        if (MatchSpec(reader.Unread, nextSpec.Spec.AsSpan()))
                        {
                            reader.Advance(reader.Unread.Length);
                            specIndex++;
                            foundMatch = true;
                        }

                        break;
                    }
                }

                if (!foundMatch)
                {
                    fullyMatches = false;
                    return false;
                }
            }
            else
            {
                // Regular segment, must match exactly
                if (!reader.TrySplit(Path.DirectorySeparatorChar, out ReadOnlySpan<char> segment))
                {
                    // No more separators, check the remaining unread data
                    segment = reader.Unread;
                    reader.Advance(segment.Length);
                }

                if (!MatchSpec(segment, currentSpec.Spec.AsSpan()))
                {
                    fullyMatches = false;
                    return false;
                }

                specIndex++;
            }
        }

        // If we have a final segment to process, handle it now
        if (!finalSegment.IsEmpty && specIndex < _specSegments.Count)
        {
            Segment currentSpec = _specSegments[specIndex];

            if (currentSpec.IsAnyDirectory)
            {
                // "**" matches the final segment
                specIndex++;

                // If there are more spec segments after "**", try to match them with the final segment
                if (specIndex < _specSegments.Count)
                {
                    Segment nextSpec = _specSegments[specIndex];
                    if (MatchSpec(finalSegment, nextSpec.Spec.AsSpan()))
                    {
                        specIndex++;
                    }
                    else
                    {
                        fullyMatches = false;
                        return false;
                    }
                }
            }
            else
            {
                // Regular segment, must match the final segment
                if (!MatchSpec(finalSegment, currentSpec.Spec.AsSpan()))
                {
                    fullyMatches = false;
                    return false;
                }

                specIndex++;
            }
        }

        // Check if we've matched appropriately
        // For partial match (recursion), we succeed if:
        // 1. We've consumed all path segments and haven't exceeded spec segments, OR
        // 2. The remaining spec segments start with "**"
        bool hasUnprocessedFinalSegment = !finalSegment.IsEmpty && specIndex < _specSegments.Count;
        bool canMatch = reader.End
            && !hasUnprocessedFinalSegment
            && specIndex <= _specSegments.Count
            && (specIndex == _specSegments.Count || _specSegments[specIndex].IsAnyDirectory);

        // For full match (file inclusion), we need to have consumed all spec segments
        // or the remaining spec segments are all "**"
        int tempSpecIndex = specIndex;
        while (tempSpecIndex < _specSegments.Count && _specSegments[tempSpecIndex].IsAnyDirectory)
        {
            tempSpecIndex++;
        }

        fullyMatches = reader.End && finalSegment.IsEmpty && tempSpecIndex >= _specSegments.Count;

        return canMatch;
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

    private (bool CanMatch, bool FullyMatches) GetCachedDirectoryMatchState(ReadOnlySpan<char> relativePath)
    {
        if (_cacheValid)
        {
            return (_cachedCanMatch, _cachedFullyMatches);
        }

        bool canMatch = DoesDirectoryPathMatch(relativePath, out bool fullyMatches);

        _cacheValid = true;
        _cachedCanMatch = canMatch;
        _cachedFullyMatches = fullyMatches;

        return (canMatch, fullyMatches);
    }
}
