// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki.Text;

/// <summary>
///  Extensions for <see cref="StringBuilder"/> to provide additional functionality.
/// </summary>
public static unsafe partial class StringBuilderExtensions
{
    /// <summary>
    ///  Appends a <see cref="ReadOnlySpan{T}"/> of characters to the end of the <see cref="StringBuilder"/>.
    /// </summary>
    public static StringBuilder AppendSpan(this StringBuilder builder, ReadOnlySpan<char> value)
    {
        if (!value.IsEmpty)
        {
            fixed (char* pValue = value)
            {
                // Use the StringBuilder's Append method that takes a char pointer and length for better performance.
                builder.Append(pValue, value.Length);
            }
        }

        return builder;
    }

    /// <summary>
    ///  Appends a <see cref="Memory{T}"/> of characters to the end of the <see cref="StringBuilder"/>.
    /// </summary>
    public static StringBuilder AppendSpan(this StringBuilder builder, Memory<char> value) => builder.AppendSpan(value.Span);

    /// <summary>
    ///  Cross Framework interpolated string extension for <see cref="StringBuilder"/>.
    /// </summary>
    public static StringBuilder AppendFormatted(this StringBuilder builder, ref ValueStringBuilder valueBuilder)
    {
        if (valueBuilder.Length > 0)
        {
            builder.AppendSpan(valueBuilder.AsSpan());

            // Clear the ValueStringBuilder after appending to ensure buffer release.
            valueBuilder.Clear();
        }

        return builder;
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    public static StringBuilder AppendFormatted<TArgument>(this StringBuilder builder, string format, TArgument arg)
        where TArgument : unmanaged => AppendFormatted(builder, format.AsSpan(), arg);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    [SkipLocalsInit]
    public static StringBuilder AppendFormatted<TArgument>(this StringBuilder builder, ReadOnlySpan<char> format, TArgument arg) where TArgument : unmanaged
    {
        Span<char> buffer = stackalloc char[256];
        ValueStringBuilder valueBuilder = new(buffer);
        valueBuilder.AppendFormat(format, arg);
        return builder.AppendFormatted(ref valueBuilder);
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    public static StringBuilder AppendFormatted(this StringBuilder builder, string format, Value arg)
        => AppendFormatted(builder, format.AsSpan(), arg);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    [SkipLocalsInit]
    public static StringBuilder AppendFormatted(this StringBuilder builder, ReadOnlySpan<char> format, Value arg)
    {
        Span<char> buffer = stackalloc char[256];
        ValueStringBuilder valueBuilder = new(buffer);
        valueBuilder.AppendFormat(format, arg);
        return builder.AppendFormatted(ref valueBuilder);
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    public static StringBuilder AppendFormatted(this StringBuilder builder, string format, ReadOnlySpan<Value> args) =>
        AppendFormatted(builder, format.AsSpan(), args);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value)" />
    [SkipLocalsInit]
    public static StringBuilder AppendFormatted(this StringBuilder builder, ReadOnlySpan<char> format, ReadOnlySpan<Value> args)
    {
        Span<char> buffer = stackalloc char[256];
        ValueStringBuilder valueBuilder = new(buffer);
        valueBuilder.AppendFormat(format, args);
        return builder.AppendFormatted(ref valueBuilder);
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value)" />
    public static StringBuilder AppendFormatted(this StringBuilder builder, string format, Value arg1, Value arg2) =>
        AppendFormatted(builder, format.AsSpan(), arg1, arg2);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value)" />
    [SkipLocalsInit]
    public static StringBuilder AppendFormatted(this StringBuilder builder, ReadOnlySpan<char> format, Value arg1, Value arg2)
    {
        Span<char> buffer = stackalloc char[256];
        ValueStringBuilder valueBuilder = new(buffer);
        valueBuilder.AppendFormat(format, arg1, arg2);
        return builder.AppendFormatted(ref valueBuilder);
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value)" />
    public static StringBuilder AppendFormatted(
        this StringBuilder builder,
        string format,
        Value arg1,
        Value arg2,
        Value arg3) =>
        AppendFormatted(builder, format.AsSpan(), arg1, arg2, arg3);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value)" />
    [SkipLocalsInit]
    public static StringBuilder AppendFormatted(
        this StringBuilder builder,
        ReadOnlySpan<char> format,
        Value arg1,
        Value arg2,
        Value arg3)
    {
        Span<char> buffer = stackalloc char[256];
        ValueStringBuilder valueBuilder = new(buffer);
        valueBuilder.AppendFormat(format, arg1, arg2, arg3);
        return builder.AppendFormatted(ref valueBuilder);
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value, Value)" />
    public static StringBuilder AppendFormatted(
        this StringBuilder builder,
        string format,
        Value arg1,
        Value arg2,
        Value arg3,
        Value arg4) =>
        AppendFormatted(builder, format.AsSpan(), arg1, arg2, arg3, arg4);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormat(ReadOnlySpan{char}, Value, Value, Value, Value)" />
    [SkipLocalsInit]
    public static StringBuilder AppendFormatted(
        this StringBuilder builder,
        ReadOnlySpan<char> format,
        Value arg1,
        Value arg2,
        Value arg3,
        Value arg4)
    {
        Span<char> buffer = stackalloc char[256];
        ValueStringBuilder valueBuilder = new(buffer);
        valueBuilder.AppendFormat(format, arg1, arg2, arg3, arg4);
        return builder.AppendFormatted(ref valueBuilder);
    }
}
