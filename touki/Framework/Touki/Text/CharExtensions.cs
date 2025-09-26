// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public static partial class CharExtensions
{
    extension(char)
    {
        /// <summary>Indicates whether a character is categorized as an ASCII letter.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is an ASCII letter; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range 'A' through 'Z', inclusive,
        /// or 'a' through 'z', inclusive.
        /// </remarks>
        public static bool IsAsciiLetter(char c) =>
            (uint)((c | 0x20) - 'a') <= 'z' - 'a';

        /// <summary>Indicates whether a character is categorized as a lowercase ASCII letter.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is a lowercase ASCII letter; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range 'a' through 'z', inclusive.
        /// </remarks>
        public static bool IsAsciiLetterLower(char c) =>
            IsBetween(c, 'a', 'z');

        /// <summary>Indicates whether a character is categorized as an uppercase ASCII letter.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is an uppercase ASCII letter; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range 'A' through 'Z', inclusive.
        /// </remarks>
        public static bool IsAsciiLetterUpper(char c) =>
            IsBetween(c, 'A', 'Z');

        /// <summary>Indicates whether a character is categorized as an ASCII digit.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is an ASCII digit; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range '0' through '9', inclusive.
        /// </remarks>
        public static bool IsAsciiDigit(char c) =>
            IsBetween(c, '0', '9');

        /// <summary>Indicates whether a character is categorized as an ASCII letter or digit.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is an ASCII letter or digit; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range 'A' through 'Z', inclusive,
        /// 'a' through 'z', inclusive, or '0' through '9', inclusive.
        /// </remarks>
        public static bool IsAsciiLetterOrDigit(char c) =>
            IsAsciiLetter(c) | IsBetween(c, '0', '9');

        /// <summary>Indicates whether a character is categorized as an ASCII hexadecimal digit.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is a hexadecimal digit; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range '0' through '9', inclusive,
        /// 'A' through 'F', inclusive, or 'a' through 'f', inclusive.
        /// </remarks>
        public static bool IsAsciiHexDigit(char c) =>
            System.HexConverter.IsHexChar(c);

        /// <summary>Indicates whether a character is categorized as an ASCII upper-case hexadecimal digit.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is a hexadecimal digit; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range '0' through '9', inclusive,
        /// or 'A' through 'F', inclusive.
        /// </remarks>
        public static bool IsAsciiHexDigitUpper(char c) =>
            System.HexConverter.IsHexUpperChar(c);

        /// <summary>Indicates whether a character is categorized as an ASCII lower-case hexadecimal digit.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <returns>true if <paramref name="c"/> is a lower-case hexadecimal digit; otherwise, false.</returns>
        /// <remarks>
        /// This determines whether the character is in the range '0' through '9', inclusive,
        /// or 'a' through 'f', inclusive.
        /// </remarks>
        public static bool IsAsciiHexDigitLower(char c) =>
            System.HexConverter.IsHexLowerChar(c);

        /// <summary>Indicates whether a character is within the specified inclusive range.</summary>
        /// <param name="c">The character to evaluate.</param>
        /// <param name="minInclusive">The lower bound, inclusive.</param>
        /// <param name="maxInclusive">The upper bound, inclusive.</param>
        /// <returns>true if <paramref name="c"/> is within the specified range; otherwise, false.</returns>
        /// <remarks>
        /// The method does not validate that <paramref name="maxInclusive"/> is greater than or equal
        /// to <paramref name="minInclusive"/>.  If <paramref name="maxInclusive"/> is less than
        /// <paramref name="minInclusive"/>, the behavior is undefined.
        /// </remarks>
        public static bool IsBetween(char c, char minInclusive, char maxInclusive) =>
            (uint)(c - minInclusive) <= (uint)(maxInclusive - minInclusive);
    }
}
