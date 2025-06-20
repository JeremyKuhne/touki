// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki;

public ref partial struct ValueStringBuilder
{
    private readonly Span<char> Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chars[_position..];
    }

    private bool TryAppendFormattedPrimitives<T>(T value, ReadOnlySpan<char> format, IFormatProvider? formatProvider)
    {
        // Try to leave space for the largest values, including when formatting with separators or as hexadecimal.
        const int IntegerReserveSpace = 32;

        Debug.Assert(value is not null);
        if (value is null)
        {
            // If the value is null, just leave it blank.
            return true;
        }

        if (typeof(T) == typeof(bool))
        {
            AppendLiteral(Unsafe.As<T, bool>(ref value).ToString());
            return true;
        }
        else if (typeof(T) == typeof(sbyte))
        {
            EnsureRemaining(IntegerReserveSpace);

            if (Number.TryFormatInt32(Unsafe.As<T, int>(ref value), 0xFF, format, formatProvider, Remaining, out int charsWritten))
            {
                _position += charsWritten;
                return true;
            }

            Debug.Fail("Not enough space to format int?");
            return false;
        }
        else if (typeof(T) == typeof(short))
        {
            EnsureRemaining(IntegerReserveSpace);

            if (Number.TryFormatInt32(Unsafe.As<T, int>(ref value), 0xFFFF, format, formatProvider, Remaining, out int charsWritten))
            {
                _position += charsWritten;
                return true;
            }

            Debug.Fail("Not enough space to format int?");
            return false;
        }
        else if (typeof(T) == typeof(int))
        {
            EnsureRemaining(IntegerReserveSpace);

            if (Number.TryFormatInt32(Unsafe.As<T, int>(ref value), ~0, format, formatProvider, Remaining, out int charsWritten))
            {
                _position += charsWritten;
                return true;
            }

            Debug.Fail("Not enough space to format int?");
            return false;
        }
        else if (typeof(T) == typeof(byte))
        {
            return TryAppendFormattedPrimitives((uint)Unsafe.As<T, byte>(ref value), format, formatProvider);
        }
        else if (typeof(T) == typeof(ushort))
        {
            return TryAppendFormattedPrimitives((uint)Unsafe.As<T, ushort>(ref value), format, formatProvider);
        }
        else if (typeof(T) == typeof(uint))
        {
            EnsureRemaining(IntegerReserveSpace);

            if (Number.TryFormatUInt32(Unsafe.As<T, uint>(ref value), format, formatProvider, Remaining, out int charsWritten))
            {
                _position += charsWritten;
                return true;
            }

            Debug.Fail("Not enough space to format uint?");
            return false;
        }
        else if (typeof(T) == typeof(long))
        {
            EnsureRemaining(IntegerReserveSpace);

            if (Number.TryFormatInt64(Unsafe.As<T, long>(ref value), format, formatProvider, Remaining, out int charsWritten))
            {
                _position += charsWritten;
                return true;
            }

            Debug.Fail("Not enough space to format long?");
            return false;
        }
        else if (typeof(T) == typeof(ulong))
        {
            EnsureRemaining(IntegerReserveSpace);

            if (Number.TryFormatUInt64(Unsafe.As<T, ulong>(ref value), format, formatProvider, Remaining, out int charsWritten))
            {
                _position += charsWritten;
                return true;
            }

            Debug.Fail("Not enough space to format ulong?");
            return false;
        }
        else if (typeof(T) == typeof(decimal))
        {
            EnsureRemaining(IntegerReserveSpace);

            if (Number.TryFormatDecimal(
                Unsafe.As<T, decimal>(ref value),
                format,
                NumberFormatInfo.GetInstance(formatProvider),
                Remaining,
                out int charsWritten))
            {
                _position += charsWritten;
                return true;
            }

            Debug.Fail("Not enough space to format decimal?");
            return false;
        }
        else if (typeof(T) == typeof(float))
        {
            // Behavior of floating point formatting changed in .NET Core 3.0. To maintain compatibility, always use
            // G15 format for floats and doubles to avoid compatibility issues.

            // https://devblogs.microsoft.com/dotnet/floating-point-parsing-and-formatting-improvements-in-net-core-3-0/

            if (format.IsEmpty || (format.Length == 1 && format[0] == 'G'))
            {
                format = "G7".AsSpan();
            }

            Number.FormatSingle(
                Unsafe.As<T, float>(ref value),
                ref this,
                format,
                NumberFormatInfo.GetInstance(_formatProvider));

            return true;
        }
        else if (typeof(T) == typeof(double))
        {
            // Behavior of floating point formatting changed in .NET Core 3.0. To maintain compatibility, always use
            // G15 format for floats and doubles to avoid compatibility issues.

            // https://devblogs.microsoft.com/dotnet/floating-point-parsing-and-formatting-improvements-in-net-core-3-0/

            if (format.IsEmpty || (format.Length == 1 && format[0] == 'G'))
            {
                format = "G15".AsSpan();
            }

            Number.FormatDouble(
                Unsafe.As<T, double>(ref value),
                ref this,
                format,
                NumberFormatInfo.GetInstance(_formatProvider));

            return true;
        }

        // We could handle very common enums so that they don't need to allocate like crazy.
        if (typeof(T).IsEnum
            && format.IsEmpty
            && EnumExtensions.GetEnumData(typeof(T)) is var enumData
            && !enumData.IsFlags
            && enumData.UnderlyingType == typeof(int))
        {
            int intValue = Unsafe.As<T, int>(ref value);

            (ulong[] values, string[] names) = enumData.Data;
            int index = Array.BinarySearch(values, (ulong)intValue);
            if (index >= 0)
            {
                Append(names[index]);
            }
            else
            {
                return TryAppendFormattedPrimitives(intValue, default, null);
            }

            return true;
        }

        return false;
    }
}
