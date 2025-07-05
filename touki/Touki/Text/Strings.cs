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
    public static int GetHashCode(ReadOnlySpan<char> value)
    {
#if NET
        return string.GetHashCode(value);
#else
        if (value.IsEmpty)
        {
            // "".GetHashCode();
            return 371857150;
        }

        // This is the 64bit .NET Framework implementation
        // adapted to work with spans instead of pointers
        int hash1 = 5381;
        int hash2 = hash1;

        int i = 0;
        int length = value.Length;

        while (i < length)
        {
            int c = value[i];
            hash1 = ((hash1 << 5) + hash1) ^ c;
            i++;

            if (i < length)
            {
                c = value[i];
                hash2 = ((hash2 << 5) + hash2) ^ c;
                i++;
            }
        }

        return hash1 + (hash2 * 1566083941);
#endif
    }
}
