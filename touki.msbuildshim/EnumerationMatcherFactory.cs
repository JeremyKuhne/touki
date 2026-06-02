// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Io.Globbing;
using Touki.Text;

namespace Touki.Io;

/// <summary>
///  Builds the same <see cref="IEnumerationMatcher"/> instances that <see cref="GlobEnumerator"/>
///  and <see cref="MSBuildEnumerator"/> drive, without constructing a file-system-bound enumerator.
/// </summary>
/// <remarks>
///  <para>
///   This lives in the test project so it can reach the internal matcher-construction helpers and
///   expose them publicly for replay-based testing and performance evaluation (for example, feeding
///   a matcher to <see cref="RecordedDirectoryEnumerator"/>).
///  </para>
/// </remarks>
public static class EnumerationMatcherFactory
{
    /// <summary>
    ///  Builds the matcher used by <see cref="GlobEnumerator"/> for the given include and excludes.
    /// </summary>
    public static IEnumerationMatcher CreateGlob(
        string includePattern,
        IReadOnlyList<string>? excludePatterns,
        string rootDirectory,
        GlobDialect dialect = GlobDialect.PosixPath,
        GlobOptions globOptions = GlobOptions.None) =>
        GlobEnumerator.BuildMatcher(includePattern, excludePatterns, rootDirectory, dialect, globOptions);

    /// <summary>
    ///  Builds the matcher used by <see cref="MSBuildEnumerator"/> for the given file specification
    ///  and exclude specifications, returning the resolved start directory.
    /// </summary>
    public static IEnumerationMatcher CreateMSBuild(
        string fileSpec,
        string excludeSpecs,
        string projectDirectory,
        out string startDirectory)
    {
        IEnumerationMatcher matcher = MSBuildMatchBuilder.FromSpecification(
            fileSpec,
            excludeSpecs,
            MatchType.Simple,
            MatchCasing.PlatformDefault,
            projectDirectory,
            out StringSegment start);

        startDirectory = start.ToString();
        return matcher;
    }
}
