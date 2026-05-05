// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  <see cref="float"/> static methods that don't have a direct equivalent in the .NET Framework build.
/// </summary>
public static class SingleExtensions
{
    extension(float)
    {
        /// <summary>Determines whether the specified value is finite (zero, subnormal, or normal).</summary>
        public static bool IsFinite(float f)
        {
            int bits = BitConverter.SingleToInt32Bits(f);
            return (bits & 0x7FFFFFFF) < 0x7F800000;
        }

        /// <summary>Determines whether the specified value is negative.</summary>
        public static bool IsNegative(float f) => BitConverter.SingleToInt32Bits(f) < 0;

        /// <summary>Determines whether the specified value is normal.</summary>
        public static bool IsNormal(float f)
        {
            int bits = BitConverter.SingleToInt32Bits(f) & 0x7FFFFFFF;
            return (bits < 0x7F800000) && (bits != 0) && ((bits & 0x7F800000) != 0);
        }

        /// <summary>Determines whether the specified value is subnormal.</summary>
        public static bool IsSubnormal(float f)
        {
            int bits = BitConverter.SingleToInt32Bits(f) & 0x7FFFFFFF;
            return (bits < 0x7F800000) && (bits != 0) && ((bits & 0x7F800000) == 0);
        }

        /// <summary>Determines whether the specified value is positive.</summary>
        public static bool IsPositive(float f) => BitConverter.SingleToInt32Bits(f) >= 0;

        /// <summary>Determines whether the specified value represents a real number (i.e. not NaN).</summary>
        public static bool IsRealNumber(float f) => !float.IsNaN(f);

        /// <summary>Determines whether the specified value is integral (finite and equal to its truncation).</summary>
        public static bool IsInteger(float f) => float.IsFinite(f) && (f == (float)Math.Truncate((double)f));

        /// <summary>Determines whether the specified value is integral and even.</summary>
        public static bool IsEvenInteger(float f) => float.IsInteger(f) && (Math.Abs(f % 2f) == 0f);

        /// <summary>Determines whether the specified value is integral and odd.</summary>
        public static bool IsOddInteger(float f) => float.IsInteger(f) && (Math.Abs(f % 2f) == 1f);

        /// <summary>Determines whether the specified value is a power of two.</summary>
        public static bool IsPow2(float f)
        {
            int bits = BitConverter.SingleToInt32Bits(f);
            if (bits <= 0)
            {
                return false;
            }

            int exponent = (bits >> 23) & 0xFF;
            int mantissa = bits & 0x007FFFFF;

            if (exponent == 0)
            {
                return mantissa != 0 && (mantissa & (mantissa - 1)) == 0;
            }

            if (exponent == 0xFF)
            {
                return false;
            }

            return mantissa == 0;
        }
    }
}
