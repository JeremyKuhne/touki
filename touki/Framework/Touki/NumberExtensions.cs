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
///  Numeric extensions for various types, including <see cref="decimal"/>, <see cref="float"/>, and <see cref="double"/>.
/// </summary>
public static unsafe class NumberExtensions
{
    /// <summary>Determines whether the specified value is negative.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNegative(this decimal d)
    {
        DecimalFields* p = (DecimalFields*)&d;
        return (p->_flags & 0x80000000) != 0;
    }

    /// <summary>
    ///  Low bits of the decimal value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Low(this decimal d)
    {
        DecimalFields* p = (DecimalFields*)&d;
        return p->_lo;
    }

    /// <summary>
    ///  Mid bits of the decimal value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Mid(this decimal d)
    {
        DecimalFields* p = (DecimalFields*)&d;
        return p->_mid;
    }

    /// <summary>
    ///  High bits of the decimal value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint High(this decimal d)
    {
        DecimalFields* p = (DecimalFields*)&d;
        return p->_hi;
    }

    /// <summary>
    ///  Returns the scale of the decimal value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Scale(this decimal d)
    {
        DecimalFields* p = (DecimalFields*)&d;
        return (int)(p->_flags & 0x00FF0000) >> 16; // Extracting the scale from the flags
    }

#pragma warning disable CS0649 // Field 'DecimalFields._flags' is never assigned to, and will always have its default value 0
    private struct DecimalFields
    {
        // Matching the layout of the decimal type in .NET Framework.
        internal uint _flags;
        internal uint _hi;
        internal uint _lo;
        internal uint _mid;
    }
#pragma warning restore CS0649

    /// <summary>
    ///  Divides the specified decimal value by 10^9 (1,000,000,000), updates the decimal
    ///  with the quotient, and returns the remainder as a uint.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This is primarily used for decimal formatting and arithmetic operations.
    ///  </para>
    ///  <para>
    ///   Taken from .NET DecCalc struct.
    ///  </para>
    /// </remarks>
    internal static uint DecDivMod1E9(this ref decimal value)
    {
        const uint TenToPowerNine = 1000000000;

        fixed (decimal* pValue = &value)
        {
            DecimalFields* pFields = (DecimalFields*)pValue;

            ulong high64 = ((ulong)pFields->_hi << 32) + pFields->_mid;
            ulong div64 = high64 / TenToPowerNine;
            pFields->_hi = (uint)(div64 >> 32);
            pFields->_mid = (uint)div64;

            ulong num = ((high64 - (uint)div64 * TenToPowerNine) << 32) + pFields->_lo;
            uint div = (uint)(num / TenToPowerNine);
            pFields->_lo = div;
            return (uint)num - div * TenToPowerNine;
        }
    }

    // From here forward, code is from the .NET codebase, with minor modifications for clarity.

    /// <summary>Determines whether the specified value is finite (zero, subnormal, or normal).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(this float f)
    {
        int bits = *(int*)&f;
        return (bits & 0x7FFFFFFF) < 0x7F800000;
    }

    /// <summary>Determines whether the specified value is finite (zero, subnormal, or normal).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(this double d)
    {
        long bits = BitConverter.DoubleToInt64Bits(d);
        return (bits & 0x7FFFFFFFFFFFFFFF) < 0x7FF0000000000000;
    }

    /// <summary>Determines whether the specified value is negative.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNegative(this float f) => (*(int*)&f) < 0;

    /// <summary>Determines whether the specified value is negative.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNegative(this double d) => BitConverter.DoubleToInt64Bits(d) < 0;
}
