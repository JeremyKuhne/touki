// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using static Microsoft.IO.StringExtensions;

namespace Touki.Text;

public static partial class StringExtensions
{
    extension(string stringValue)
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

        /// <summary>
        ///  Creates a new <see langword="string"/> with a specific length and initializes it after creation by
        ///  using the specified callback.
        /// </summary>
        /// <typeparam name="TState">Type of the state object to pass to <paramref name="action"/>.</typeparam>
        /// <param name="length">The length of the string to create.</param>
        /// <param name="state">The state object to pass to <paramref name="action"/>.</param>
        /// <param name="action">A callback to initialize the string.</param>
        /// <returns>The newly created <see langword="string"/>.</returns>
        public static unsafe string Create<TState>(int length, TState state, SpanAction<char, TState> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            ArgumentOutOfRangeException.ThrowIfNegative(length);

            if (length == 0)
            {
                return string.Empty;
            }

            string result = FastAllocateString(length);

            fixed (char* ptr = result)
            {
                action(new Span<char>(ptr, length), state);
            }

            return result;
        }

        /// <summary>
        ///  Creates a new string by using the specified provider to control the formatting of the specified interpolated string.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="handler">The interpolated string.</param>
        /// <returns>The string that results for formatting the interpolated string using the specified format provider.</returns>
        public static string Create(
            IFormatProvider? provider,
            [InterpolatedStringHandlerArgument("provider")] ref DefaultInterpolatedStringHandler handler) =>
            handler.ToStringAndClear();

        /// <summary>
        ///  Creates a new string by using the specified provider to control the formatting of the specified interpolated string.
        /// </summary>
        /// <param name="provider">An object that supplies culture-specific formatting information.</param>
        /// <param name="initialBuffer">
        ///  The initial buffer that may be used as temporary space as part of the formatting operation.
        ///  The contents of this buffer may be overwritten.
        /// </param>
        /// <param name="handler">The interpolated string.</param>
        /// <returns>The string that results for formatting the interpolated string using the specified format provider.</returns>
        public static string Create(
            IFormatProvider? provider,
            Span<char> initialBuffer,
            [InterpolatedStringHandlerArgument("provider", "initialBuffer")] ref DefaultInterpolatedStringHandler handler) =>
            handler.ToStringAndClear();

        /// <summary>
        ///  Copies the contents of this string into the destination span.
        /// </summary>
        /// <param name="destination">The span into which to copy this string's contents.</param>
        /// <exception cref="ArgumentException">The destination span is shorter than the source string.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(Span<char> destination)
        {
            if (destination.Length < stringValue.Length)
            {
                throw new ArgumentException("Destination span is too short to copy the string.", nameof(destination));
            }

            stringValue.AsSpan().CopyTo(destination);
        }

        /// <summary>
        ///  Copies the contents of this string into the destination span.
        /// </summary>
        /// <param name="destination">The span to copy the string into.</param>
        /// <returns>
        ///  <see langword="true"/> if the data was copied;
        ///  <see langword="false"/> if the destination was too short to fit the contents of the string.
        /// </returns>
        public bool TryCopyTo(Span<char> destination)
        {
            if (destination.Length < stringValue.Length)
            {
                return false;
            }

            stringValue.AsSpan().CopyTo(destination);
            return true;
        }
    }
}
