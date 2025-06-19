// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Supports string operations and utilities.
/// </summary>
public static partial class Strings
{
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
}
