// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using Touki.Text;

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
public class MSBuildEnumerator : MatchEnumerator<string>
{
    /// <summary>
    ///  Default options for the enumerator.
    /// </summary>
    private static EnumerationOptions DefaultOptions { get; } = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
        RecurseSubdirectories = true
    };

    private readonly string _projectDirectory;
    private readonly bool _stripProjectDirectory;
    private readonly int _projectDirectoryLength;

    /// <summary>
    ///  Initializes a new instance of the <see cref="MSBuildEnumerator"/> class.
    /// </summary>
    private MSBuildEnumerator(
        IEnumerationMatcher matcher,
        string? projectDirectory,
        bool stripProjectDirectory,
        string startDirectory,
        EnumerationOptions options)
        : base(startDirectory, matcher, options)
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
    ///  Creates an <see cref="MSBuildEnumerator"/> for the given file specification.
    /// </summary>
    /// <param name="fileSpec">
    ///  The specification of files to enumerate, which can include wildcards.
    /// </param>
    /// <param name="projectDirectory">
    ///  The project directory. Returns paths relative to this directory.
    /// </param>
    /// <param name="options">
    ///  Enumeration options that control matching behavior and recursion. If <see langword="null"/>,
    ///  sensible defaults are used.
    /// </param>
    /// <returns>
    ///  An <see cref="MSBuildEnumerator"/> that yields files matching the provided specification.
    /// </returns>
    public static MSBuildEnumerator Create(string fileSpec, string? projectDirectory, EnumerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(fileSpec);

        options ??= DefaultOptions;

        MSBuildSpecification include = new(fileSpec);
        IEnumerationMatcher matcher = MSBuildMatchBuilder.FromSpecification(
            include,
            EmptyList<MSBuildSpecification>.Instance,
            options.MatchType,
            options.MatchCasing,
            projectDirectory,
            out StringSegment startDirectory);

        return new MSBuildEnumerator(
            matcher,
            projectDirectory,
            !Path.IsPathFullyQualified(fileSpec),
            startDirectory.ToString(),
            options);
    }

    /// <inheritdoc cref="Create(string, string?, EnumerationOptions?)"/>
    /// <param name="excludeSpecs">Exclude specifications.</param>
    public static MSBuildEnumerator Create(
        string fileSpec,
        string excludeSpecs,
        string? projectDirectory,
        EnumerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(fileSpec);

        options ??= DefaultOptions;
        MatchCasing matchCasing = Paths.GetFinalCasing(options.MatchCasing);

        MSBuildSpecification include = new(fileSpec);
        using var excludes = MSBuildSpecification.Split(excludeSpecs, ignoreCase: matchCasing == MatchCasing.CaseInsensitive);

        IEnumerationMatcher matcher = MSBuildMatchBuilder.FromSpecification(
            include,
            excludes,
            options.MatchType,
            options.MatchCasing,
            projectDirectory,
            out StringSegment startDirectory);

        return new MSBuildEnumerator(
            matcher,
            projectDirectory,
            stripProjectDirectory: !Path.IsPathFullyQualified(fileSpec),
            startDirectory.ToString(),
            options);
    }

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        !entry.IsDirectory && base.ShouldIncludeEntry(ref entry);

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
