// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Controls how the extglob engine constrains matches based on effective double-star participation.
/// </summary>
internal enum EffectiveDoubleStarMode : byte
{
    /// <summary>
    ///  Does not constrain effective double-star participation.
    /// </summary>
    Ignore,

    /// <summary>
    ///  Requires a match without effective double-star participation.
    /// </summary>
    RequireAbsent,

    /// <summary>
    ///  Requires a match with effective double-star participation.
    /// </summary>
    RequirePresent
}