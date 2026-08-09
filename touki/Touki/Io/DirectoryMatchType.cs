// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Describes whether files below a candidate directory can match a file-system matcher.
/// </summary>
public enum DirectoryMatchType : byte
{
    /// <summary>
    ///  Files below the directory may match. The enumerator must recurse to determine which files match.
    /// </summary>
    MayContainMatchingFiles = 0,

    /// <summary>
    ///  No file below the directory can match.
    /// </summary>
    NoDescendantFilesMatch = 1,

    /// <summary>
    ///  Every file below the directory matches.
    /// </summary>
    AllDescendantFilesMatch = 2
}