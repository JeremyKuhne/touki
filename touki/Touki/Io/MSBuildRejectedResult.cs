// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  An MSBuild-compatible request rejected by a safety policy before enumeration.
/// </summary>
public sealed class MSBuildRejectedResult : MSBuildEnumerationResult
{
    internal MSBuildRejectedResult(MSBuildRejectionReason reason, string message)
    {
        if (reason is not MSBuildRejectionReason.DriveEnumerationForbidden)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        ArgumentException.ThrowIfNullOrEmpty(message);
        Reason = reason;
        Message = message;
    }

    /// <summary>
    ///  Gets the rejection reason.
    /// </summary>
    public MSBuildRejectionReason Reason { get; }

    /// <summary>
    ///  Gets the rejection message.
    /// </summary>
    public string Message { get; }
}
