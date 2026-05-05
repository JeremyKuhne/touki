// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  <see cref="Math"/> methods that don't have a direct equivalent in the .NET Framework build.
/// </summary>
public static class MathExtensions
{
    extension(Math)
    {
        /// <summary>Produces the quotient and the remainder of two signed 32-bit numbers.</summary>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns>The quotient and the remainder of the specified numbers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (int Quotient, int Remainder) DivRem(int left, int right)
        {
            // Manually doing the math to facilitate better optimization by the JIT.
            //
            // int quotient = Math.DivRem(left, right, out int remainder);
            // return (quotient, remainder);

            int quotient = left / right;
            return (quotient, left - (quotient * right));
        }

        /// <summary>Produces the quotient and the remainder of two unsigned 32-bit numbers.</summary>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns>The quotient and the remainder of the specified numbers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (uint Quotient, uint Remainder) DivRem(uint left, uint right)
        {
            uint quotient = left / right;
            return (quotient, left - (quotient * right));
        }

        /// <summary>Produces the quotient and the remainder of two unsigned 64-bit numbers.</summary>
        /// <param name="left">The dividend.</param>
        /// <param name="right">The divisor.</param>
        /// <returns>The quotient and the remainder of the specified numbers.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ulong Quotient, ulong Remainder) DivRem(ulong left, ulong right)
        {
            ulong quotient = left / right;
            return (quotient, left - (quotient * right));
        }

        /// <inheritdoc cref="DivRem(int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (long Quotient, long Remainder) DivRem(long left, long right)
        {
            long quotient = left / right;
            return (quotient, left - (quotient * right));
        }

        /// <inheritdoc cref="DivRem(int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (sbyte Quotient, sbyte Remainder) DivRem(sbyte left, sbyte right)
        {
            sbyte quotient = (sbyte)(left / right);
            return (quotient, (sbyte)(left - (quotient * right)));
        }

        /// <inheritdoc cref="DivRem(int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (byte Quotient, byte Remainder) DivRem(byte left, byte right)
        {
            byte quotient = (byte)(left / right);
            return (quotient, (byte)(left - (quotient * right)));
        }

        /// <inheritdoc cref="DivRem(int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (short Quotient, short Remainder) DivRem(short left, short right)
        {
            short quotient = (short)(left / right);
            return (quotient, (short)(left - (quotient * right)));
        }

        /// <inheritdoc cref="DivRem(int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ushort Quotient, ushort Remainder) DivRem(ushort left, ushort right)
        {
            ushort quotient = (ushort)(left / right);
            return (quotient, (ushort)(left - (quotient * right)));
        }

