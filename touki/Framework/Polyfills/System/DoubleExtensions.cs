// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  <see cref="double"/> static methods that don't have a direct equivalent in the .NET Framework build.
/// </summary>
public static class DoubleExtensions
{
    extension(double)
    {
        /// <summary>Determines whether the specified value is finite (zero, subnormal, or normal).</summary>
        public static bool IsFinite(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            return (bits & 0x7FFFFFFFFFFFFFFFL) < 0x7FF0000000000000L;
        }

        /// <summary>Determines whether the specified value is negative.</summary>
        public static bool IsNegative(double d) => BitConverter.DoubleToInt64Bits(d) < 0;

        /// <summary>Determines whether the specified value is normal.</summary>
        public static bool IsNormal(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d) & 0x7FFFFFFFFFFFFFFFL;
            return (bits < 0x7FF0000000000000L) && (bits != 0) && ((bits & 0x7FF0000000000000L) != 0);
        }

        /// <summary>Determines whether the specified value is subnormal.</summary>
        public static bool IsSubnormal(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d) & 0x7FFFFFFFFFFFFFFFL;
            return (bits < 0x7FF0000000000000L) && (bits != 0) && ((bits & 0x7FF0000000000000L) == 0);
        }

        /// <summary>Determines whether the specified value is positive.</summary>
        public static bool IsPositive(double d) => BitConverter.DoubleToInt64Bits(d) >= 0;

        /// <summary>Determines whether the specified value represents a real number (i.e. not NaN).</summary>
        public static bool IsRealNumber(double d) => !double.IsNaN(d);

        /// <summary>Determines whether the specified value is integral (finite and equal to its truncation).</summary>
        public static bool IsInteger(double d) => double.IsFinite(d) && (d == Math.Truncate(d));

        /// <summary>Determines whether the specified value is integral and even.</summary>
        public static bool IsEvenInteger(double d) => double.IsInteger(d) && (Math.Abs(d % 2.0) == 0.0);

        /// <summary>Determines whether the specified value is integral and odd.</summary>
        public static bool IsOddInteger(double d) => double.IsInteger(d) && (Math.Abs(d % 2.0) == 1.0);

        /// <summary>Determines whether the specified value is a power of two.</summary>
        public static bool IsPow2(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            // Positive, finite, normal or subnormal, mantissa is zero (i.e. value is 2^n).
            if (bits <= 0)
            {
                return false;
            }

            int exponent = (int)((bits >> 52) & 0x7FF);
            long mantissa = bits & 0x000FFFFFFFFFFFFFL;

            if (exponent == 0)
            {
                // Subnormal: must be a single-bit mantissa.
                return mantissa != 0 && (mantissa & (mantissa - 1)) == 0;
            }

            if (exponent == 0x7FF)
            {
                // Infinity / NaN.
                return false;
            }

            return mantissa == 0;
        }
    }
}
