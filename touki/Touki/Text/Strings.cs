// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki;

/// <summary>
///  Supports string operations and utilities.
/// </summary>
public static partial class Strings
{
    private static Random? s_defaultRandom
#if NET
        = Random.Shared
#endif
        ;

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    public static string Format<TArgument>(string format, TArgument arg) where TArgument : unmanaged
        => Format(format.AsSpan(), arg);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    [SkipLocalsInit]
    public static string Format<TArgument>(ReadOnlySpan<char> format, TArgument arg) where TArgument : unmanaged
    {
        Span<char> buffer = stackalloc char[256];
        using ValueStringBuilder builder = new(buffer);
        builder.AppendFormat(format, arg);
        return builder.ToString();
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    public static string Format(string format, Value arg)
        => Format(format.AsSpan(), arg);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    [SkipLocalsInit]
    public static string Format(ReadOnlySpan<char> format, Value arg)
    {
        Span<char> buffer = stackalloc char[256];
        using ValueStringBuilder builder = new(buffer);
        builder.AppendFormat(format, arg);
        return builder.ToString();
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    public static string Format(string format, ReadOnlySpan<Value> args)
        => Format(format.AsSpan(), args);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    [SkipLocalsInit]
    public static string Format(ReadOnlySpan<char> format, ReadOnlySpan<Value> args)
    {
        Span<char> buffer = stackalloc char[256];
        using ValueStringBuilder builder = new(buffer);
        builder.AppendFormat(format, args);
        return builder.ToString();
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value)" />
    public static string Format(string format, Value arg1, Value arg2) => Format(format.AsSpan(), arg1, arg2);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value)" />
    [SkipLocalsInit]
    public static string Format(ReadOnlySpan<char> format, Value arg1, Value arg2)
    {
        Span<char> buffer = stackalloc char[256];
        using ValueStringBuilder builder = new(buffer);
        builder.AppendFormat(format, arg1, arg2);
        return builder.ToString();
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value)" />
    public static string Format(string format, Value arg1, Value arg2, Value arg3) =>
        Format(format.AsSpan(), arg1, arg2, arg3);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value)" />
    [SkipLocalsInit]
    public static string Format(ReadOnlySpan<char> format, Value arg1, Value arg2, Value arg3)
    {
        Span<char> buffer = stackalloc char[256];
        using ValueStringBuilder builder = new(buffer);
        builder.AppendFormat(format, arg1, arg2, arg3);
        return builder.ToString();
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value, Value)" />
    public static string Format(string format, Value arg1, Value arg2, Value arg3, Value arg4) =>
        Format(format.AsSpan(), arg1, arg2, arg3, arg4);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value, Value)" />
    [SkipLocalsInit]
    public static string Format(ReadOnlySpan<char> format, Value arg1, Value arg2, Value arg3, Value arg4)
    {
        Span<char> buffer = stackalloc char[256];
        using ValueStringBuilder builder = new(buffer);
        builder.AppendFormat(format, arg1, arg2, arg3, arg4);
        return builder.ToString();
    }

    /// <summary>
    ///  Allocates a string of the specified length filled with null characters.
    /// </summary>
    internal static string FastAllocateString(int length) =>
        // This calls FastAllocateString in the runtime, with extra checks.
        new string('\0', length);

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
#if NET
        return string.GetHashCode(value);
#else
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
#endif
    }

    /// <summary>
    ///  Returns the exact comparison of two spans as if they were strings, including embedded nulls.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Use this method when you want the exact same result you would get from ordinally comparing two strings
    ///   that have the same content.
    ///  </para>
    /// </remarks>
    public static int CompareOrdinalAsString(ReadOnlySpan<char> span1, ReadOnlySpan<char> span2)
    {
        int sharedLength = Math.Min(span1.Length, span2.Length);

        int result = span1[..sharedLength].SequenceCompareTo(span2[..sharedLength]);

        if (result != 0 || span1.Length == span2.Length)
        {
            // If the spans are equal and the same length, or we found a mismatch, return the result.
            return result;
        }

        // If we've fully matched the shared length, follow the logic string would do. If there is no shared length
        // or the shared length is odd, we return the next character in the longer span, inverted if it is from
        // the second span (effectively comparing to "null").
        return sharedLength != 0 && sharedLength % 2 == 0
            ? span1.Length - span2.Length
            : span1.Length > span2.Length ? span1[sharedLength] : -span2[sharedLength];
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
        ArgumentOutOfRange.ThrowIfLessThanOrEqual(count, 0, nameof(count));
        ArgumentOutOfRange.ThrowIfLessThanOrEqual(minLength, 0, nameof(minLength));
        ArgumentOutOfRange.ThrowIfLessThan(maxLength, minLength, nameof(maxLength));

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
                    buffer[position++] = Chars.GetRandomSimpleChar(random);
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
