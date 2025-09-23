// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Some code is from .NET Runtime.
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Touki.Text;

namespace Touki.Io;

/// <summary>
///  Path related helpers and utilities.
/// </summary>
public static class Paths
{
    /// <summary>
    ///  The maximum path length on Windows when long paths are not enabled or the path does not start with "\\?\".
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is useful as a default stack allocation size for path related operations as it covers most paths you're
    ///   likely to see and doesn't stress the stack too much (520 bytes).
    ///  </para>
    /// </remarks>
    public const int MaxShortPath = 260;

    /// <summary>
    ///  The default match casing for the current operating system.
    /// </summary>
    public static MatchCasing OSDefaultMatchCasing { get; } =
#if NETFRAMEWORK
        MatchCasing.CaseInsensitive;
#else
        OperatingSystem.IsWindows()
            || OperatingSystem.IsMacOS()
            || OperatingSystem.IsIOS()
            || OperatingSystem.IsTvOS()
            || OperatingSystem.IsWatchOS()
                ? MatchCasing.CaseInsensitive
                : MatchCasing.CaseSensitive;
#endif

    /// <summary>
    ///  Given <paramref name="matchCasing"/>, ensure that it is set to a specific casing. The default is
    ///  to get the default casing for the current operating system.
    /// </summary>
    public static MatchCasing GetFinalCasing(MatchCasing matchCasing) => matchCasing switch
    {
        MatchCasing.CaseSensitive => MatchCasing.CaseSensitive,
        MatchCasing.CaseInsensitive => MatchCasing.CaseInsensitive,
        _ => OSDefaultMatchCasing
    };

    /// <summary>
    ///  Returns whether the <paramref name="name"/> matches the <paramref name="expression"/>.
    /// </summary>
    /// <param name="expression">The expression to match with, such as "*foo".</param>
    /// <param name="name">The name to match against the expression.</param>
    /// <param name="matchCasing">The casing to use.</param>
    public static bool MatchesExpression(
        ReadOnlySpan<char> name,
        ReadOnlySpan<char> expression,
        MatchCasing matchCasing,
        MatchType matchType = MatchType.Simple) => matchType switch
        {
            MatchType.Win32 => FileSystemName.MatchesWin32Expression(
                expression,
                name,
                ignoreCase: GetFinalCasing(matchCasing) == MatchCasing.CaseInsensitive),
            _ => FileSystemName.MatchesSimpleExpression(
                expression,
                name,
                ignoreCase: GetFinalCasing(matchCasing) == MatchCasing.CaseInsensitive)
        };

