// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Describes why an MSBuild-compatible request was rejected before enumeration.
/// </summary>
public enum MSBuildRejectionReason : byte
{
    /// <summary>
    ///  The include would recursively enumerate an entire drive or share while that operation is disabled.
    /// </summary>
    DriveEnumerationForbidden = 0
}
