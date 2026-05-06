// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace System;

/// <summary>
///  Polyfill for <see langword="string"/> methods that take spans.
/// </summary>
public static class StringExtensions
{
    extension(string)
    {
        /// <summary>
        ///  Concatenates the string representations of two specified read-only character spans.
        /// </summary>
        public static unsafe string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1)
        {
            int length = checked(str0.Length + str1.Length);
            if (length == 0)
            {
                return string.Empty;
            }

            string result = new('\0', length);
            fixed (char* p = result)
            {
                Span<char> dst = new(p, length);
                str0.CopyTo(dst);
                str1.CopyTo(dst[str0.Length..]);
            }

            return result;
        }

        /// <summary>
        ///  Concatenates the string representations of three specified read-only character spans.
        /// </summary>
        public static unsafe string Concat(ReadOnlySpan<char> str0, ReadOnlySpan<char> str1, ReadOnlySpan<char> str2)
        {
            int length = checked(str0.Length + str1.Length + str2.Length);
            if (length == 0)
            {
                return string.Empty;
            }

            string result = new('\0', length);
            fixed (char* p = result)
            {
                Span<char> dst = new(p, length);
                str0.CopyTo(dst);
                dst = dst[str0.Length..];
                str1.CopyTo(dst);
                dst = dst[str1.Length..];
                str2.CopyTo(dst);
            }

            return result;
        }

