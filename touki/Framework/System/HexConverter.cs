// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// From .NET codebase, with minor modifications for clarity. Original license header:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System;

internal static partial class HexConverter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexChar(int c)
    {
        if (IntPtr.Size == 8)
        {
            // This code path, when used, has no branches and doesn't depend on cache hits,
            // so it's faster and does not vary in speed depending on input data distribution.
            // We only use this logic on 64-bit systems, as using 64 bit values would otherwise
            // be much slower than just using the lookup table anyway (no hardware support).
            // The magic constant 18428868213665201664 is a 64 bit value containing 1s at the
            // indices corresponding to all the valid hex characters (ie. "0123456789ABCDEFabcdef")
            // minus 48 (ie. '0'), and backwards (so from the most significant bit and downwards).
            // The offset of 48 for each bit is necessary so that the entire range fits in 64 bits.
            // First, we subtract '0' to the input digit (after casting to uint to account for any
            // negative inputs). Note that even if this subtraction underflows, this happens before
            // the result is zero-extended to ulong, meaning that `i` will always have upper 32 bits
            // equal to 0. We then left shift the constant with this offset, and apply a bitmask that
            // has the highest bit set (the sign bit) if and only if `c` is in the ['0', '0' + 64) range.
            // Then we only need to check whether this final result is less than 0: this will only be
            // the case if both `i` was in fact the index of a set bit in the magic constant, and also
            // `c` was in the allowed range (this ensures that false positive bit shifts are ignored).
            ulong i = (uint)c - '0';
            ulong shift = 18428868213665201664UL << (int)i;
            ulong mask = i - 64;

            return (long)(shift & mask) < 0;
        }

        return FromChar(c) != 0xFF;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexUpperChar(int c)
    {
        return (uint)(c - '0') <= 9 || (uint)(c - 'A') <= ('F' - 'A');
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsHexLowerChar(int c)
    {
        return (uint)(c - '0') <= 9 || (uint)(c - 'a') <= ('f' - 'a');
    }
}
