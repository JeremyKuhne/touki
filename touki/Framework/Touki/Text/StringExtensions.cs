// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public static partial class StringExtensions
{
    extension(string)
    {
        /// <summary>
        ///  Generates a hash code for the specified string value that matches what <see langword="string"/> generates.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   On .NET Framework strings don't go beyond embedded nulls when calculating hash codes. If this matters to
        ///   you, you'll need to slice the span to the first null character. In addition, this is not a safe hashing
        ///   algorithm, it is not resistant to hash collisions, and should not be used for security purposes. It is meant
        ///   to give you a hash code that matches what <see langword="string"/> generates for the same value.
        ///  </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe int GetHashCode(ReadOnlySpan<char> value)
        {
            // .NET Framework uses the DJB2 (Daniel J. Bernstein) algorithm. It iterates through to the first null character.
            // Here we don't know if we'll have one so we use the length and unroll to get the next best thing. The speed
            // converges on rough equivalence with about 100 characters and above. At smaller sizes there is about a
            // 5ns overhead penalty.

            if (value.IsEmpty)
            {
                // "".GetHashCode();
                return 371857150;
            }

            fixed (char* ptr = value)
            {
                // For strings 10-100+ chars, unrolling by 4 provides best performance
                int hash1 = 5381;
                int hash2 = hash1;

                char* p = ptr;
                int remaining = value.Length;

                // Process 4 characters at a time
                while (remaining >= 4)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                    hash2 = ((hash2 << 5) + hash2) ^ p[1];
                    hash1 = ((hash1 << 5) + hash1) ^ p[2];
                    hash2 = ((hash2 << 5) + hash2) ^ p[3];

                    p += 4;
                    remaining -= 4;
                }

                // Handle remaining characters
                if (remaining == 3)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                    hash2 = ((hash2 << 5) + hash2) ^ p[1];
                    hash1 = ((hash1 << 5) + hash1) ^ p[2];
                }
                else if (remaining == 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                    hash2 = ((hash2 << 5) + hash2) ^ p[1];
                }
                else if (remaining == 1)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ p[0];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }
    }
}
