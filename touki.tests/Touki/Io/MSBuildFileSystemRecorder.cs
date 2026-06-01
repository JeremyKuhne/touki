// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Build.FileSystem;

namespace Touki.Io;

/// <summary>
///  An <see cref="MSBuildFileSystemBase"/> that forwards every query to the real default file
///  system and records the result into a <see cref="RecordedMSBuildFileSystem"/>. Injected into
///  MSBuild's internal <c>FileMatcher</c> (via <see cref="FileMatcherWrapper"/>) so a real
///  traversal can be captured and later replayed by <see cref="MSBuildFileSystemPlayback"/>.
/// </summary>
/// <remarks>
///  <para>
///   The base <see cref="MSBuildFileSystemBase"/> virtuals already forward to MSBuild's default
///   file system, so each override calls <c>base</c> for the real answer, records it, and returns
///   it unchanged. Only the four members <c>FileMatcher</c> actually invokes are overridden.
///  </para>
/// </remarks>
public sealed class MSBuildFileSystemRecorder : MSBuildFileSystemBase
{
    private readonly RecordedMSBuildFileSystem _data;

    /// <summary>
    ///  Initializes a new instance writing into <paramref name="data"/>.
    /// </summary>
    public MSBuildFileSystemRecorder(RecordedMSBuildFileSystem data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
    }

    /// <inheritdoc/>
    public override IEnumerable<string> EnumerateFileSystemEntries(
        string path,
        string searchPattern = "*",
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly)
    {
        List<string> results = [.. base.EnumerateFileSystemEntries(path, searchPattern, searchOption)];
        _data.RecordEnumeration(
            RecordedMSBuildFileSystem.EnumerateFileSystemEntriesMethod, path, searchPattern, (int)searchOption, results);
        return results;
    }

    /// <inheritdoc/>
    public override IEnumerable<string> EnumerateFiles(
        string path,
        string searchPattern = "*",
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly)
    {
        List<string> results = [.. base.EnumerateFiles(path, searchPattern, searchOption)];
        _data.RecordEnumeration(
            RecordedMSBuildFileSystem.EnumerateFilesMethod, path, searchPattern, (int)searchOption, results);
        return results;
    }

    /// <inheritdoc/>
    public override IEnumerable<string> EnumerateDirectories(
        string path,
        string searchPattern = "*",
        System.IO.SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly)
    {
        List<string> results = [.. base.EnumerateDirectories(path, searchPattern, searchOption)];
        _data.RecordEnumeration(
            RecordedMSBuildFileSystem.EnumerateDirectoriesMethod, path, searchPattern, (int)searchOption, results);
        return results;
    }

    /// <inheritdoc/>
    public override bool DirectoryExists(string path)
    {
        bool exists = base.DirectoryExists(path);
        _data.RecordExistence(RecordedMSBuildFileSystem.DirectoryExistsMethod, path, exists);
        return exists;
    }
}
