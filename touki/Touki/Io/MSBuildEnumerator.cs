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
public sealed class MSBuildEnumerator : FileSystemEnumerator<string>
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
    private readonly IEnumerationMatcher _matcher;

    /// <summary>
    ///  Initializes a new instance of the <see cref="MSBuildEnumerator"/> class.
    /// </summary>
    private MSBuildEnumerator(
        IEnumerationMatcher matcher,
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

        _matcher = matcher;
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
        ArgumentNull.ThrowIfNull(fileSpec);

        options ??= DefaultOptions;

        MSBuildSpecification include = new(fileSpec);
        IEnumerationMatcher matcher = GenerateMatcherFromSpec(
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
        ArgumentNull.ThrowIfNull(fileSpec);

        options ??= DefaultOptions;
        MatchCasing matchCasing = Paths.GetFinalCasing(options.MatchCasing);

        MSBuildSpecification include = new(fileSpec);
        using var excludes = MSBuildSpecification.Split(excludeSpecs, ignoreCase: matchCasing == MatchCasing.CaseInsensitive);

        IEnumerationMatcher matcher = GenerateMatcherFromSpec(
            include,
            excludes,
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

    /// <inheritdoc/>
    protected override void OnDirectoryFinished(ReadOnlySpan<char> directory) =>
        // Clear the cache when we finish processing a directory
        _matcher.DirectoryFinished();

    /// <inheritdoc/>
    protected override bool ShouldRecurseIntoEntry(ref FileSystemEntry entry) =>
        _matcher.MatchesDirectory(entry.Directory, entry.FileName, matchForExclusion: false);

    /// <inheritdoc/>
    protected override bool ShouldIncludeEntry(ref FileSystemEntry entry) =>
        !entry.IsDirectory && _matcher.MatchesFile(entry.Directory, entry.FileName);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _matcher.Dispose();
        }

        base.Dispose(disposing);
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

    // In a default .NET library project, here are the default ItemExcludes that are applied to the project.
    // When looking for all *.cs files only TWO of these are relevant (exclude bin and obj).
    //
    // DefaultItemExcludes =
    //
    //  bin\Debug\/**;
    //  obj\Debug\/**;
    //  bin\/**;
    //  obj\/**;
    //  **/*.user;
    //  **/*.*proj;
    //  **/*.sln;
    //  **/*.slnx;
    //  **/*.vssscc;
    //  **/.DS_Store

    /// <summary>
    ///  Generates an <see cref="IEnumerationMatcher"/> that encapsulates include and exclude MSBuild specifications
    ///  and determines the starting directory to enumerate from.
    /// </summary>
    /// <param name="includeSpecification">
    ///  The include specification. If not fully qualified it will be qualified against <paramref name="rootDirectory"/>.
    /// </param>
    /// <param name="excludeSpecifications">
    ///  A collection of exclude specifications. Non-applicable excludes are filtered out for efficiency.
    /// </param>
    /// <param name="matchType">
    ///  The pattern match type to use when evaluating file and directory names.
    /// </param>
    /// <param name="matchCasing">
    ///  The casing behavior to use when matching. The final casing is normalized for the current platform.
    /// </param>
    /// <param name="rootDirectory">
    ///  The root directory used to fully qualify non-rooted specifications. If <see langword="null"/>,
    ///  the <see cref="Environment.CurrentDirectory"/> is used.
    /// </param>
    /// <param name="startDirectory">
    ///  When this method returns, contains the fixed directory portion that should be used as the enumeration root.
    /// </param>
    /// <returns>
    ///  An <see cref="IEnumerationMatcher"/> that applies the include and applicable exclude rules.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   When the include is a simple recursive match (e.g. <c>**/*.cs</c>), a specialized fast matcher is used.
    ///   Otherwise a full MSBuild-aware matcher is constructed. Excludes are pre-filtered by:
    ///  </para>
    ///  <para>
    ///   - File name expression exclusivity compared to the include.<br/>
    ///   - Whether the exclude falls under the include's fixed path.<br/>
    ///   - Whether a relative exclude can escape the include root.
    ///  </para>
    ///  <para>
    ///   Simple excludes are mapped to either a <c>MatchAnyFile</c> or a <c>MatchAnyDirectory</c> depending on
    ///   whether the file expression is a wildcard for all files.
    ///  </para>
    /// </remarks>
    public static IEnumerationMatcher GenerateMatcherFromSpec(
        MSBuildSpecification includeSpecification,
        ListBase<MSBuildSpecification> excludeSpecifications,
        MatchType matchType,
        MatchCasing matchCasing,
        string? rootDirectory,
        out StringSegment startDirectory)
    {
        rootDirectory ??= Environment.CurrentDirectory;

        includeSpecification = includeSpecification.FullyQualify(rootDirectory);
        Debug.Assert(includeSpecification.IsFullyQualified);

        startDirectory = includeSpecification.FixedPath;

        matchCasing = Paths.GetFinalCasing(matchCasing);

        IEnumerationMatcher include = includeSpecification.IsSimpleRecursiveMatch
            // The simplest wild match there is, namely something like `**\*.cs`.
            ? new MatchAnyFile(
                expression: includeSpecification.FileName,
                rootPath: startDirectory,
                matchType: matchType,
                matchCasing: matchCasing)
            // More complicated case, need to build a full MSBuild matcher.
            : new MatchMSBuild(
                includeSpecification,
                matchType: matchType,
                matchCasing: matchCasing);

        if (excludeSpecifications.Count == 0)
        {
            // No excludes, the include is all we have
            return include;
        }

        // Excludes need to be processed.

        bool ignoreCase = matchCasing == MatchCasing.CaseInsensitive;

        // The startDirectory is our root for all excludes.
        MatchSet matchSet = new(include);
        foreach (MSBuildSpecification excludeSpecification in excludeSpecifications)
        {
            // We can ignore excludes that:
            //
            //  - Do not fall under the start directory
            //  - Do not align with the filename spec
            //    - This is things like excluding *.cs when we're including *.txt

            // Check to see if the filenames are exclusive
            if (Paths.AreExpressionsExclusive(
                includeSpecification.FileName,
                excludeSpecification.FileName,
                matchType,
                matchCasing))
            {
                // The filenames cannot possibly match the same names, ignore it.
                continue;
            }

            if (excludeSpecification.IsFullyQualified)
            {
                if (!Paths.IsSameOrSubdirectory(excludeSpecification.FixedPath, startDirectory, ignoreCase))
                {
                    // Not part of the include path, ignore it.
                    continue;
                }
            }
            else if (!excludeSpecification.IsNestedRelative)
            {
                // Not fully qualified and it can escape the root, ignore it.
                continue;
            }

            var qualifiedExclude = excludeSpecification.FullyQualify(rootDirectory);

            matchSet.AddExclude(!excludeSpecification.IsSimpleRecursiveMatch
                // More complicated case, need to build a full MSBuild matcher.
                ? new MatchMSBuild(
                    qualifiedExclude,
                    matchType: matchType,
                    matchCasing: matchCasing)
                // The simplest wild match there is, namely something like `**\*.cs`
                : excludeSpecification.FileName != "*"
                    ? new MatchAnyFile(
                        expression: excludeSpecification.FileName,
                        rootPath: qualifiedExclude.FixedPath,
                        matchType: matchType,
                        matchCasing: matchCasing)
                    // Just skip the entire directory, all files will match.
                    : new MatchAnyDirectory(
                        expression: excludeSpecification.FileName,
                        rootPath: qualifiedExclude.FixedPath,
                        matchType: matchType,
                        matchCasing: matchCasing));
        }

        return matchSet;
    }
}