    /// <summary>
    ///  Returns <see langword="true"/> if the second directory is the same as or a subdirectory of the first directory.
    ///  Paths must be fully normalized and qualified to get valid results.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is only a string based comparison. It does not resolve symbolic links, junctions, or other file system
    ///   redirections. Additionally, alternate directory separators are not considered.
    ///  </para>
    ///  <para>
    ///   If both paths are normalized and fully qualified then comparisons will be correct. If either are rooted and
    ///   are not fully qualified, results may be incorrect. If they are normalized
    ///   <see cref="ChangeAlternateDirectorySeparators(string)"/> and <see cref="RemoveRelativeSegments(StringSegment)"/>"
    ///   and both are not rooted and neither start with a ".." segment, they can be compared correctly.
    ///  </para>
    /// </remarks>
    public static bool IsSameOrSubdirectory(
        ReadOnlySpan<char> firstDirectory,
        ReadOnlySpan<char> secondDirectory,
        bool ignoreCase)
    {
        if (Path.EndsInDirectorySeparator(firstDirectory))
        {
            // "/foo/bar/" and "/foo/bar" should still pass
            firstDirectory = firstDirectory[..^1];
        }

        return secondDirectory.StartsWith(firstDirectory, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            // They must be the same length or we need to know that there is an explicit separator at the boundary.
            && (firstDirectory.Length == secondDirectory.Length
                || secondDirectory[firstDirectory.Length] == Path.DirectorySeparatorChar);
    }

    /// <inheritdoc cref="RemoveRelativeSegments(ReadOnlySpan{char}, ref ValueStringBuilder)"/>
    public static StringSegment RemoveRelativeSegments(StringSegment path)
    {
        var sb = new ValueStringBuilder(stackalloc char[260]);

        if (RemoveRelativeSegments(path.AsSpan(), ref sb))
        {
            // This could potentially be optimized to slice if the original path starts with the result.
            path = sb.ToString();
        }

        sb.Dispose();
        return path;
    }

    /// <summary>
    ///  Converts all alternate directory separators in the given path to the primary directory separator.
    /// </summary>
    public static string ChangeAlternateDirectorySeparators(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Path.DirectorySeparatorChar == Path.AltDirectorySeparatorChar)
        {
            // No need to change anything.
            return path;
        }

        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    // RemoveRelativeSegments is originally from the .NET runtime. I was one of the primary authors of the original code.
    // This version flips some of the logic so it can handle paths that aren't fully qualified without losing context.

    /// <summary>
    ///  Try to remove relative segments from the given path (without combining with a root). Collapses runs of directory
    ///  separators. Only primary directory separators are considered (<see cref="Path.DirectorySeparatorChar"/>).
    /// </summary>
    /// <param name="path">The path to simplify.</param>
    /// <param name="builder">String builder that will store the result.</param>
    /// <returns>
    ///  <see langword="true"/> if the path was modified in copying to the <paramref name="builder"/>
    /// </returns>
    public static bool RemoveRelativeSegments(ReadOnlySpan<char> path, ref ValueStringBuilder builder)
    {
        // Need to get the length up front to allow for replacing in place.
        int pathLength = path.Length;
        int rootLength = Path.GetPathRoot(path).Length;
        bool fullyQualified = Path.IsPathFullyQualified(path);

        if (rootLength > 2 && path[rootLength - 1] != Path.DirectorySeparatorChar)
        {
            // This is important for UNCs ("\\Server\Share\" returns "\\Server\Share").
            // Drive relative paths (e.g. "C:a" instead of "C:\a") will return a root length of 2, which is correct.
            rootLength++;
        }

        int skip = rootLength;

        if (skip > 0)
        {
            builder.Append(path[..skip]);
        }

        // Remove "//", "/./", and "/../" from the path by copying each character to the output,
        // except the ones we're removing, such that the builder contains the normalized path
        // at the end.

        bool startOfSegment = true;

        for (int i = skip; i < pathLength; i++)
        {
            char c = path[i];

            // Skip this character if the prior is a directory separator.
            if (c == Path.DirectorySeparatorChar)
            {
                if (!startOfSegment)
                {
                    builder.Append(c);
                    startOfSegment = true;
                }

                continue;
            }

            if (startOfSegment && c == '.')
            {
                if (i + 1 == pathLength || path[i + 1] == Path.DirectorySeparatorChar)
                {
                    // Skip this character if it is a '.' followed by a directory separator, e.g. "parent/./child" => "parent/child"
                    continue;
                }

                if ((i + 2 == pathLength || (i + 3 == pathLength || path[i + 2] == Path.DirectorySeparatorChar))
                    && path[i + 1] == '.')
                {
                    // We've found a '..' segment. Unwind past the last segment.
                    //
                    //  "parent/child/../grandchild" => "parent/grandchild"
                    //  "../../child" => "../../child"
                    //  "C:\tmp\..\..\child" => "C:\child"

                    if (!fullyQualified && builder.Length == skip)
                    {
                        // We're not fully qualified, we need to push the skip forward if we're just past the current skip
                        // to avoid collapsing the important '..' segments.
                        //
                        // ("..\..\child" for example, should not become just "child".)

                        builder.Append("..");
                        skip += 3;
                        i += 1;
                        startOfSegment = false;
                        continue;
                    }

                    int s;
                    for (s = builder.Length - 2; s > skip; s--)
                    {
                        if (builder[s] == Path.DirectorySeparatorChar)
                        {
                            builder.Length = s + 1;
                            break;
                        }
                    }

                    if (s <= skip)
                    {
                        builder.Length = skip;
                    }

                    i += 2;
                    continue;
                }
            }

            startOfSegment = false;
            builder.Append(c);
        }

        if (builder.Length == pathLength)
        {
            // Haven't changed the source path.
            return false;
        }

        return true;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if <paramref name="pattern1"/> can never match <paramref name="pattern2"/>
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   When this returns <see langword="false"/>, it is still possible that the patterns do not overlap. To
    ///   be reasonably performant, this method only proves the most obvious cases of exclusivity.
    ///  </para>
    /// </remarks>
    public static bool AreExpressionsExclusive(
        StringSegment pattern1,
        StringSegment pattern2,
        MatchType matchType = MatchType.Simple,
        MatchCasing matchCasing = MatchCasing.PlatformDefault)
    {
        // Scan pattern1 first (optimized for common *.ext patterns)
        int firstFirstWild = pattern1.IndexOfAny('*', '?');

        // Check if pattern1 is universal wildcard
        if ((firstFirstWild == 0 && pattern1.Length == 1 && pattern1[0] == '*') ||
            (matchType == MatchType.Win32 && pattern1 == "*.*"))
        {
            return false;
        }

        // Now scan pattern2
        int secondFirstWild = pattern2.IndexOfAny('*', '?');

        // Check if pattern2 is universal wildcard
        if ((secondFirstWild == 0 && pattern2.Length == 1 && pattern2[0] == '*') ||
            (matchType == MatchType.Win32 && pattern2 == "*.*"))
        {
            return false;
        }

        matchCasing = GetFinalCasing(matchCasing);
        bool ignoreCase = matchCasing == MatchCasing.CaseInsensitive;

        if (firstFirstWild == -1 && secondFirstWild == -1)
        {
            // Both are literal - quick length check first
            return pattern1.Length != pattern2.Length || !pattern1.Equals(pattern2, ignoreCase: ignoreCase);
        }

        if (firstFirstWild == -1)
        {
            // First is literal, second has wildcards
            return !MatchesExpression(pattern1.AsSpan(), pattern2.AsSpan(), matchCasing, matchType);
        }

        if (secondFirstWild == -1)
        {
            // Second is literal, first has wildcards
            return !MatchesExpression(pattern2.AsSpan(), pattern1.AsSpan(), matchCasing, matchType);
        }

        // Check prefixes first (no additional scanning needed)
        if (firstFirstWild > 0 && secondFirstWild > 0)
        {
            var prefix1 = pattern1[..firstFirstWild];
            var prefix2 = pattern2[..secondFirstWild];
            if (!prefix1.Equals(prefix2, ignoreCase))
            {
                // Exclusive based on prefixes - no need to check suffixes
                return true;
            }
        }

        // Both have wildcards
        int firstLastWild = pattern1.LastIndexOfAny('*', '?');
        int secondLastWild = pattern2.LastIndexOfAny('*', '?');


        // Check suffixes if both patterns have them
        if (firstLastWild < pattern1.Length - 1 && secondLastWild < pattern2.Length - 1)
        {
            var suffix1 = pattern1[(firstLastWild + 1)..];
            var suffix2 = pattern2[(secondLastWild + 1)..];
            if (!suffix1.Equals(suffix2, ignoreCase))
            {
                return true;
            }
        }

        // We cannot prove exclusivity with simple prefix/suffix analysis.
        return false;
    }
}
