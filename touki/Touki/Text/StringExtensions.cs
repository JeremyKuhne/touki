// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

/// <summary>
///  Helpers for working with <see cref="string"/> and <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
/// </summary>
public static partial class StringExtensions
{
    extension(string)
    {
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
    }
}
