// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  Polyfill for <see cref="Random"/> span-based APIs.
/// </summary>
public static class RandomExtensions
{
    extension(Random random)
    {
        /// <summary>
        ///  Fills the elements of a specified span of bytes with random numbers.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   This polyfill tries to avoid allocating an intermediate <see langword="byte"/>
        ///   array but is slower than calling <see cref="Random.NextBytes(byte[])"/>
        ///   directly on .NET Framework. Prefer this overload when zero allocations
        ///   matter (e.g. on hot paths or when filling a stack-allocated span); prefer
        ///   the array overload when raw throughput matters and the allocation cost is
        ///   acceptable.
        ///  </para>
        /// </remarks>
        public unsafe void NextBytes(Span<byte> buffer)
        {
            ArgumentNullException.ThrowIfNull(random);

            if (buffer.IsEmpty)
            {
                return;
            }

            if (typeof(Random) == random.GetType())
            {
                // Pinned pointer loop avoids per-element bounds checks. The dominant
                // cost remains the virtual call to Random.Next(); pointer iteration
                // recovers most (but not all) of the gap vs the array overload.
                fixed (byte* pStart = &MemoryMarshal.GetReference(buffer))
                {
                    byte* p = pStart;
                    byte* end = pStart + buffer.Length;

                    while (p < end)
                    {
                        *p++ = (byte)(random.Next() % (byte.MaxValue + 1));
                    }
                }

                return;
            }

            // Subclass: forward to the array overload to honor any override of
            // NextBytes / Sample semantics. (Unlikely that anyone would override these.)
            byte[] temp = new byte[buffer.Length];
            random.NextBytes(temp);
            temp.AsSpan().CopyTo(buffer);
        }
    }
}
