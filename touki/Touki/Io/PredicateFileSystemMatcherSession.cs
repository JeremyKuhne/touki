// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Invokes a file predicate with the current directory and file-name spans supplied by enumeration.
/// </summary>
/// <param name="predicate">The file predicate.</param>
internal sealed class PredicateFileSystemMatcherSession(FileSystemMatchPredicate predicate)
    : FileSystemMatcherSession
{
    public override bool MatchesFile(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> fileName) => predicate(currentDirectory, fileName);
}