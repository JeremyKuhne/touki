// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

/// <summary>
///  Validation helpers for <see cref="StringResourceManagerOptions"/>.
/// </summary>
internal static class StringResourceManagerOptionsExtensions
{
    /// <summary>
    ///  Validates that <paramref name="options"/> contains only defined flags.
    /// </summary>
    /// <param name="options">The options to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    internal static void Validate(this StringResourceManagerOptions options)
    {
        if (options is not (StringResourceManagerOptions.None
            or StringResourceManagerOptions.IgnoreNonStringResources))
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }
}
