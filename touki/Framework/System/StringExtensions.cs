// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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
}