        /// <summary>
        ///  Concatenates the string representations of four specified read-only character spans.
        /// </summary>
        public static unsafe string Concat(
            ReadOnlySpan<char> str0,
            ReadOnlySpan<char> str1,
            ReadOnlySpan<char> str2,
            ReadOnlySpan<char> str3)
        {
            int length = checked(str0.Length + str1.Length + str2.Length + str3.Length);
            if (length == 0)
            {
                return string.Empty;
            }

            string result = new('\0', length);
            fixed (char* p = result)
            {
                Span<char> dst = new(p, length);
                str0.CopyTo(dst);
                dst = dst[str0.Length..];
                str1.CopyTo(dst);
                dst = dst[str1.Length..];
                str2.CopyTo(dst);
                dst = dst[str2.Length..];
                str3.CopyTo(dst);
            }

            return result;
        }
    }

    extension(string source)
    {
        /// <summary>
        ///  Copies the contents of this string into the destination span.
        /// </summary>
        /// <param name="destination">The span into which to copy this string's contents.</param>
        /// <exception cref="ArgumentException">The destination span is shorter than the source string.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void CopyTo(Span<char> destination)
        {
            if ((uint)source.Length <= (uint)destination.Length)
            {
                fixed (char* pSource = source)
                fixed (char* pDestination = destination)
                {
                    Buffer.MemoryCopy(
                        pSource,
                        pDestination,
                        destination.Length * sizeof(char),
                        source.Length * sizeof(char));
                }
            }
            else
            {
                ArgumentException.Throw(null, nameof(destination));
            }
        }

        /// <summary>
        ///  Copies the contents of this string into the destination span.
        /// </summary>
        /// <returns><see langword="true"/> if the data was copied; <see langword="false"/> if the destination is too short.</returns>
        public bool TryCopyTo(Span<char> destination)
        {
            if ((uint)source.Length > (uint)destination.Length)
            {
                return false;
            }

            source.AsSpan().CopyTo(destination);
            return true;
        }

        /// <summary>
        ///  Returns a value indicating whether a specified character occurs within this string.
        /// </summary>
        public bool Contains(char value) => source.IndexOf(value) >= 0;

        /// <summary>
        ///  Returns a value indicating whether a specified character occurs within this string,
        ///  using the specified comparison rules.
        /// </summary>
        public bool Contains(char value, StringComparison comparisonType) =>
            source.IndexOf(value.ToString(), comparisonType) >= 0;

        /// <summary>
        ///  Returns a value indicating whether a specified substring occurs within this string,
        ///  using the specified comparison rules.
        /// </summary>
        public bool Contains(string value, StringComparison comparisonType)
        {
            ArgumentNullException.ThrowIfNull(value);
            return source.IndexOf(value, comparisonType) >= 0;
        }

        /// <summary>
        ///  Determines whether this string begins with the specified character.
        /// </summary>
        public bool StartsWith(char value) => source.Length > 0 && source[0] == value;

        /// <summary>
        ///  Determines whether the end of this string matches the specified character.
        /// </summary>
        public bool EndsWith(char value) => source.Length > 0 && source[^1] == value;

        /// <summary>
        ///  Returns the hash code for this string using the specified rules.
        /// </summary>
        public int GetHashCode(StringComparison comparisonType) =>
            ComparerForComparison(comparisonType).GetHashCode(source);

        /// <summary>
        ///  Returns a new string in which all occurrences of <paramref name="oldValue"/> in this instance are
        ///  replaced with <paramref name="newValue"/>, using the provided <paramref name="comparisonType"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   For non-ordinal comparisons, this polyfill advances by <paramref name="oldValue"/>'s
        ///   character length per match. Locales whose collation maps a single search character
        ///   to a multi-character match (rare) may differ slightly from the modern BCL.
        ///  </para>
        /// </remarks>
        public string Replace(string oldValue, string? newValue, StringComparison comparisonType)
        {
            ArgumentNullException.ThrowIfNull(oldValue);
            if (oldValue.Length == 0)
            {
                throw new ArgumentException("String cannot be of zero length.", nameof(oldValue));
            }

            // Fast path for ordinal: defer to BCL.
            if (comparisonType == StringComparison.Ordinal && newValue is not null)
            {
                return source.Replace(oldValue, newValue);
            }

            newValue ??= string.Empty;
            CompareInfo compareInfo;
            CompareOptions options;
            switch (comparisonType)
            {
                case StringComparison.CurrentCulture:
                    compareInfo = CultureInfo.CurrentCulture.CompareInfo;
                    options = CompareOptions.None;
                    break;
                case StringComparison.CurrentCultureIgnoreCase:
                    compareInfo = CultureInfo.CurrentCulture.CompareInfo;
                    options = CompareOptions.IgnoreCase;
                    break;
                case StringComparison.InvariantCulture:
                    compareInfo = CultureInfo.InvariantCulture.CompareInfo;
                    options = CompareOptions.None;
                    break;
                case StringComparison.InvariantCultureIgnoreCase:
                    compareInfo = CultureInfo.InvariantCulture.CompareInfo;
                    options = CompareOptions.IgnoreCase;
                    break;
                case StringComparison.Ordinal:
                    compareInfo = CultureInfo.InvariantCulture.CompareInfo;
                    options = CompareOptions.Ordinal;
                    break;
                case StringComparison.OrdinalIgnoreCase:
                    compareInfo = CultureInfo.InvariantCulture.CompareInfo;
                    options = CompareOptions.OrdinalIgnoreCase;
                    break;
                default:
                    throw new ArgumentException("Unsupported comparison.", nameof(comparisonType));
            }

            ValueStringBuilder builder = new(stackalloc char[256]);
            builder.EnsureCapacity(source.Length);
            int start = 0;
            while (start <= source.Length)
            {
                int matchStart = compareInfo.IndexOf(source, oldValue, start, source.Length - start, options);
                if (matchStart < 0)
                {
                    builder.Append(source.AsSpan(start, source.Length - start));
                    break;
                }

                builder.Append(source.AsSpan(start, matchStart - start));
                builder.Append(newValue);
                start = matchStart + oldValue.Length;
            }

            return builder.ToStringAndDispose();
        }

        /// <summary>
        ///  Replaces all newline sequences in this string with <see cref="Environment.NewLine"/>.
        /// </summary>
        public string ReplaceLineEndings() => source.ReplaceLineEndings(Environment.NewLine);

        /// <summary>
        ///  Replaces all newline sequences in this string with <paramref name="replacementText"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   The recognized line endings are LF (<c>\n</c>), CR (<c>\r</c>), CRLF (<c>\r\n</c>),
        ///   FF (<c>\u000C</c>), NEL (<c>\u0085</c>), LS (<c>\u2028</c>), and PS (<c>\u2029</c>).
        ///  </para>
        /// </remarks>
        public string ReplaceLineEndings(string replacementText)
        {
            ArgumentNullException.ThrowIfNull(replacementText);

            // Quick scan to see if any line ending is present.
            int firstIndex = IndexOfLineEnding(source.AsSpan());
            if (firstIndex < 0)
            {
                return source;
            }

            ValueStringBuilder builder = new(stackalloc char[256]);
            builder.EnsureCapacity(source.Length);
            builder.Append(source.AsSpan(0, firstIndex));
            int i = firstIndex;
            while (i < source.Length)
            {
                char c = source[i];
                if (IsLineEnding(c))
                {
                    builder.Append(replacementText);
                    // Treat CRLF as a single line ending.
                    if (c == '\r' && i + 1 < source.Length && source[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
                else
                {
                    builder.Append(c);
                    i++;
                }
            }

            return builder.ToStringAndDispose();
        }

        /// <summary>
        ///  Splits this string into substrings based on a single character separator.
        /// </summary>
        public string[] Split(char separator, StringSplitOptions options = StringSplitOptions.None) =>
            source.Split([separator], options);

        /// <summary>
        ///  Splits this string into a maximum of <paramref name="count"/> substrings using a single character separator.
        /// </summary>
        public string[] Split(char separator, int count, StringSplitOptions options = StringSplitOptions.None) =>
            source.Split([separator], count, options);

        /// <summary>
        ///  Splits this string into substrings based on a string separator.
        /// </summary>
        public string[] Split(string? separator, StringSplitOptions options = StringSplitOptions.None)
        {
            // Match modern BCL: a null separator yields the source unchanged (does NOT fall back to whitespace).
            if (separator is null)
            {
                return [source];
            }

            return source.Split([separator], options);
        }

        /// <summary>
        ///  Splits this string into a maximum of <paramref name="count"/> substrings based on a string separator.
        /// </summary>
        public string[] Split(string? separator, int count, StringSplitOptions options = StringSplitOptions.None)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            if (count == 0 || source.Length == 0)
            {
                return count == 0 ? [] : [source];
            }

            if (separator is null)
            {
                return [source];
            }

            return source.Split([separator], count, options);
        }
    }

    private static bool IsLineEnding(char c) =>
        c is '\n' or '\r' or '\f' or '\u0085' or '\u2028' or '\u2029';

    private static int IndexOfLineEnding(ReadOnlySpan<char> span)
    {
        for (int i = 0; i < span.Length; i++)
        {
            if (IsLineEnding(span[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static StringComparer ComparerForComparison(StringComparison comparisonType) => comparisonType switch
    {
        StringComparison.CurrentCulture => StringComparer.CurrentCulture,
        StringComparison.CurrentCultureIgnoreCase => StringComparer.CurrentCultureIgnoreCase,
        StringComparison.InvariantCulture => StringComparer.InvariantCulture,
        StringComparison.InvariantCultureIgnoreCase => StringComparer.InvariantCultureIgnoreCase,
        StringComparison.Ordinal => StringComparer.Ordinal,
        StringComparison.OrdinalIgnoreCase => StringComparer.OrdinalIgnoreCase,
        _ => throw new ArgumentException("Unsupported comparison.", nameof(comparisonType))
    };
}
