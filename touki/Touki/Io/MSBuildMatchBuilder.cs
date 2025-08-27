// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Buffers;
#endif
using Touki.Collections;
using Touki.Text;

namespace Touki.Io;

/// <summary>
///  Helpers for working with MSBuild formatted strings.
/// </summary>
public static class MSBuildMatchBuilder
{
    private static readonly char[] s_wildcardCharacters = ['*', '?'];
    private static readonly char[] s_wildcardAndSemicolonCharacters = ['*', '?', ';'];
    private static readonly char[] s_referenceCharacters = ['$', '@'];

#if NET
    private static readonly SearchValues<string> s_propertyAndItemReferences = SearchValues.Create(["$(", "@("], StringComparison.Ordinal);
#else
    private static readonly string[] s_propertyAndItemReferences = ["$(", "@("];
#endif

    /// <summary>
    ///  Unescapes the given <paramref name="segment"/> if needed. `%` is used to escape special characters
    ///  in MSBuild strings, such as `*`, `?`, and `%`.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This method will allocate a new string for the segment if needed.
    ///  </para>
    /// </remarks>
    public static StringSegment UnescapeSegment(StringSegment segment)
    {
        // Don't bother if the segment doesn't contain an escape character.
        if (!segment.Contains('%'))
        {
            return segment;
        }

        // Path segments should never be over 256 characters, so we shouldn't need to stack allocate more than this.
        using ValueStringBuilder builder = new(stackalloc char[256]);

        // Iterate through the segment and unescape characters as needed. Escape sequences are of the form `%XX` where
        // `XX` is the hexadecimal representation of the character. Invalid escape sequences are left as is.
        for (int i = 0; i < segment.Length; i++)
        {
            char c = segment[i];
            if (c != '%'
                || i + 2 >= segment.Length
                || !segment[i + 1].TryDecodeHexDigit(out int firstDigit)
                || !segment[i + 2].TryDecodeHexDigit(out int secondDigit))
            {
                builder.Append(c);
            }
            else
            {
                // Valid escape sequence, decode it.
                builder.Append((char)((firstDigit << 4) + secondDigit));
                i += 2;
            }
        }

        // It is possible that all of the escapes are "invalid", so check if we've actually built anything different.
        return builder.Length < segment.Length ? builder.ToString() : segment;
    }

