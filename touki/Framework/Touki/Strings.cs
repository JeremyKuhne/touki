// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public static partial class Strings
{
    /// <summary>
    ///  Allocates a string of the specified length filled with null characters.
    /// </summary>
    internal static string FastAllocateString(int length) =>
        // This calls FastAllocateString in the runtime, with extra checks.
        new string('\0', length);
}
