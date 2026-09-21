// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#nullable disable

namespace Touki.Resources;

/// <summary>
///  Supplies an invalid null stream for runtime contract testing.
/// </summary>
internal static class StringResourceManagerNullStreamFactory
{
    /// <summary>
    ///  Returns null despite its nullable-oblivious stream signature.
    /// </summary>
    internal static Stream Create() => null;
}