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

        // Initialize project directory settings
        if (projectDirectory is null)
        {
            _stripProjectDirectory = false;
            _projectDirectory = string.Empty;
            _projectDirectoryLength = 0;
        }
        else
        {
            _projectDirectory = projectDirectory;
            _projectDirectoryLength = projectDirectory.Length +
                (Path.EndsInDirectorySeparator(_projectDirectory) ? 0 : 1);
            _stripProjectDirectory = !Path.IsPathFullyQualified(fileSpec);
        }

        // Initialize matching options
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

        // Parse pattern segments
        _directorySpec = new(fullPathSpec, startDirectory.Length + 1);
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

        if (_specSegments.Count == 1 && _specSegments[0].IsAnyDirectory)
        {
            // The only spec is "**"
            _alwaysRecurse = true;
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
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) =>
        // Clear the cache when we finish processing a directory
        _cacheValid = false;

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

        if (_specSegments.Count == 0)
        {
            // No directory segments to match.
            return false;
        }

        // Get the relative path from start directory to this directory
        ReadOnlySpan<char> relativePath = GetRelativeDirectoryPath(entry.Directory);

        // Check if recursing into this directory would match the pattern
        // Pass the entry filename as the final segment to avoid string allocation

        return MatchDirectorySegments(relativePath, entry.FileName, out _);
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

    private bool MatchDirectorySegments(ReadOnlySpan<char> relativePath, ReadOnlySpan<char> finalSegment, out bool fullyMatches)
    {
        if (relativePath.IsEmpty)
        {
            if (!finalSegment.IsEmpty)
            {
                bool canRecurse = _specSegments.Count > 0 &&
                    (_specSegments[0].IsAnyDirectory || MatchSpec(finalSegment, _specSegments[0].Spec.AsSpan()));

                fullyMatches = false;
                return canRecurse;
            }

            if (_specSegments.Count == 0)
            {
                fullyMatches = true;
                return true;
            }

            if (_specSegments[0].IsAnyDirectory)
            {
                fullyMatches = _specSegments.Count == 1;
                return true;
            }

            fullyMatches = false;
            return false;
        }

        bool exactMatch = MatchSegments(relativePath, finalSegment, out fullyMatches);

        if (!exactMatch && !finalSegment.IsEmpty && _specSegments.Count > 0 && _specSegments[0].IsAnyDirectory)
        {
            fullyMatches = false;
            return true;
        }

        return exactMatch;
    }

    private bool MatchSegments(ReadOnlySpan<char> relativePath, ReadOnlySpan<char> finalPathSegment, out bool fullyMatches)
    {
        SpanReader<char> reader = new(relativePath);
        int specIndex = 0;

        while (specIndex < _specSegments.Count && !reader.End)
        {
            Segment currentSpec = _specSegments[specIndex];

            if (currentSpec.IsAnyDirectory)
            {
                specIndex++;

                if (specIndex >= _specSegments.Count)
                {
                    break;
                }

                Segment nextSpec = _specSegments[specIndex];
                if (!FindMatchingSegment(ref reader, nextSpec, ref finalPathSegment))
                {
                    fullyMatches = false;
                    return false;
                }

                specIndex++;
            }
            else
            {
                if (!reader.TrySplit(Path.DirectorySeparatorChar, out ReadOnlySpan<char> segment)
                    || !MatchSpec(segment, currentSpec.Spec.AsSpan()))
                {
                    fullyMatches = false;
                    return false;
                }

                specIndex++;
            }
        }

        if (!finalPathSegment.IsEmpty && specIndex < _specSegments.Count)
        {
            if (!ProcessFinalSegment(finalPathSegment, specIndex, ref specIndex))
            {
                fullyMatches = false;
                return false;
            }

            finalPathSegment = default;
        }

        bool hasRemainingSpecs = specIndex < _specSegments.Count;
        bool allConsumed = (!hasRemainingSpecs || reader.End) && finalPathSegment.IsEmpty;

        fullyMatches = allConsumed && (!hasRemainingSpecs ||
            (_specSegments[specIndex].IsAnyDirectory && specIndex == _specSegments.Count - 1));

        return allConsumed;
    }

    private bool FindMatchingSegment(ref SpanReader<char> reader, Segment nextSpec, ref ReadOnlySpan<char> finalPathSegment)
    {
        while (reader.TrySplit(Path.DirectorySeparatorChar, out ReadOnlySpan<char> segment))
        {
            if (MatchSpec(segment, nextSpec.Spec.AsSpan()))
            {
                return true;
            }
        }

        if (!finalPathSegment.IsEmpty && MatchSpec(finalPathSegment, nextSpec.Spec.AsSpan()))
        {
            finalPathSegment = default;
            return true;
        }

        return false;
    }

    private bool ProcessFinalSegment(ReadOnlySpan<char> finalPathSegment, int specIndex, ref int newSpecIndex)
    {
        Segment currentSpec = _specSegments[specIndex];

        if (currentSpec.IsAnyDirectory)
        {
            newSpecIndex = specIndex + 1;
            if (newSpecIndex < _specSegments.Count)
            {
                return MatchSpec(finalPathSegment, _specSegments[newSpecIndex].Spec.AsSpan()) &&
                       (++newSpecIndex == _specSegments.Count);
            }
        }
        else
        {
            newSpecIndex = specIndex + 1;
            return MatchSpec(finalPathSegment, currentSpec.Spec.AsSpan());
        }

        return true;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (bool CanMatch, bool FullyMatches) GetCachedDirectoryMatchState(ReadOnlySpan<char> relativePath)
    {
        if (_cacheValid)
        {
            return (_cachedCanMatch, _cachedFullyMatches);
        }

        bool canMatch;
        bool fullyMatches;

        if (_specSegments.Count == 0)
        {
            fullyMatches = _alwaysRecurse || relativePath.IsEmpty;
            canMatch = true;
        }
        else
        {
            canMatch = MatchDirectorySegments(relativePath, default, out fullyMatches);
        }

        _cacheValid = true;
        _cachedCanMatch = canMatch;
        _cachedFullyMatches = fullyMatches;

        return (canMatch, fullyMatches);
    }
}
