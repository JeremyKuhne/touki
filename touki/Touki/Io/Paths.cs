﻿// Copyright (c) 2025 Jeremy W Kuhne
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
    ///  Paths must be fully normalized to get valid results.
    /// </summary>
    public static bool IsSameOrSubdirectory(
        ReadOnlySpan<char> firstDirectory,
        ReadOnlySpan<char> secondDirectory,
        bool ignoreCase)
    {
        bool endsInSeparator = Path.EndsInDirectorySeparator(firstDirectory);

        if (secondDirectory.StartsWith(firstDirectory, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            if (endsInSeparator
                || firstDirectory.Length == secondDirectory.Length
                || secondDirectory[firstDirectory.Length] == Path.DirectorySeparatorChar)
            {
                return true;
            }
        }

        return false;
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
        ArgumentNull.ThrowIfNull(path);
        if (Path.DirectorySeparatorChar == Path.AltDirectorySeparatorChar)
        {
            // No need to change anything.
            return path;
        }

        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    // RemoveRelativeSegments is originally from the .NET runtime. I was one of the primary authors of the orignal code.
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
}
