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
public abstract partial class MSBuildEnumerator : FileSystemEnumerator<string>
{
    /// <summary>
    ///  Default options for the enumerator.
    /// </summary>
    protected static EnumerationOptions DefaultOptions { get; } = new()
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

    /// <summary>
    ///  Initializes a new instance of the <see cref="MSBuildEnumerator"/> class.
    /// </summary>
    private MSBuildEnumerator(
        string? projectDirectory,
        bool stripProjectDirectory,
        string startDirectory,
        EnumerationOptions options)
        : base(startDirectory, options)
    {
        // Initialize project directory settings
        if (projectDirectory is null || !stripProjectDirectory)
        {
            _stripProjectDirectory = false;
            _projectDirectory = string.Empty;
            _projectDirectoryLength = 0;
        }
        else
        {
            _stripProjectDirectory = true;
            _projectDirectory = projectDirectory;
            _projectDirectoryLength = projectDirectory.Length +
                (Path.EndsInDirectorySeparator(_projectDirectory) ? 0 : 1);
        }
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

        options ??= DefaultOptions;

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

        return new SingleSpec(
            new MSBuildSpec(fullPathSpec, startDirectory, options.MatchType, options.MatchCasing),
            projectDirectory,
            !Path.IsPathFullyQualified(fileSpec),
            startDirectory,
            options);
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
}
