// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Specifies the result applied when an ordered matcher rule matches.
/// </summary>
public enum FileSystemMatchAction : byte
{
    /// <summary>
    ///  Include matching files.
    /// </summary>
    Include = 0,

    /// <summary>
    ///  Exclude matching files.
    /// </summary>
    Exclude = 1
}