        /// <summary>Returns <paramref name="value"/> clamped to the inclusive range of <paramref name="min"/> and <paramref name="max"/>.</summary>
        /// <param name="value">The value to be clamped.</param>
        /// <param name="min">The lower bound of the result.</param>
        /// <param name="max">The upper bound of the result.</param>
        public static byte Clamp(byte value, byte min, byte max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static sbyte Clamp(sbyte value, sbyte min, sbyte max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static short Clamp(short value, short min, short max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static ushort Clamp(ushort value, ushort min, ushort max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static int Clamp(int value, int min, int max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static uint Clamp(uint value, uint min, uint max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static long Clamp(long value, long min, long max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static ulong Clamp(ulong value, ulong min, ulong max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static float Clamp(float value, float min, float max)
        {
            // BCL semantics: NaN propagates; if min > max throws.
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static double Clamp(double value, double min, double max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <inheritdoc cref="Clamp(byte, byte, byte)"/>
        public static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            return value < min ? min : value > max ? max : value;
        }

        /// <summary>Returns the angle whose hyperbolic cosine is the specified number.</summary>
        public static double Acosh(double d) => Math.Log(d + Math.Sqrt((d * d) - 1.0));

        /// <summary>Returns the angle whose hyperbolic sine is the specified number.</summary>
        public static double Asinh(double d)
        {
            // Equivalent to Log(d + Sqrt(d * d + 1)) but more accurate for large |d|.
            if (double.IsNegativeInfinity(d))
            {
                return double.NegativeInfinity;
            }

            return Math.Log(d + Math.Sqrt((d * d) + 1.0));
        }

        /// <summary>Returns the angle whose hyperbolic tangent is the specified number.</summary>
        public static double Atanh(double d) => 0.5 * Math.Log((1.0 + d) / (1.0 - d));

        /// <summary>Returns the cube root of a specified number.</summary>
        public static double Cbrt(double d)
        {
            if (d < 0)
            {
                return -Math.Pow(-d, 1.0 / 3.0);
            }

            return Math.Pow(d, 1.0 / 3.0);
        }

        /// <summary>
        ///  Returns the next smallest value that compares less than <paramref name="x"/>.
        /// </summary>
        public static double BitDecrement(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);

            if (((bits >> 32) & 0x7FF00000) >= 0x7FF00000)
            {
                // NaN returns NaN; -Infinity returns -Infinity; +Infinity returns double.MaxValue.
                return bits == 0x7FF00000_00000000 ? double.MaxValue : x;
            }

            if (bits == 0x00000000_00000000)
            {
                // +0.0 returns -double.Epsilon
                return -double.Epsilon;
            }

            // Negate the magnitude when the sign is set, otherwise step down toward zero.
            bits += bits < 0 ? +1 : -1;
            return BitConverter.Int64BitsToDouble(bits);
        }

        /// <summary>
        ///  Returns the next largest value that compares greater than <paramref name="x"/>.
        /// </summary>
        public static double BitIncrement(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);

            if (((bits >> 32) & 0x7FF00000) >= 0x7FF00000)
            {
                // NaN returns NaN; +Infinity returns +Infinity; -Infinity returns double.MinValue.
                return bits == unchecked((long)0xFFF00000_00000000) ? double.MinValue : x;
            }

            if (bits == unchecked((long)0x80000000_00000000))
            {
                // -0.0 returns double.Epsilon
                return double.Epsilon;
            }

            bits += bits < 0 ? -1 : +1;
            return BitConverter.Int64BitsToDouble(bits);
        }

        /// <summary>
        ///  Returns a value with the magnitude of <paramref name="x"/> and the sign of <paramref name="y"/>.
        /// </summary>
        public static double CopySign(double x, double y)
        {
            const long signMask = unchecked((long)0x8000000000000000);
            long xbits = BitConverter.DoubleToInt64Bits(x);
            long ybits = BitConverter.DoubleToInt64Bits(y);
            return BitConverter.Int64BitsToDouble((xbits & ~signMask) | (ybits & signMask));
        }

        /// <summary>
        ///  Returns <c>(<paramref name="x"/> * <paramref name="y"/>) + <paramref name="z"/></c> rounded as one ternary operation.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   On .NET Framework this polyfill performs the multiplication and addition as
        ///   separate operations and is not a true fused multiply-add.
        ///  </para>
        /// </remarks>
        public static double FusedMultiplyAdd(double x, double y, double z) => (x * y) + z;

        /// <summary>
        ///  Returns the integer (base-2) logarithm of a specified number.
        /// </summary>
        public static int ILogB(double x)
        {
            if (double.IsNaN(x))
            {
                return int.MaxValue;
            }

            long bits = BitConverter.DoubleToInt64Bits(x);
            int exponent = (int)((bits >> 52) & 0x7FF);

            if (exponent == 0)
            {
                if ((bits & 0x000FFFFFFFFFFFFFL) == 0)
                {
                    // Zero
                    return int.MinValue;
                }

                // Subnormal: normalize.
                ulong significand = (ulong)(bits & 0x000FFFFFFFFFFFFFL);
                exponent = -1022;
                while ((significand & 0x0010000000000000UL) == 0)
                {
                    significand <<= 1;
                    exponent--;
                }

                return exponent;
            }

            if (exponent == 0x7FF)
            {
                // Infinity
                return int.MaxValue;
            }

            return exponent - 1023;
        }

        /// <summary>Returns the base-2 logarithm of a specified number.</summary>
        public static double Log2(double x) => Math.Log(x) / 0.6931471805599453; // ln(2)

        /// <summary>Returns the larger magnitude of two double-precision floating-point numbers.</summary>
        public static double MaxMagnitude(double x, double y)
        {
            double ax = Math.Abs(x);
            double ay = Math.Abs(y);

            if ((ax > ay) || double.IsNaN(ax))
            {
                return x;
            }

            if (ax == ay)
            {
                return double.IsNegative(x) ? y : x;
            }

            return y;
        }

        /// <summary>Returns the smaller magnitude of two double-precision floating-point numbers.</summary>
        public static double MinMagnitude(double x, double y)
        {
            double ax = Math.Abs(x);
            double ay = Math.Abs(y);

            if ((ax < ay) || double.IsNaN(ax))
            {
                return x;
            }

            if (ax == ay)
            {
                return double.IsNegative(x) ? x : y;
            }

            return y;
        }

        /// <summary>Returns x * 2^n efficiently.</summary>
        public static double ScaleB(double x, int n)
        {
            // Cap the multiplier to two safe steps to avoid intermediate over/underflow.
            const double Two1023 = 8.98846567431158e+307;
            const double TwoM1022 = 2.2250738585072014e-308;
            const double Two53 = 9007199254740992.0;

            double y = x;
            if (n > 1023)
            {
                y *= Two1023;
                n -= 1023;
                if (n > 1023)
                {
                    y *= Two1023;
                    n -= 1023;
                    if (n > 1023)
                    {
                        n = 1023;
                    }
                }
            }
            else if (n < -1022)
            {
                y *= TwoM1022 * Two53;
                n += 1022 - 53;
                if (n < -1022)
                {
                    y *= TwoM1022 * Two53;
                    n += 1022 - 53;
                    if (n < -1022)
                    {
                        n = -1022;
                    }
                }
            }

            double u = BitConverter.Int64BitsToDouble(((long)(0x3FF + n) << 52));
            return y * u;
        }

        /// <summary>Returns an estimate of the reciprocal of a specified number.</summary>
        public static double ReciprocalEstimate(double d) => 1.0 / d;

        /// <summary>Returns an estimate of the reciprocal square root of a specified number.</summary>
        public static double ReciprocalSqrtEstimate(double d) => 1.0 / Math.Sqrt(d);

        /// <summary>Returns the sine and cosine of the specified angle.</summary>
        public static (double Sin, double Cos) SinCos(double x) => (Math.Sin(x), Math.Cos(x));

        /// <summary>Produces the full product of two 32-bit signed numbers.</summary>
        public static long BigMul(int a, int b) => (long)a * b;

        /// <summary>Produces the full product of two 64-bit signed numbers, returning the lower 64 bits and the upper 64 bits via <paramref name="low"/>.</summary>
        public static long BigMul(long a, long b, out long low)
        {
            // Split each operand into 32-bit halves and recombine the 128-bit product.
            ulong ua = (ulong)a;
            ulong ub = (ulong)b;

            ulong al = ua & 0xFFFFFFFF;
            ulong ah = ua >> 32;
            ulong bl = ub & 0xFFFFFFFF;
            ulong bh = ub >> 32;

            ulong mull = al * bl;
            ulong t = (ah * bl) + (mull >> 32);
            ulong tl = (al * bh) + (t & 0xFFFFFFFF);

            low = unchecked((long)((tl << 32) | (mull & 0xFFFFFFFF)));
            long high = (long)((ah * bh) + (t >> 32) + (tl >> 32));

            // Sign-correct: if a or b were negative, subtract the other operand from the high.
            if (a < 0)
            {
                high -= b;
            }

            if (b < 0)
            {
                high -= a;
            }

            return high;
        }

        /// <summary>Produces the full product of two 64-bit unsigned numbers, returning the lower 64 bits and the upper 64 bits via <paramref name="low"/>.</summary>
        public static ulong BigMul(ulong a, ulong b, out ulong low)
        {
            ulong al = a & 0xFFFFFFFF;
            ulong ah = a >> 32;
            ulong bl = b & 0xFFFFFFFF;
            ulong bh = b >> 32;

            ulong mull = al * bl;
            ulong t = (ah * bl) + (mull >> 32);
            ulong tl = (al * bh) + (t & 0xFFFFFFFF);

            low = (tl << 32) | (mull & 0xFFFFFFFF);
            return (ah * bh) + (t >> 32) + (tl >> 32);
        }
    }

    [DoesNotReturn]
    private static void ThrowMinMaxException<T>(T min, T max) =>
        throw new ArgumentException($"'{min}' cannot be greater than {max}.");
}
