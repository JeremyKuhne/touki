// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Security.Cryptography;

/// <summary>
///  Polyfill for <see cref="Cryptography"/> operations.
/// </summary>
public static class CryptographicOperations
{
    /// <summary>
    ///  Determines whether two read-only spans of bytes are equal in fixed time.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The runtime of this method is independent of the contents of the inputs (assuming
    ///   equal lengths). Use this method instead of <c>MemoryExtensions.SequenceEqual</c>
    ///   or equivalent to avoid timing side channels when comparing security tokens.
    ///  </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public static bool FixedTimeEquals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        // NoOptimization|NoInlining keeps the JIT from short-circuiting the loop. The early
        // length check is acceptable since differing lengths are not secret.
        if (left.Length != right.Length)
        {
            return false;
        }

        int length = left.Length;
        int accum = 0;

        for (int i = 0; i < length; i++)
        {
            accum |= left[i] - right[i];
        }

        return accum == 0;
    }
}
