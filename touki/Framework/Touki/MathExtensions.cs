// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

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
    }
}
