// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

/// <summary>
///  Helpers for working with <see cref="string"/> and <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
/// </summary>
public static partial class StringExtensions
{
    private static Random? s_defaultRandom
#if NET
        = Random.Shared
#endif
        ;

    extension(string)
    {
        /// <summary>
        ///  Allocates a string of the specified length filled with null characters.
        /// </summary>
        internal static string FastAllocateString(int length) =>
            // This calls FastAllocateString in the runtime, with extra checks.
            new string('\0', length);

        /// <summary>
        ///  Creates a formatted string without boxing primitive arguments.
        /// </summary>
        /// <param name="format">The format string.</param>
        /// <param name="arg">The argument to format.</param>
        /// <remarks>
        ///  <para>
        ///  </para>
        /// </remarks>
        [SkipLocalsInit]
        public static string FormatValue<TArgument>(ReadOnlySpan<char> format, TArgument arg) where TArgument : unmanaged
        {
            Span<char> buffer = stackalloc char[256];
            using ValueStringBuilder builder = new(buffer);
            builder.AppendFormat(format, arg);
            return builder.ToString();
        }

        /// <inheritdoc cref="FormatValue{TArgument}(ReadOnlySpan{char}, TArgument)" />
        [SkipLocalsInit]
        public static string FormatValue(ReadOnlySpan<char> format, Value arg)
        {
            Span<char> buffer = stackalloc char[256];
            using ValueStringBuilder builder = new(buffer);
            builder.AppendFormat(format, arg);
            return builder.ToString();
        }

        /// <inheritdoc cref="FormatValue{TArgument}(ReadOnlySpan{char}, TArgument)" />
        /// <param name="args">The arguments to format.</param>
        [SkipLocalsInit]
        public static string FormatValues(ReadOnlySpan<char> format, ReadOnlySpan<Value> args)
        {
            Span<char> buffer = stackalloc char[256];
            using ValueStringBuilder builder = new(buffer);
            builder.AppendFormat(format, args);
            return builder.ToString();
        }

        /// <inheritdoc cref="FormatValues(ReadOnlySpan{char}, Value, Value, Value, Value)" />
        [SkipLocalsInit]
        public static string FormatValues(ReadOnlySpan<char> format, Value arg1, Value arg2)
        {
            Span<char> buffer = stackalloc char[256];
            using ValueStringBuilder builder = new(buffer);
            builder.AppendFormat(format, arg1, arg2);
            return builder.ToString();
        }

        /// <inheritdoc cref="FormatValues(ReadOnlySpan{char}, Value, Value, Value, Value)" />
        [SkipLocalsInit]
        public static string FormatValues(ReadOnlySpan<char> format, Value arg1, Value arg2, Value arg3)
        {
            Span<char> buffer = stackalloc char[256];
            using ValueStringBuilder builder = new(buffer);
            builder.AppendFormat(format, arg1, arg2, arg3);
            return builder.ToString();
        }

        /// <inheritdoc cref="FormatValue{TArgument}(ReadOnlySpan{char}, TArgument)"/>
        /// <param name="arg1">The first argument to format.</param>
        /// <param name="arg2">The second argument to format.</param>
        /// <param name="arg3">The third argument to format.</param>
        /// <param name="arg4">The fourth argument to format.</param>
        [SkipLocalsInit]
        public static string FormatValues(ReadOnlySpan<char> format, Value arg1, Value arg2, Value arg3, Value arg4)
        {
            Span<char> buffer = stackalloc char[256];
            using ValueStringBuilder builder = new(buffer);
            builder.AppendFormat(format, arg1, arg2, arg3, arg4);
            return builder.ToString();
        }

        /// <summary>
        ///  Generates <paramref name="count"/> random UTF-16 strings.
        /// </summary>
        /// <param name="minLength">
        ///  Minimum length of the generated strings in UTF-16 code units, inclusive.
        /// </param>
        /// <param name="maxLength">
        ///  Maximum length of the generated strings in UTF-16 code units, inclusive.
        /// </param>
        /// <remarks>
        ///  <para>
        ///   Length bounds are inclusive and measure UTF-16 code units (i.e., C# char count).
        ///  </para>
        ///  <para>
        ///   Control characters and U+0000 are excluded. Surrogate pairs are emitted only if enabled.
        ///  </para>
        /// </remarks>
        public static List<string> GenerateRandomStrings(
            int count,
            int minLength = 4,
            int maxLength = 40,
            bool allowSurrogatePairs = false,
            Random? random = null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(count, 0, nameof(count));
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minLength, 0, nameof(minLength));
            ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, minLength, nameof(maxLength));

            random ??= s_defaultRandom ??= new Random();
            List<string> result = new(count);
            using BufferScope<char> buffer = new(stackalloc char[128], maxLength);

            for (int i = 0; i < count; i++)
            {
#pragma warning disable CA5394 // Do not use insecure randomness
                int length = random.Next(minLength, maxLength + 1);
                int position = 0;

                while (position < length)
                {
                    // Optionally emit a surrogate pair (~25% chance) if space allows.
                    if (!allowSurrogatePairs || (length - position) < 2 || random.Next(4) != 0)
                    {
                        buffer[position++] = char.GetRandomSimpleChar(random);
                        continue;
                    }

                    int codePoint;

                    do
                    {
                        // 0..0xFFFFF, skip ?FFFE/?FFFF in every plane.
                        codePoint = 0x10000 + random.Next(0x110000 - 0x10000);
                    } while ((codePoint & 0xFFFE) == 0xFFFE);

                    codePoint -= 0x10000;

                    // High surrogate
                    buffer[position++] = (char)(0xD800 + (codePoint >> 10));

                    // Low surrogate
                    buffer[position++] = (char)(0xDC00 + (codePoint & 0x3FF));
                }

                string s = buffer[..length].ToString();
                result.Add(s);
            }

            return result;
#pragma warning restore CA5394 // Do not use insecure randomness
        }
    }
}
