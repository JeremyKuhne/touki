// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

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

    /// <summary>
    ///  Builds an enumeration result for the given include / exclude specifications, mirroring the
    ///  4-tuple returned by MSBuild's internal <c>FileMatcher.GetFiles</c>.
    /// </summary>
    /// <param name="fileSpec">The include specification.</param>
    /// <param name="excludeSpecs">
    ///  Optional semicolon-separated exclude specifications. <see langword="null"/> or empty means no
    ///  excludes.
    /// </param>
    /// <param name="projectDirectory">
    ///  The project directory used to resolve a non-rooted include spec. When <see langword="null"/>,
    ///  <see cref="Environment.CurrentDirectory"/> is used to resolve the spec, but absolute paths are
    ///  returned in <see cref="MSBuildEnumerationResult.Enumerator"/> (paths are stripped to be relative
    ///  to <paramref name="projectDirectory"/> only when a non-<see langword="null"/> value is supplied
    ///  and the include spec is not fully qualified).
    /// </param>
    /// <param name="options">
    ///  Enumeration options and Touki-specific safety flags. <see langword="null"/> selects a fresh
    ///  <see cref="MSBuildEnumerationOptions"/> with default values.
    /// </param>
    /// <remarks>
    ///  <para>
    ///   On the <see cref="MSBuildSearchAction.RunSearch"/> path the caller owns the
    ///   <see cref="MSBuildEnumerationResult.Enumerator"/> and must dispose it after iteration.
    ///  </para>
    /// </remarks>
    public static MSBuildEnumerationResult CreateResult(
        string fileSpec,
        string? excludeSpecs = null,
        string? projectDirectory = null,
        MSBuildEnumerationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(fileSpec);

        options ??= new MSBuildEnumerationOptions();
        EnumerationOptions enumOptions = options.EnumerationOptions;
        string rootDirectory = projectDirectory ?? Environment.CurrentDirectory;

        // Validate the include spec against MSBuild's "legal file spec" rules before we ever try to
        // build an MSBuildSpecification. When the spec is illegal, MSBuild's FileMatcher.GetFiles
        // returns it verbatim via SearchAction.ReturnFileSpec; we mirror that with our own
        // ReturnFileSpec action and surface the validation reason via GlobFailure.
        StringSegment fileSpecSegment = new(fileSpec);
        StringSegment normalizedFileSpec = MSBuildSpecification.NormalizeAndValidate(fileSpecSegment, out string? includeError);

        if (includeError is not null)
        {
            return new MSBuildEnumerationResult(
                enumerator: null,
                action: MSBuildSearchAction.ReturnFileSpec,
                failedExcludeSpec: null,
                globFailure: $"Specification '{fileSpec}' is not a legal file spec: {includeError}");
        }

        // Parse once. The Create overloads parse a second time and FullyQualify a third time through
        // MSBuildMatchBuilder; CreateResult avoids that by driving the match builder directly with the
        // already-qualified include.
        MSBuildSpecification include = new MSBuildSpecification(fileSpecSegment, normalizedFileSpec).FullyQualify(rootDirectory);

        if (!options.AllowDriveEnumeration && include.IsDriveRootRecursion)
        {
            return new MSBuildEnumerationResult(
                enumerator: null,
                action: MSBuildSearchAction.FailBecauseDriveEnumerationIsForbidden,
                failedExcludeSpec: null,
                globFailure:
                    $"Drive enumeration is not allowed for '{fileSpec}'. Set " +
                    $"{nameof(MSBuildEnumerationOptions)}.{nameof(MSBuildEnumerationOptions.AllowDriveEnumeration)} = true to override.");
        }

        bool ignoreCase = Paths.GetFinalCasing(enumOptions.MatchCasing) == MatchCasing.CaseInsensitive;
        ListBase<MSBuildSpecification> excludes = string.IsNullOrEmpty(excludeSpecs)
            ? EmptyList<MSBuildSpecification>.Instance
            : MSBuildSpecification.Split(excludeSpecs!, ignoreCase);

        try
        {
            IEnumerationMatcher matcher = MSBuildMatchBuilder.FromSpecification(
                include,
                excludes,
                enumOptions.MatchType,
                enumOptions.MatchCasing,
                rootDirectory,
                out StringSegment startDirectory);

            string startDirectoryString = startDirectory.ToString();

            // Mirror MSBuild's FileMatcher.GetFileSearchData: when the resolved fixed directory does
            // not exist as a directory on disk, return an empty list rather than letting the
            // underlying FileSystemEnumerator throw on first iteration. This catches both trailing-
            // separator specs that resolve onto a file (e.g. "Foo/b.txt/") and specs naming a missing
            // subdirectory (e.g. "Missing/", "Missing/**").
            if (!Directory.Exists(startDirectoryString))
            {
                return new MSBuildEnumerationResult(
                    enumerator: null,
                    action: MSBuildSearchAction.ReturnEmptyList,
                    failedExcludeSpec: null,
                    globFailure: null);
            }

            MSBuildEnumerator enumerator = new(
                matcher,
                projectDirectory,
                stripProjectDirectory: !Path.IsPathFullyQualified(fileSpec),
                startDirectoryString,
                enumOptions);

            return new MSBuildEnumerationResult(
                enumerator: enumerator,
                action: MSBuildSearchAction.RunSearch,
                failedExcludeSpec: null,
                globFailure: null);
        }
        finally
        {
            if (excludes is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
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
