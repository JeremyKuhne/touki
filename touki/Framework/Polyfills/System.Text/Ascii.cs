// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Text;

/// <summary>
///  Polyfill for the .NET 8+ <c>System.Text.Ascii</c> static helper class. Provides
///  ASCII-only span comparison primitives that match the modern .NET surface byte-for-byte.
/// </summary>
/// <remarks>
///  <para>
///   Only the overloads touki currently consumes are polyfilled. Adding new overloads as
///   they become needed is encouraged; match the BCL signatures and semantics exactly.
///  </para>
/// </remarks>
public static class Ascii
{
    /// <summary>
    ///  Determines whether the provided buffers contain equal ASCII characters, ignoring
    ///  case considerations.
    /// </summary>
    /// <param name="left">The buffer to compare with <paramref name="right"/>.</param>
    /// <param name="right">The buffer to compare with <paramref name="left"/>.</param>
    /// <returns>
    ///  <see langword="true"/> if the corresponding elements in <paramref name="left"/>
    ///  and <paramref name="right"/> are equal ignoring case considerations and ASCII;
    ///  <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   <b>If both buffers contain equal, but non-ASCII characters, the method returns
    ///   <see langword="false"/>.</b> This matches the modern .NET BCL contract and is
    ///   the entire reason this helper is distinct from
    ///   <c>MemoryExtensions.Equals(span, span, StringComparison.OrdinalIgnoreCase)</c>,
    ///   which folds non-ASCII characters under invariant-culture rules.
    ///  </para>
    ///  <para>
    ///   On net472/net481 the loop walks the spans through a hoisted <c>ref char</c>
    ///   (see <c>docs/framework-span-performance.md</c>, Strategy B). On net10 the BCL
    ///   ships the vectorized implementation; this polyfill is excluded from that build.
    ///  </para>
    /// </remarks>
    public static bool EqualsIgnoreCase(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        int length = left.Length;
        ref char pa = ref MemoryMarshal.GetReference(left);
        ref char pb = ref MemoryMarshal.GetReference(right);

        for (int i = 0; i < length; i++)
        {
            char x = Unsafe.Add(ref pa, i);
            char y = Unsafe.Add(ref pb, i);

            // Either side non-ASCII -> false (strict ASCII semantics).
            if (((uint)x | y) > 0x7F)
            {
                return false;
            }

            if (x == y)
            {
                continue;
            }

            // x and y differ. They can only compare equal under ASCII case-fold if
            // x | 0x20 is in 'a'..'z' (forcing both into the lowercase ASCII range)
            // and the folded values match. Because x | 0x20 == y | 0x20 implies both
            // sides fold into the same character, checking one side's letter range
            // is sufficient.
            if ((uint)((x | 0x20) - 'a') > 'z' - 'a'
                || (x | 0x20) != (y | 0x20))
            {
                return false;
            }
        }

        return true;
    }
}
