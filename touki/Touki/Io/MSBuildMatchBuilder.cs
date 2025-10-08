// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using Touki.Text;

namespace Touki.Io;

/// <summary>
///  Builds an <see cref="IEnumerationMatcher"/> from MSBuild-style include and exclude specifications.
/// </summary>
public static class MSBuildMatchBuilder
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

    /// <inheritdoc cref="FromSpecification(MSBuildSpecification, ListBase{MSBuildSpecification}, MatchType, MatchCasing, string?, out StringSegment)"/>
    public static IEnumerationMatcher FromSpecification(
        string includeSpecification,
        string excludeSpecifications,
        MatchType matchType,
        MatchCasing matchCasing,
        string? rootDirectory,
        out StringSegment startDirectory)
    {
        matchCasing = Paths.GetFinalCasing(matchCasing);

        MSBuildSpecification include = new(includeSpecification);
        using var excludes = MSBuildSpecification.Split(excludeSpecifications, ignoreCase: matchCasing == MatchCasing.CaseInsensitive);
        return FromSpecification(include, excludes, matchType, matchCasing, rootDirectory, out startDirectory);
    }

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
    public static IEnumerationMatcher FromSpecification(
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
