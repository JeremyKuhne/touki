// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Describes why an MSBuild-compatible request has no files to enumerate.
/// </summary>
public enum MSBuildEmptyReason : byte
{
    /// <summary>
    ///  The resolved fixed start directory does not exist as a directory.
    /// </summary>
    StartDirectoryNotFound = 0
}
