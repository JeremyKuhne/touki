// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  An include specification that must be returned literally rather than expanded.
/// </summary>
public sealed class MSBuildReturnLiteralResult : MSBuildEnumerationResult
{
    /// <summary>
    ///  Initializes a result for an include specification that must be returned literally.
    /// </summary>
    /// <param name="specification">The original include specification.</param>
    /// <param name="reason">The validation reason.</param>
    internal MSBuildReturnLiteralResult(string specification, string reason)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        Specification = specification;
        Reason = reason;
    }

    /// <summary>
    ///  Gets the original include specification.
    /// </summary>
    public string Specification { get; }

    /// <summary>
    ///  Gets the validation reason.
    /// </summary>
    public string Reason { get; }
}
