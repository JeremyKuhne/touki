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
    private readonly bool _specEndsInAnyDirectory;
    private readonly StringSegment _directorySpec;
    private readonly StringSegment _fileNameSpec;
    private readonly List<SpecSegment> _specSegments = [];
    private readonly string _startDirectory;

    private readonly MatchType _matchType;
    private readonly MatchCasing _matchCasing;

    // Cache for current directory being processed - valid until OnDirectoryFinished is called
    private bool _cacheValid;
    private bool _cachedFullyMatches;

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

        if (_specSegments.Count > 0 && _specSegments[^1].IsAnyDirectory)
        {
            // Spec ends with "**"; always recurse only when it's the sole segment
            _alwaysRecurse = _specSegments.Count == 1;
            _specEndsInAnyDirectory = true;
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

        if (!_cacheValid)
        {
            UpdateCachedMatchState(relativePath);
        }

        if (_cachedFullyMatches && _specEndsInAnyDirectory)
        {
            // If the current directory fully matches the pattern and it ends with "**", we should always recurse.
            return true;
        }

        // Check if recursing into this directory would match the pattern
        // Combine relative path and entry filename into a VirtualPath to avoid string allocation

        VirtualPath virtualPath = new(relativePath, entry.FileName);
        return MatchDirectorySegments(ref virtualPath);
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

        if (!_cacheValid)
        {
            UpdateCachedMatchState(relativePath);
        }

        // Check if the current directory fully matches the pattern and the file name matches
        // Use cached result since we'll be called multiple times for files in the same directory
        return _cachedFullyMatches && MatchSpec(entry.FileName, _fileNameSpec);
    }

    private void UpdateCachedMatchState(ReadOnlySpan<char> relativePath)
    {
        if (_specSegments.Count == 0)
        {
            _cachedFullyMatches = _alwaysRecurse || relativePath.IsEmpty;
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
        fullDirectory.Length <= _startDirectory.Length ? default : fullDirectory[(_startDirectory.Length + 1)..];

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
        fullMatch = _specEndsInAnyDirectory
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
}
