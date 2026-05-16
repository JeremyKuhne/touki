// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Mirrors MSBuild's internal <c>FileMatcher.SearchAction</c> enum, returned as part of
///  <see cref="MSBuildEnumerationResult"/>. Numeric values match MSBuild's enum so oracle parity with
///  <c>FileMatcher</c> is a direct compare.
/// </summary>
public enum MSBuildSearchAction
{
    /// <summary>
    ///  Normal enumeration ran (or will run lazily) to completion.
    /// </summary>
    RunSearch = 0,

    /// <summary>
    ///  The input could not be evaluated as a glob and should be surfaced to the caller as a literal
    ///  file spec string rather than expanded.
    /// </summary>
    ReturnFileSpec = 1,

    /// <summary>
    ///  Enumeration was abandoned and the caller should treat the result as an empty list.
    /// </summary>
    ReturnEmptyList = 2,

    /// <summary>
    ///  A path encountered during enumeration exceeded the platform's maximum path length.
    /// </summary>
    FailBecauseFileTooLong = 3,

    /// <summary>
    ///  A subdirectory encountered during enumeration exceeded the platform's maximum path length.
    /// </summary>
    FailBecauseSubdirectoryTooLong = 4,

    /// <summary>
    ///  The include or exclude spec would recursively enumerate an entire drive or share
    ///  (e.g. <c>C:\**</c>, <c>/**</c>, <c>\\server\share\**</c>) and drive enumeration is disabled.
    /// </summary>
    FailBecauseDriveEnumerationIsForbidden = 5,

    /// <summary>
    ///  Reserved for parity with MSBuild's logging-only drive-enumeration outcome. Not currently
    ///  produced by this library.
    /// </summary>
    LogDriveEnumerationWildcard = 6,
}
