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
public sealed partial class MSBuildEnumerator : FileSystemEnumerator<string>
{
    private static readonly EnumerationOptions s_defaultOptions = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    private static readonly char[] s_wildcardChars = ['*', '?'];

    private readonly string _projectDirectory;
    private readonly bool _stripProjectDirectory;
    private readonly int _projectDirectoryLength;

    private readonly MatchType _matchType;
    private readonly MatchCasing _matchCasing;

    private readonly MSBuildSpec _spec;

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

        _spec = new MSBuildSpec(fullPathSpec, startDirectory, _matchType, _matchCasing);
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="MSBuildEnumerator"/> class.
    /// </summary>
    /// <param name="projectDirectory">
    ///  The project directory. Returns paths relative to this directory.
    /// </param>
    /// <param name="fileSpec">
    ///  The specification of files to enumerate, which can include wildcards.
    /// </param>
    public static MSBuildEnumerator Create(string fileSpec, string? projectDirectory, EnumerationOptions? options = null)
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
        _spec.InvalidateCache();

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
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        _spec.ShouldRecurseIntoDirectory(entry.Directory, entry.FileName);

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        !entry.IsDirectory && _spec.ShouldIncludeFile(entry.Directory, entry.FileName);
}
