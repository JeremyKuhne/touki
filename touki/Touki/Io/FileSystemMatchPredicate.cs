// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Matches a file from the separate directory and file-name spans supplied by file-system enumeration.
/// </summary>
public delegate bool FileSystemMatchPredicate(
    ReadOnlySpan<char> currentDirectory,
    ReadOnlySpan<char> fileName);