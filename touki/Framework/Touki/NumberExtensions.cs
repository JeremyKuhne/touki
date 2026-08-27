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
public static unsafe partial class NumberExtensions
{
    /// <param name="decimalValue">The decimal receiver.</param>
    extension(decimal decimalValue)
    {
        /// <summary>
        ///  Determines whether the specified value is negative.
        /// </summary>
        /// <param name="value">The decimal value to inspect.</param>
        /// <returns><see langword="true"/> if the sign bit is set; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNegative(decimal value)
        {
            DecimalFields* p = (DecimalFields*)&value;
            return (p->_flags & 0x80000000) != 0;
        }

        // The next three properties (Low, Mid, High) replicate internal decimal properties.
        // Exposed as methods to avoid name ambiguity issues.

        /// <summary>
        ///  Low bits of the decimal value.
        /// </summary>
        /// <returns>The low 32 bits of the decimal value's 96-bit integer representation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Low()
        {
            DecimalFields* p = (DecimalFields*)&decimalValue;
            return p->_lo;
        }

        /// <summary>
        ///  Mid bits of the decimal value.
        /// </summary>
        /// <returns>The middle 32 bits of the decimal value's 96-bit integer representation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint Mid()
        {
            DecimalFields* p = (DecimalFields*)&decimalValue;
            return p->_mid;
        }

        /// <summary>
        ///  High bits of the decimal value.
        /// </summary>
        /// <returns>The high 32 bits of the decimal value's 96-bit integer representation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint High()
        {
            DecimalFields* p = (DecimalFields*)&decimalValue;
            return p->_hi;
        }

        /// <summary>
        ///  Gets the scaling factor of the decimal, which is a number from 0 to 28 that represents
        ///  the number of decimal digits.
        /// </summary>
        public byte Scale
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                DecimalFields* p = (DecimalFields*)&decimalValue;
                return (byte)((p->_flags & 0x00FF0000) >> 16); // Extracting the scale from the flags
            }
        }

        /// <summary>
        ///  Divides the specified decimal value by 10^9 (1,000,000,000), updates the decimal
        ///  with the quotient, and returns the remainder as a uint.
        /// </summary>
        /// <param name="value">The decimal value to replace with the quotient.</param>
        /// <returns>The remainder after division by 1,000,000,000.</returns>
        /// <remarks>
        ///  <para>
        ///   This is primarily used for decimal formatting and arithmetic operations.
        ///  </para>
        ///  <para>
        ///   Taken from .NET DecCalc struct.
        ///  </para>
        /// </remarks>
        internal static uint DecDivMod1E9(ref decimal value)
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
    }

    // From here forward, code is from the .NET codebase, with minor modifications for clarity.

    /// <param name="floatValue">The single-precision value to inspect.</param>
    extension(float floatValue)
    {
        /// <summary>
        ///  Determines whether the specified value is finite (zero, subnormal, or normal).
        /// </summary>
        /// <returns><see langword="true"/> if the value is finite; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFinite()
        {
            int bits = *(int*)&floatValue;
            return (bits & 0x7FFFFFFF) < 0x7F800000;
        }

        /// <summary>
        ///  Determines whether the specified value is negative.
        /// </summary>
        /// <returns><see langword="true"/> if the sign bit is set; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNegative() => (*(int*)&floatValue) < 0;
    }

    /// <param name="doubleValue">The double-precision value to inspect.</param>
    extension(double doubleValue)
    {
        /// <summary>
        ///  Determines whether the specified value is finite (zero, subnormal, or normal).
        /// </summary>
        /// <returns><see langword="true"/> if the value is finite; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsFinite()
        {
            long bits = BitConverter.DoubleToInt64Bits(doubleValue);
            return (bits & 0x7FFFFFFFFFFFFFFF) < 0x7FF0000000000000;
        }

        /// <summary>
        ///  Determines whether the specified value is negative.
        /// </summary>
        /// <returns><see langword="true"/> if the sign bit is set; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNegative() => BitConverter.DoubleToInt64Bits(doubleValue) < 0;
    }
}
