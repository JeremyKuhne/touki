// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matches a file from the separate directory and file-name spans supplied by file-system enumeration.
/// </summary>
/// <param name="currentDirectory">The directory containing the file.</param>
/// <param name="fileName">The file name.</param>
/// <returns><see langword="true"/> if the file matches; otherwise <see langword="false"/>.</returns>
public delegate bool FileSystemMatchPredicate(
    ReadOnlySpan<char> currentDirectory,
    ReadOnlySpan<char> fileName);