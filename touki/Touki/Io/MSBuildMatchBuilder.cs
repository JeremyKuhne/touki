// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

namespace Touki.Io;

/// <summary>
///  Builds an <see cref="IFileSystemMatcherSession"/> from MSBuild-style include and exclude specifications.
/// </summary>
internal static class MSBuildMatchBuilder
{
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

    /// <inheritdoc
    ///  cref="FromSpecification(MSBuildSpecification, ListBase{MSBuildSpecification}, MatchType, MatchCasing, string?)"/>
    public static MSBuildMatchBuildResult FromSpecification(
        string includeSpecification,
        string excludeSpecifications,
        MatchType matchType,
        MatchCasing matchCasing,
        string? rootDirectory)
    {
        matchCasing = Paths.GetFinalCasing(matchCasing);

        MSBuildSpecification include = new(includeSpecification);
        using ListBase<MSBuildSpecification> excludes = MSBuildSpecification.Split(
            excludeSpecifications,
            ignoreCase: matchCasing == MatchCasing.CaseInsensitive);
        return FromSpecification(include, excludes, matchType, matchCasing, rootDirectory);
    }

    /// <summary>
    ///  Generates an <see cref="IFileSystemMatcherSession"/> that encapsulates include and exclude MSBuild specifications
    ///  and determines the starting directory to enumerate from.
    /// </summary>
    /// <param name="includeSpecification">
    ///  The include specification. If not fully qualified it will be qualified against <paramref name="rootDirectory"/>.
    /// </param>
    /// <param name="excludeSpecifications">
    ///  A collection of exclude specifications. Non-applicable excludes are filtered out for efficiency.
    /// </param>
    /// <param name="matchType">
    ///  The pattern match type to use for physical filename matching. Logical MSBuild directory and exclude phases
    ///  use simple wildcard semantics.
    /// </param>
    /// <param name="matchCasing">
    ///  The casing behavior to use when matching. The final casing is normalized for the current platform.
    /// </param>
    /// <param name="rootDirectory">
    ///  The root directory used to fully qualify non-rooted specifications. If <see langword="null"/>,
    ///  the <see cref="Environment.CurrentDirectory"/> is used.
    /// </param>
    /// <returns>
    ///  The owned matcher session and resolved enumeration start directory.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   When the include is a simple recursive match (e.g. <c>**/*.cs</c>), a specialized fast matcher is used.
    ///   Otherwise a full MSBuild-aware matcher is constructed. Excludes are pre-filtered by:
    ///  </para>
    ///  <para>
    ///   - File name expression exclusivity compared to the include.<br/>
    ///   - Whether the include and exclude fixed paths overlap in either direction.<br/>
    ///   - Whether a relative exclude can escape the include root.
    ///  </para>
    ///  <para>
    ///   Simple file excludes use <c>MSBuildMatchAnyFile</c>. Proven terminal-globstar subtree excludes use
    ///   <c>MatchMSBuildSubtree</c>; remaining patterns use <c>MatchMSBuild</c>.
    ///  </para>
    /// </remarks>
    public static MSBuildMatchBuildResult FromSpecification(
        MSBuildSpecification includeSpecification,
        ListBase<MSBuildSpecification> excludeSpecifications,
        MatchType matchType,
        MatchCasing matchCasing,
        string? rootDirectory)
    {
        rootDirectory ??= Environment.CurrentDirectory;

        includeSpecification = includeSpecification.FullyQualify(rootDirectory);
        Debug.Assert(includeSpecification.IsFullyQualified);

        StringSegment startDirectory = includeSpecification.FixedPath;

        matchCasing = Paths.GetFinalCasing(matchCasing);

        IFileSystemMatcherSession include = includeSpecification.IsSimpleRecursiveMatch
            // The simplest wild match there is, namely something like `**\*.cs`.
            ? new MSBuildMatchAnyFile(
                expression: includeSpecification.FileName,
                rootPath: startDirectory,
                matchType: matchType,
                matchCasing: matchCasing,
                rootMatchCasing: matchCasing,
                useMSBuildFileNameSemantics: MSBuildFileNamePattern.RequiresPolicy(
                    includeSpecification.FileName,
                    matchType))
            // More complicated case, need to build a full MSBuild matcher.
            : new MatchMSBuild(
                includeSpecification,
                matchType: matchType,
                matchCasing: matchCasing);

        if (excludeSpecifications.Count == 0)
        {
            // No excludes, the include is all we have
            return new(include, startDirectory);
        }

        // Excludes need to be processed.

        bool ignoreCase = matchCasing == MatchCasing.CaseInsensitive;

        // The startDirectory is our root for all excludes.
        MSBuildMatchSetSession matchSet = new(include);
        try
        {
            foreach (MSBuildSpecification excludeSpecification in excludeSpecifications)
            {
                // We can ignore excludes that:
                //
                //  - Do not fall under the start directory
                //  - Do not align with the filename spec
                //    - This is things like excluding *.cs when we're including *.txt

                // Check to see if the filenames are exclusive
                if (!MSBuildFileNamePattern.RequiresPolicy(includeSpecification.FileName, matchType)
                    && !MSBuildFileNamePattern.RequiresPolicy(excludeSpecification.FileName, matchType)
                    && Paths.AreExpressionsExclusive(
                        includeSpecification.FileName,
                        excludeSpecification.FileName,
                        matchType,
                        MatchCasing.CaseInsensitive))
                {
                    // The filenames cannot possibly match the same names, ignore it.
                    continue;
                }

                if (excludeSpecification.IsFullyQualified)
                {
                    if (!Paths.IsSameOrSubdirectory(startDirectory, excludeSpecification.FixedPath, ignoreCase)
                        && !Paths.IsSameOrSubdirectory(excludeSpecification.FixedPath, startDirectory, ignoreCase))
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

                MSBuildSpecification qualifiedExclude = excludeSpecification.FullyQualify(rootDirectory);
                StringSegment matchStartPath = Paths.IsSameOrSubdirectory(
                    qualifiedExclude.FixedPath,
                    startDirectory,
                    ignoreCase)
                        ? startDirectory
                        : qualifiedExclude.FixedPath;

                matchSet.AddExclude(!excludeSpecification.IsSimpleRecursiveMatch
                    ? TryGetAnyDirectoryExpression(excludeSpecification, out StringSegment directoryExpression)
                        ? new MatchMSBuildSubtree(
                            rootPath: qualifiedExclude.FixedPath,
                            matchStartPath: matchStartPath,
                            directoryPattern: directoryExpression,
                            matchType: MatchType.Simple,
                            matchCasing: matchCasing)
                        // More complicated case, need to build a full MSBuild matcher.
                        : new MatchMSBuild(
                            qualifiedExclude,
                            matchType: matchType,
                            matchCasing: matchCasing,
                            forceLogicalSemantics: true)
                    // The simplest wild match there is, namely something like `**\*.cs`
                    : excludeSpecification.FileName != "*" && excludeSpecification.FileName != "*.*"
                        ? new MSBuildMatchAnyFile(
                            expression: excludeSpecification.FileName,
                            rootPath: qualifiedExclude.FixedPath,
                            matchType: MatchType.Simple,
                            matchCasing: MatchCasing.CaseInsensitive,
                            rootMatchCasing: matchCasing,
                            useMSBuildFileNameSemantics: false)
                        // Just skip the entire directory, all files will match.
                        : new MatchMSBuildSubtree(
                            rootPath: qualifiedExclude.FixedPath,
                            matchCasing: matchCasing));
            }

            return new(matchSet, startDirectory);
        }
        catch
        {
            matchSet.Dispose();
            throw;
        }
    }

    private static bool TryGetAnyDirectoryExpression(
        MSBuildSpecification specification,
        out StringSegment expression)
    {
        StringSegment wildPath = specification.WildPath;
        char separator = Path.DirectorySeparatorChar;
        if (specification.FileName != "*"
            || wildPath.Length < 7
            || wildPath[0] != '*'
            || wildPath[1] != '*'
            || wildPath[2] != separator
            || wildPath[^3] != separator
            || wildPath[^2] != '*'
            || wildPath[^1] != '*')
        {
            expression = default;
            return false;
        }

        expression = wildPath[3..^3];
        return !expression.IsEmpty && expression.IndexOf(separator) < 0;
    }
}
