// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

/// <summary>
///  Char helpers.
/// </summary>
public static class Chars
{
    private static Random? s_defaultRandom;

    /// <summary>
    ///  Gets a random character from the Basic Multilingual Plane (BMP) that is
    ///  not a control character and is not a non-character.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Allowed ranges: [0020..007E], [00A0..D7FF], [E000..FFFD], skipping FDD0..FDEF.
    ///  </para>
    /// </remarks>
    public static char GetRandomSimpleChar(Random? random)
    {
        random ??= s_defaultRandom ??= new Random();

        const int a1Start = 0x0020, a1End = 0x007E; // printable ASCII, excludes DEL (007F)
        const int a2Start = 0x00A0, a2End = 0xD7FF; // skips C1 controls 0080..009F and surrogates
        const int cStart = 0xE000, cEnd = 0xFFFD; // excludes FFFE/FFFF

        int lenA1 = a1End - a1Start + 1;      // 95
        int lenA2 = a2End - a2Start + 1;
        int lenC = cEnd - cStart + 1;

        while (true)
        {
#pragma warning disable CA5394 // Don't use random for cryptographic purposes
            int pick = random.Next(lenA1 + lenA2 + lenC);
#pragma warning restore CA5394
            int code = (pick < lenA1)
                ? a1Start + pick
                : (pick < lenA1 + lenA2)
                    ? a2Start + (pick - lenA1)
                    : cStart + (pick - lenA1 - lenA2);

            // Skip the 32 BMP noncharacters U+FDD0..U+FDEF to avoid oddities.
            if (code is >= 0xFDD0 and <= 0xFDEF)
            {
                continue;
            }

            return (char)code;
        }
    }
}
