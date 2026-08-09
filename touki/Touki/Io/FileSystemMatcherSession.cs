// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Base matcher session with conservative directory traversal and no retained resources.
/// </summary>
public abstract class FileSystemMatcherSession : IFileSystemMatcherSession
{
    /// <inheritdoc/>
    public abstract bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName);

    /// <inheritdoc/>
    public virtual DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName) => DirectoryMatchType.MayContainMatchingFiles;

    /// <inheritdoc/>
    public virtual void DirectoryFinished(ReadOnlySpan<char> directory) { }

    /// <inheritdoc/>
    public virtual void Dispose() { }
}