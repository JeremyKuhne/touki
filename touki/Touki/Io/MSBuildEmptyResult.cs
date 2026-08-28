// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  A valid MSBuild-compatible request whose resolved search is empty.
/// </summary>
public sealed class MSBuildEmptyResult : MSBuildEnumerationResult
{
    /// <summary>
    ///  Initializes an empty result with the specified reason.
    /// </summary>
    /// <param name="reason">The reason the search is empty.</param>
    internal MSBuildEmptyResult(MSBuildEmptyReason reason)
    {
        if (reason is not MSBuildEmptyReason.StartDirectoryNotFound)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        Reason = reason;
    }

    /// <summary>
    ///  Gets the reason the search is empty.
    /// </summary>
    public MSBuildEmptyReason Reason { get; }
}
