// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Some code is from the .NET codebase, with minor modifications for clarity. See comments inline.
// Original license header:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki;

/// <summary>
///  Helper methods for <see cref="char"/> types.
/// </summary>
/// <remarks>
///  <para>
///   Provides extension method access to <see cref="char"/> functionality that is not available otherwise
///   on .NET Framework.
///  </para>
/// </remarks>
public static class CharExtensions
{
    /// <summary>Indicates whether a character is categorized as an ASCII letter.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is an ASCII letter; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range 'A' through 'Z', inclusive,
    /// or 'a' through 'z', inclusive.
    /// </remarks>
    public static bool IsAsciiLetter(this char c) =>
#if NETFRAMEWORK
        (uint)((c | 0x20) - 'a') <= 'z' - 'a';
#else
        char.IsAsciiLetter(c);
#endif

    /// <summary>Indicates whether a character is categorized as a lowercase ASCII letter.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is a lowercase ASCII letter; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range 'a' through 'z', inclusive.
    /// </remarks>
    public static bool IsAsciiLetterLower(this char c) =>
#if NETFRAMEWORK
        IsBetween(c, 'a', 'z');
#else
        char.IsAsciiLetterLower(c);
#endif

    /// <summary>Indicates whether a character is categorized as an uppercase ASCII letter.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is an uppercase ASCII letter; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range 'A' through 'Z', inclusive.
    /// </remarks>
    public static bool IsAsciiLetterUpper(this char c) =>
#if NETFRAMEWORK
        IsBetween(c, 'A', 'Z');
#else
        char.IsAsciiLetterUpper(c);
#endif

    /// <summary>Indicates whether a character is categorized as an ASCII digit.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is an ASCII digit; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range '0' through '9', inclusive.
    /// </remarks>
    public static bool IsAsciiDigit(this char c) =>
#if NETFRAMEWORK
        IsBetween(c, '0', '9');
#else
        char.IsAsciiDigit(c);
#endif

    /// <summary>Indicates whether a character is categorized as an ASCII letter or digit.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is an ASCII letter or digit; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range 'A' through 'Z', inclusive,
    /// 'a' through 'z', inclusive, or '0' through '9', inclusive.
    /// </remarks>
    public static bool IsAsciiLetterOrDigit(this char c) =>
#if NETFRAMEWORK
    IsAsciiLetter(c) | IsBetween(c, '0', '9');
#else
        char.IsAsciiLetterOrDigit(c);
#endif

    /// <summary>Indicates whether a character is categorized as an ASCII hexadecimal digit.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is a hexadecimal digit; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range '0' through '9', inclusive,
    /// 'A' through 'F', inclusive, or 'a' through 'f', inclusive.
    /// </remarks>
    public static bool IsAsciiHexDigit(this char c) =>
#if NETFRAMEWORK
        System.HexConverter.IsHexChar(c);
#else
        char.IsAsciiHexDigit(c);
#endif

    /// <summary>Indicates whether a character is categorized as an ASCII upper-case hexadecimal digit.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is a hexadecimal digit; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range '0' through '9', inclusive,
    /// or 'A' through 'F', inclusive.
    /// </remarks>
    public static bool IsAsciiHexDigitUpper(this char c) =>
#if NETFRAMEWORK
        System.HexConverter.IsHexUpperChar(c);
#else
        char.IsAsciiHexDigitUpper(c);
#endif

    /// <summary>Indicates whether a character is categorized as an ASCII lower-case hexadecimal digit.</summary>
    /// <param name="c">The character to evaluate.</param>
    /// <returns>true if <paramref name="c"/> is a lower-case hexadecimal digit; otherwise, false.</returns>
    /// <remarks>
    /// This determines whether the character is in the range '0' through '9', inclusive,
    /// or 'a' through 'f', inclusive.
    /// </remarks>
    public static bool IsAsciiHexDigitLower(this char c) =>
#if NETFRAMEWORK
        System.HexConverter.IsHexLowerChar(c);
#else
        char.IsAsciiHexDigitLower(c);
#endif

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
    public static bool IsBetween(this char c, char minInclusive, char maxInclusive) =>
#if NETFRAMEWORK
        (uint)(c - minInclusive) <= (uint)(maxInclusive - minInclusive);
#else
        char.IsBetween(c, minInclusive, maxInclusive);
#endif

    /// <summary>
    ///  Tries to convert a hexadecimal character to its integer value.
    /// </summary>
    public static bool TryDecodeHexDigit(this char c, out int digit)
    {
        digit = HexConverter.FromChar(c);
        return digit != 0xFF;
    }
}
