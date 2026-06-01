// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Replays a <see cref="RecordedFileSystem"/> through an <see cref="IEnumerationMatcher"/>,
///  mirroring the breadth-first scheduling and callback order of a real
///  <see cref="FileSystemEnumerator{TResult}"/> driven by a <see cref="MatchEnumerator{TResult}"/>,
///  but without any file system interaction.
/// </summary>
/// <remarks>
///  <para>
///   This makes it possible to test and performance-evaluate the matching engine over large data
///   sets captured by <see cref="DirectoryEnumerationRecorder"/> deterministically and without I/O.
///   For each directory the matcher receives the same calls a real enumeration would produce:
///   <see cref="IEnumerationMatcher.MatchesDirectory"/> (recursion decision) and
///   <see cref="IEnumerationMatcher.MatchesFile"/> (inclusion decision) per entry, followed by a
///   single <see cref="IEnumerationMatcher.DirectoryFinished"/> when the directory is exhausted.
///  </para>
///  <para>
///   Results are returned as relative paths in the same shape as
///   <see cref="MSBuildEnumerator"/> and <see cref="GlobEnumerator"/> produce them.
///  </para>
/// </remarks>
public sealed class RecordedDirectoryEnumerator : IDisposable
{
    private readonly RecordedFileSystem _fileSystem;
    private readonly IEnumerationMatcher _matcher;
    private readonly bool _recurseSubdirectories;
    private readonly bool _excludeDirectories;
    private readonly bool _stripRootDirectory;
    private readonly string _rootDirectory;
    private readonly int _rootDirectoryLength;

    private readonly Queue<string> _pending = new();
    private string _currentDirectory = string.Empty;
    private IReadOnlyList<RecordedFileSystem.Entry> _currentEntries = [];
    private int _index;
    private bool _scanning;
    private string _current = string.Empty;
    private bool _disposed;

    /// <summary>
    ///  Initializes a new instance of the <see cref="RecordedDirectoryEnumerator"/> class.
    /// </summary>
    /// <param name="fileSystem">The recorded tree to replay.</param>
    /// <param name="matcher">The matcher that decides inclusion and recursion.</param>
    /// <param name="rootDirectory">
    ///  The directory to start enumerating. When <see langword="null"/>, the recorded
    ///  <see cref="RecordedFileSystem.Root"/> is used.
    /// </param>
    /// <param name="recurseSubdirectories">
    ///  When <see langword="true"/> (default), subdirectories the matcher accepts are recursed into.
    /// </param>
    /// <param name="excludeDirectories">
    ///  When <see langword="true"/>, directory entries are never returned as results (MSBuild
    ///  semantics). When <see langword="false"/> (default), directory entries can be returned when
    ///  the matcher includes them (plain <see cref="MatchEnumerator{TResult}"/> semantics).
    /// </param>
    /// <param name="stripRootDirectory">
    ///  When <see langword="true"/> (default), results are returned relative to
    ///  <paramref name="rootDirectory"/>.
    /// </param>
    public RecordedDirectoryEnumerator(
        RecordedFileSystem fileSystem,
        IEnumerationMatcher matcher,
        string? rootDirectory = null,
        bool recurseSubdirectories = true,
        bool excludeDirectories = false,
        bool stripRootDirectory = true)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(matcher);

        _fileSystem = fileSystem;
        _matcher = matcher;
        _recurseSubdirectories = recurseSubdirectories;
        _excludeDirectories = excludeDirectories;
        _stripRootDirectory = stripRootDirectory;

        _rootDirectory = RecordedFileSystem.Normalize(
            string.IsNullOrEmpty(rootDirectory) ? fileSystem.Root : rootDirectory!);
        _rootDirectoryLength = _rootDirectory.Length
            + (Path.EndsInDirectorySeparator(_rootDirectory) ? 0 : 1);

        _pending.Enqueue(_rootDirectory);
    }

    /// <summary>
    ///  The current result, valid after <see cref="MoveNext"/> returns <see langword="true"/>.
    /// </summary>
    public string Current => _current;

    /// <summary>
    ///  Advances to the next matching result.
    /// </summary>
    /// <returns><see langword="true"/> when a result is available; otherwise <see langword="false"/>.</returns>
    public bool MoveNext()
    {
        while (true)
        {
            if (!_scanning)
            {
                if (_pending.Count == 0)
                {
                    return false;
                }

                _currentDirectory = _pending.Dequeue();
                _currentEntries = _fileSystem.GetEntries(_currentDirectory);
                _index = 0;
                _scanning = true;
            }

            while (_index < _currentEntries.Count)
            {
                RecordedFileSystem.Entry entry = _currentEntries[_index++];

                if (entry.IsDirectory)
                {
                    if (_recurseSubdirectories
                        && _matcher.MatchesDirectory(_currentDirectory, entry.Name, matchForExclusion: false))
                    {
                        _pending.Enqueue(Combine(_currentDirectory, entry.Name));
                    }

                    if (!_excludeDirectories && _matcher.MatchesFile(_currentDirectory, entry.Name))
                    {
                        _current = TransformEntry(_currentDirectory, entry.Name);
                        return true;
                    }
                }
                else if (_matcher.MatchesFile(_currentDirectory, entry.Name))
                {
                    _current = TransformEntry(_currentDirectory, entry.Name);
                    return true;
                }
            }

            _matcher.DirectoryFinished();
            _scanning = false;
        }
    }

    private string TransformEntry(string directory, string name)
    {
        if (!_stripRootDirectory || directory.Length <= _rootDirectoryLength)
        {
            return name;
        }

        return $"{directory[_rootDirectoryLength..]}{Path.DirectorySeparatorChar}{name}";
    }

    private static string Combine(string directory, string name) =>
        $"{directory}{Path.DirectorySeparatorChar}{name}";

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _matcher.Dispose();
    }
}
