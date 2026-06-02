// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Build.FileSystem;

namespace Touki.Io;

/// <summary>
///  An <see cref="MSBuildFileSystemBase"/> that answers every query from a
///  <see cref="RecordedMSBuildFileSystem"/> instead of touching disk. Injected into MSBuild's
///  internal <c>FileMatcher</c> (via <see cref="FileMatcherWrapper"/>) so a previously recorded
///  traversal can be replayed deterministically and without I/O.
/// </summary>
/// <remarks>
///  <para>
///   A query whose result was not captured during recording throws
///   <see cref="KeyNotFoundException"/>. Because <c>FileMatcher.GetFiles</c> is deterministic for a
///   given input, replaying the same specification visits exactly the recorded set of calls, so a
///   miss indicates the recording was produced from a different specification or tree.
///  </para>
/// </remarks>
public sealed class MSBuildFileSystemPlayback : MSBuildFileSystemBase
{
    private readonly RecordedMSBuildFileSystem _data;

    /// <summary>
    ///  Initializes a new instance reading from <paramref name="data"/>.
    /// </summary>
    public MSBuildFileSystemPlayback(RecordedMSBuildFileSystem data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

    /// <inheritdoc/>
    public override IEnumerable<string> EnumerateFileSystemEntries(
        string path,
        string searchPattern = "*",
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly) =>
        Lookup(RecordedMSBuildFileSystem.EnumerateFileSystemEntriesMethod, path, searchPattern, (int)searchOption);

    /// <inheritdoc/>
    public override IEnumerable<string> EnumerateFiles(
        string path,
        string searchPattern = "*",
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly) =>
        Lookup(RecordedMSBuildFileSystem.EnumerateFilesMethod, path, searchPattern, (int)searchOption);

    /// <inheritdoc/>
    public override IEnumerable<string> EnumerateDirectories(
        string path,
        string searchPattern = "*",
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly) =>
        Lookup(RecordedMSBuildFileSystem.EnumerateDirectoriesMethod, path, searchPattern, (int)searchOption);

    /// <inheritdoc/>
    public override bool DirectoryExists(string path)
    {
        if (_data.TryGetExistence(RecordedMSBuildFileSystem.DirectoryExistsMethod, path, out bool value))
        {
            return value;
        }

        throw new KeyNotFoundException($"No recorded DirectoryExists for '{path}'.");
    }

    private string[] Lookup(string method, string path, string pattern, int option)
    {
        if (_data.TryGetEnumeration(method, path, pattern, option, out string[] results))
        {
            return results;
        }

        throw new KeyNotFoundException(
            $"No recorded {method} for '{path}' pattern '{pattern}' option {option}.");
    }
}
