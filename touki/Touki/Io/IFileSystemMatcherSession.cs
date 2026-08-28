// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Root-bound matching state for one file-system enumeration.
/// </summary>
/// <remarks>
///  <para>
///   Calls are single-threaded and follow <see cref="FileSystemEnumerator{TResult}"/> callback order.
///   Input spans are valid only for the duration of each call.
///  </para>
/// </remarks>
public interface IFileSystemMatcherSession : IDisposable
{
    /// <summary>
    ///  Returns whether the specified file matches.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the file.</param>
    /// <param name="fileName">The file name.</param>
    /// <returns><see langword="true"/> if the file matches; otherwise <see langword="false"/>.</returns>
    bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName);

    /// <summary>
    ///  Describes whether files below the specified candidate directory can match.
    /// </summary>
    /// <param name="currentDirectory">The directory containing the candidate directory.</param>
    /// <param name="directoryName">The candidate directory name.</param>
    /// <returns>The match classification for the candidate directory and its descendants.</returns>
    DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName);

    /// <summary>
    ///  Signals that enumeration of the specified directory has completed.
    /// </summary>
    /// <param name="directory">The completed directory.</param>
    void DirectoryFinished(ReadOnlySpan<char> directory);
}