    /// <summary>
    ///  Tries to split the given <paramref name="segment"/> into a list of <see cref="StringSegment"/>s on
    ///  semicolon `;` separators.
    /// </summary>
    /// <param name="stringSegments">
    ///  The collection of split segments, if splitting was necessary. Dispose of this collection when done to
    ///  ensure shared resources are returned. Empty segments are excluded from the result.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the split was successful and <paramref name="stringSegments"/> contains the
    ///  resulting segments. <see langword="false"/> if there were not multiple segments.
    /// </returns>
    public static bool TrySplit(
        StringSegment segment,
        [NotNullWhen(true)] out IList<StringSegment>? stringSegments)
    {
        StringSegment right = segment;
        stringSegments = null;

        while (right.TrySplit(';', out StringSegment left, out right))
        {
            if (left.Length == segment.Length)
            {
                // Nothing to split.
                return false;
            }

            if (left.IsEmpty)
            {
                // Skip empty segments.
                continue;
            }

            stringSegments ??= [];
            stringSegments.Add(left);
        }

        return stringSegments is not null;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the given <paramref name="specification"/> is a simple recursive match of
    ///  all files or directories of a given name.
    /// </summary>
    /// <param name="specification">The path specification to check. Does not need to be unescaped or normalized.</param>
    /// <param name="name">The unescaped name of the file or directory to be recursively matched.</param>
    /// <param name="isFile">
    ///  <see langword="true"/> if the match is a file match. <see langword="false"/> if the match is a directory match.
    /// </param>
    public static bool IsSimpleMatch(StringSegment specification, out StringSegment name, out bool isFile)
    {
        StringSegment right = specification;
        StringSegment first = default;
        StringSegment second = default;
        name = default;
        isFile = false;

        // Patterns we're looking for:
        //
        //  "bin\**"     - Any directory under "bin"
        //  "**\*.user"  - Any file with a ".user" extension in any directory

        int index = 0;

        while (right.TrySplitAny(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar,
            out StringSegment left,
            out right))
        {
            if (index == 0)
            {
                first = left;
            }
            else if (index == 1)
            {
                second = left;
            }
            else
            {
                // More than two segments, not a simple match.
                return false;
            }

            if (!left.IsEmpty)
            {
                index++;
            }
        }

        if (first == "**")
        {
            name = UnescapeSegment(second);
            isFile = true;
            return true;
        }

        if (second == "**")
        {
            name = UnescapeSegment(first);
            isFile = false;
            return true;
        }

        return false;
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
    //
    // Ideally we'd dedupe some of these.

    // If there are no wildcards in the include or exclude specification, they are resolved as paths and excluded
    // or included as is. As such we'll need to process them separately.

    // Include and exclude specifications are expected to have had properties replaced before arriving at this method.

    // State questions:
    //
    // Are all specs under the project path?
    //   Yes? - Single pass
    //   No? - Multiple passes for each specification

    public static void GenerateMatchersFromSpec(
        StringSegment includeSpecsUnescaped,
        StringSegment excludeSpecsUnescaped,
        MatchType matchType,
        MatchCasing matchCasing,
        string? projectDirectory,
        out string startDirectory)
    {
        startDirectory = default;

        bool hasExcludes = !excludeSpecsUnescaped.IsEmpty;

        IEnumerationMatcher? includeMatcher = null;
        ArrayPoolList<IEnumerationMatcher>? includeMatchers = null;

        // Explicit includes need to be collected separately so that they can be matched against the exclude patterns.
        ArrayPoolList<string>? explicitIncludes = null;

        // Ensure we're fully normalized
        string rootDirectory = projectDirectory is null
            ? Path.GetFullPath(Environment.CurrentDirectory)
            : Path.GetFullPath(projectDirectory);

        // Walk through the include specification. If all we have are simple matches, they can easily be combined.
        if (!TrySplit(includeSpecsUnescaped, out IList<StringSegment>? includeSegments))
        {
            // Single include specification. Should be the most common case.

            if (includeSpecsUnescaped.IndexOfAny('*', '?') < 0)
            {
                // No wildcards, we're a simple included file.
                explicitIncludes ??= [];
                explicitIncludes.Add(Path.GetFullPath(UnescapeSegment(includeSpecsUnescaped).ToString(), rootDirectory));
            }
            else if (IsSimpleMatch(includeSpecsUnescaped, out StringSegment name, out bool isFile))
            {
                includeMatcher = isFile
                    ? new MatchAnyFile(name, matchType, matchCasing)
                    : new MatchAnyDirectory(name, matchType, matchCasing);
            }
            else
            {
                // More complex match.
                // includeMatcher = new MSBuildMatch()
            }
        }
        else
        {
            // We have multiple includes, need to split and optimize.
            throw new NotImplementedException();
        }
    }

    public static void SplitAndReduceSpecs(
        StringSegment includeSpecs,
        StringSegment excludeSpecs,
        bool ignoreCase,
        out SingleOptimizedList<MSBuildSpecification> includeReducedSpecs,
        out SingleOptimizedList<MSBuildSpecification> excludeReducedSpecs)
    {
        includeReducedSpecs = SplitSpecs(
            includeSpecs,
            ignoreCase);

        excludeReducedSpecs = SplitSpecs(
            excludeSpecs,
            ignoreCase);

        int includeCount = includeReducedSpecs.Count;
        int excludeCount = excludeReducedSpecs.Count;

        if (includeCount == 1 && excludeCount == 0)
        {
            // Only one include, nothing to reduce.
            return;
        }

        // Check to see if all of our specs are relative to the same directory.
        bool allNestedRelative = true;

        for (int i = 0; i < includeCount; i++)
        {
            var include = includeReducedSpecs[i];
            if (Path.IsPathRooted(include.Normalized.AsSpan())
                || include.Normalized.StartsWith(".."))
            {
                allNestedRelative = false;
                break;
            }
        }

        // If there are multiple excludes, try and reduce

        if (includeCount == 1)
        {
            var include = includeReducedSpecs[0];
            if (include.HasAnyWildCards && include.WildPath == "**")
            {
                // The include is a recursive match for all files. If this is a 
                // 
            }
        }

        return;
    }

    /// <summary>
    ///  Given a normalized MSBuild specification, determines if it is a simple recursive match for all files. That
    ///  is, it ends in `/**/filespec` or the equivalent.
    /// </summary>
    /// <param name="normalizedSpec">Spec with directory separators normalized.</param>
    /// <param name="fileName">When <see langword="true"/> is returned, this will be the filename portion.</param>
    /// <param name="fixedDirectory">When <see langword="true"/> is returned, this will be the fixed part of the directory.</param>
    /// <returns><see langword="true"/> when the spec is a simple recursive match.</returns>
    public static bool IsAllFilesRecursive(
        StringSegment normalizedSpec,
        out StringSegment fixedDirectory,
        out StringSegment fileName)
    {
        fixedDirectory = default;
        fileName = default;

        int firstSeparator = normalizedSpec.IndexOf(Path.DirectorySeparatorChar);
        int firstWildCard = normalizedSpec.IndexOfAny('*', '?');

        // Some of the cases we're looking for:
        //
        //  bin/**              <- Equivalent to bin/**/*
        //  **/*.cs
        //  bin/debug/**/*.cs

        // Quick check - if no wildcards, this can't be a recursive match
        if (firstWildCard < 0)
        {
            return false;
        }

        char dirSep = Path.DirectorySeparatorChar;

        // Case 1: path/**
        if (normalizedSpec.Length >= 3 &&
            normalizedSpec[^3] == dirSep &&
            normalizedSpec[^2] == '*' &&
            normalizedSpec[^1] == '*')
        {
            fixedDirectory = normalizedSpec[..^3];
            fileName = "*";
            return true;
        }

        // Find the last "**/" pattern
        int lastDoubleStarIndex = -1;
        int lastDoubleStarEnd = -1;

        for (int i = 0; i <= normalizedSpec.Length - 3; i++)
        {
            if (normalizedSpec[i] == '*' &&
                normalizedSpec[i + 1] == '*' &&
                normalizedSpec[i + 2] == dirSep &&
                (i == 0 || normalizedSpec[i - 1] == dirSep))
            {
                lastDoubleStarIndex = i;
                lastDoubleStarEnd = i + 3;  // Points to the character after "**/"
            }
        }

        if (lastDoubleStarIndex >= 0)
        {
            // Check if there are any more directory separators after this "**/"
            bool hasMoreSeparators = false;

            for (int i = lastDoubleStarEnd; i < normalizedSpec.Length; i++)
            {
                if (normalizedSpec[i] == dirSep)
                {
                    hasMoreSeparators = true;
                    break;
                }
            }

            if (!hasMoreSeparators)
            {
                // This is a valid recursive pattern
                if (lastDoubleStarIndex == 0)
                {
                    fixedDirectory = "";  // "**/" at the beginning
                }
                else
                {
                    // If the "**" pattern is not at the beginning, it must be preceded by a separator
                    // We want to include everything up to but not including that separator
                    fixedDirectory = normalizedSpec[..(lastDoubleStarIndex - 1)];
                }

                fileName = normalizedSpec[lastDoubleStarEnd..];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Splits semicolon-separated MSBuild specifications and buckets them into wildcard and literal versions.
    /// </summary>
    /// <param name="specs">
    ///  The possibly semicolon-separated MSBuild specification to split. In MSBuild this is processed unescaped, but
    ///  the logic would still work here if you wanted escaped `*` and `?` characters to be treated as literals in
    ///  matches ("/foo/bar?.text" is a valid file name on Unix).
    /// </param>
    /// <returns>
    ///  The list of split specifications. Each specification is normalized, meaning that backslashes are converted to
    ///  forward slashes on Unix and vice versa on Windows. Consecutive separators will be collapsed to a single
    ///  separator. Duplicate specifications will be removed.
    /// </returns>
    public static SingleOptimizedList<MSBuildSpecification> SplitSpecs(
        StringSegment specs,
        bool ignoreCase)
    {
        SingleOptimizedList<MSBuildSpecification> splitSpecs = [];

        // MSBuild normally validates specifications after it splits each one into fixed, wildcard, and file parts.
        // None of the validation is strictly necessary, but would be done here if we wanted to roughly match.
        // Things that are considered "invalid: should be put in the literalSpecs even if they contain wildcards.
        //
        // What MSBuild considers invalid:
        //
        //  - InvalidPathCharacters - not needed anymore, the only illegal character is null.
        //  - A colon after the second character - not a real risk unless you create a `Uri` and breaks Unix paths.
        //  - `...` - this isn't needed either, there is no risk with this.
        //  - `**` not between separators - a lot of pain for not much gain, so we don't do this.

        StringSegment right = specs;

        while (right.TrySplit(';', out StringSegment left, out right))
        {
            if (left.IsEmpty)
            {
                // Skip empty segments.
                continue;
            }

            // Normalize the spec. This will collapse any consecutive separators into a single separator and reduce
            // unnecessary segments like "." and "..". MSBuild doesn't fully do this at this stage. For performance we
            // want to dedupe the segments, which can done more efficiently if we normalize first. This will also
            // improve performance further downstream as we can be more efficient with additional comparisons.

            StringSegment normalized = NormalizeSpec(left);
            bool found = false;

            for (int i = 0; i < splitSpecs.Count; i++)
            {
                if (splitSpecs[i].Normalized.Equals(normalized, ignoreCase))
                {
                    // Already present.
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                splitSpecs.Add(new(left, normalized));
            }
        }

        return splitSpecs;
    }

    /// <summary>
    ///  Walks through two specifications to see if they match exactly or within the specified casing, ignoring
    ///  separator characters.
    /// </summary>
    private static bool AreSpecsEquivalent(StringSegment left, StringSegment right, bool caseInsensitive)
    {
        if (left.Equals(right))
        {
            // Fast path for exact matches.
            return true;
        }

        // Walk through the specs, comparing characters insensitively if matchCasing specifies it.
        // Backslashes and forward slashes are considered equivalent, and any amount of consecutive
        // slashes are considered as one.

        int leftIndex = 0;
        int rightIndex = 0;

        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            char leftChar = left[leftIndex];
            char rightChar = right[rightIndex];

            if (caseInsensitive)
            {
                leftChar = char.ToUpperInvariant(leftChar);
                rightChar = char.ToUpperInvariant(rightChar);
            }

            if (leftChar is not '\\' and not '/')
            {
                if (leftChar != rightChar)
                {
                    return false;
                }

                leftIndex++;
                rightIndex++;
            }

            leftIndex++;

            // Skip any consecutive slashes in the left spec.
            while (leftIndex < left.Length && left[leftIndex] is '\\' or '/')
            {
                leftIndex++;
            }

            if (rightChar is not '\\' and not '/')
            {
                return false;
            }

            rightIndex++;

            // Skip any consecutive slashes in the right spec.
            while (rightIndex < right.Length && right[rightIndex] is '\\' or '/')
            {
                rightIndex++;
            }
        }

        return leftIndex == rightIndex;
    }

    /// <summary>
    ///  Normalize a specficiation by converting backslashes to forward slashes on Unix systems, collapsing
    ///  consecutive directory separators into a single separator, trimming whitespace, and removing any
    ///  redundant path segments ("." and "..").
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Multiple specifications (separated by ';') must be split first for this to work correctly.
    ///  </para>
    /// </remarks>
    public static StringSegment NormalizeSpec(StringSegment segment)
    {
        segment = segment.Trim();

        char separatorToReplace = Path.DirectorySeparatorChar == '\\' ? '/' : '\\';

        ValueStringBuilder replaceBuilder = new(stackalloc char[Paths.MaxShortPath]);

        if (segment.Contains(separatorToReplace))
        {
            replaceBuilder.Append(segment);
            replaceBuilder.Replace(separatorToReplace, Path.DirectorySeparatorChar);
            ReadOnlySpan<char> currentState = replaceBuilder;
            replaceBuilder.Length = 0;
            Paths.RemoveRelativeSegments(currentState, ref replaceBuilder);
            return replaceBuilder.ToStringAndDispose();
        }
        else
        {
            if (segment.IndexOf(Path.DirectorySeparatorChar) < 0)
            {
                // No path segments to normalize, return the original segment.
                return segment;
            }

            bool modified = Paths.RemoveRelativeSegments(segment, ref replaceBuilder);
            if (modified)
            {
                return replaceBuilder.ToStringAndDispose();
            }
            else
            {
                // No changes made, return the original segment.
                replaceBuilder.Dispose();
                return segment;
            }
        }
    }

#if NET
    private static StringSegment UseForwardSlashSeparatorsOnUnix(StringSegment escapedSpec)
    {
        Debug.Assert(Path.DirectorySeparatorChar == '/');

        // Specs are "fixed" on Unix by flipping any backslashes to forward slashes.
        // Note that many MSBuild files use forward slashes regardless of platform.
        return escapedSpec.IsEmpty || Path.DirectorySeparatorChar != '/'
            ? escapedSpec
            : escapedSpec.Replace('\\', '/');
    }
#endif
}
