// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Represents a specification for matching files and directories.
/// </summary>
/// <remarks>
///  <para>
///   This is designed to faciliate composition with <see cref="FileSystemEnumerator{TResult}"/>s.
///   As such, names should always be passed breadth-first when enumerating directories.
///  </para>
/// </remarks>
public interface IEnumerationMatcher : IDisposable
{
    /// <summary>
    ///  The current directory has finished processing. Any further calls to <see cref="MatchesFile"/>
    ///  or <see cref="MatchesDirectory"/> will now have a different current directory.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Typically used to invalidate cached directory match state.
    ///  </para>
    /// </remarks>
    void DirectoryFinished();

    /// <summary>
    ///  Returns <see langword="true"/> if the specified <paramref name="directoryName"/> is a match and should (or
    ///  should not) be recursed into from the specified <paramref name="currentDirectory"/>.
    /// </summary>
    /// <param name="currentDirectory">
    ///  The directory that contains the <paramref name="directoryName"/>.
    /// </param>
    /// <param name="directoryName">
    ///  The name of the directory to check for a match.
    /// </param>
    /// <param name="matchForExclusion">
    ///  If <see langword="true"/>, this is is being checked for exclusion and matches will skip the directory.
    ///  If <see langword="false"/>, it is being checked for inclusion and the directory will be recursed.
    /// </param>
    /// <remarks>
    ///  <para>
    ///   Recursion is breadth-first, returning <see langword="true"/> from this method will put the directory at
    ///   the end of the queue to be processed, unless <paramref name="matchForExclusion"/> is true. In that case,
    ///   the directory will be skipped.
    ///  </para>
    ///  <para>
    ///   In exclusion mode, returning <see langword="true"/> will prevent recursion into the directory and will
    ///   stop all further matchers from being run. In inclusion mode, returning <see langword="true"/> will
    ///  </para>
    /// </remarks>
    bool MatchesDirectory(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> directoryName, bool matchForExclusion);

    /// <summary>
    ///  Returns <see langword="true"/> if the specified <paramref name="fileName"/> should (or should not)
    ///  be included in the results.
    /// </summary>
    /// <param name="currentDirectory">
    ///  The directory that contains the <paramref name="fileName"/>.
    /// </param>
    /// <param name="fileName">
    ///  The name of the file to check for a match.
    /// </param>
    bool MatchesFile(ReadOnlySpan<char> currentDirectory, ReadOnlySpan<char> fileName);
}
