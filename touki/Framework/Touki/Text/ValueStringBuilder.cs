// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;

namespace Touki.Text;

public ref partial struct ValueStringBuilder
{
    private delegate bool TryFormatDelegate<T>(
        in T value,
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider);

    private static class FormatterHelper<T>
    {
        private static TryFormatDelegate<T>? s_tryFormatWithoutBoxing;

        /// <summary>
        ///  Delegate that can be used to format a value of type <typeparamref name="T"/> without boxing.
        /// </summary>
        internal static TryFormatDelegate<T>? TryFormatWithoutBoxing => s_tryFormatWithoutBoxing ??= Init();

        private static TryFormatDelegate<T>? Init()
        {
            // Dynamically check if T implements ISpanFormattable (e.g., via reflection or a known flag).
            if (!typeof(ISpanFormattable).IsAssignableFrom(typeof(T)))
            {
                return null;
            }

            // Shouldn't be using this for reference types.
            Debug.Assert(typeof(T).IsValueType);

            MethodInfo method = typeof(FormatterHelper<T>).GetMethod(
                nameof(TryFormat),
                BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(typeof(T));

            return (TryFormatDelegate<T>)Delegate.CreateDelegate(typeof(TryFormatDelegate<T>), method);
        }

        private static bool TryFormat<TFormat>(
            in TFormat value,
            Span<char> destination,
            out int charsWritten,
            ReadOnlySpan<char> format,
            IFormatProvider? provider) where TFormat : struct, ISpanFormattable
        {
            return value.TryFormat(destination, out charsWritten, format, provider);
        }
    }

    private readonly Span<char> Remaining
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _chars[_length..];
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
                _length += charsWritten;
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
                _length += charsWritten;
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
                _length += charsWritten;
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
                _length += charsWritten;
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
                _length += charsWritten;
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
                _length += charsWritten;
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
                _length += charsWritten;
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
        else if (typeof(T) == typeof(DateTime))
        {
            DateTimeFormat.Format(Unsafe.As<T, DateTime>(ref value), format, _formatProvider, ref this);
            return true;
        }
        else if (typeof(T) == typeof(DateTimeOffset))
        {
            DateTimeOffset dateTimeOffset = Unsafe.As<T, DateTimeOffset>(ref value);
            DateTimeFormat.Format(dateTimeOffset.DateTime, format, _formatProvider, dateTimeOffset.Offset, ref this);
            return true;
        }
        else if (typeof(T) == typeof(StringSegment))
        {
            // StringSegment implements ISpanFormattable and will format without boxing or allocations if we let it
            // through, but the performance is significantly better if we explicitly handle it here.
            StringSegment segment = Unsafe.As<T, StringSegment>(ref value);
            if (!segment.IsEmpty)
            {
                Append(segment.AsSpan());
            }

            return true;
        }

        // We could handle very common enums so that they don't need to allocate like crazy.
        if (typeof(T).IsEnum
            && format.IsEmpty
            && EnumExtensions.GetEnumData(typeof(T)) is var enumData
            && enumData.UnderlyingType is Type underlyingType
            && underlyingType == typeof(int))
        {
            ulong ulongValue = (ulong)Unsafe.As<T, int>(ref value);

            if (enumData.IsFlags)
            {
                InternalFlagsFormat(ulongValue, signed: true, enumData);
                return true;
            }

            (ulong[] values, string[] names) = enumData.Data;
            int index = Array.BinarySearch(values, ulongValue);
            if (index >= 0)
            {
                Append(names[index]);
            }
            else
            {
                return TryAppendFormattedPrimitives((int)ulongValue, default, null);
            }

            return true;
        }

        return false;
    }

    internal void InternalFlagsFormat(ulong value, bool signed, EnumExtensions.EnumData enumData)
    {
        ulong result = value;

        ulong[] values = enumData.Data.Values;
        string[] names = enumData.Data.Names;

        int index = values.Length - 1;
        bool firstTime = true;
        ulong saveResult = result;

        int startPosition = _length;

        // We will not optimize this code further to keep it maintainable. There are some boundary checks that can be applied
        // to minimize the comparisons required. This code works the same for the best/worst case. In general the number of
        // items in an enum are sufficiently small and not worth the optimization.
        while (index >= 0)
        {
            if ((index == 0) && (values[index] == 0))
            {
                break;
            }

            if ((result & values[index]) == values[index])
            {
                result -= values[index];
                if (!firstTime)
                {
                    Insert(startPosition, ", ");
                }

                Insert(startPosition, names[index]);
                firstTime = false;
            }

            index--;
        }

        // We were unable to represent this number as a bitwise or of valid flags
        if (result != 0)
        {
            _length = startPosition;
            bool success = signed
                ? TryAppendFormattedPrimitives((long)value, default, default)
                : TryAppendFormattedPrimitives(value, default, default);

            Debug.Assert(success, "Failed to format value as a primitive type.");
            return;
        }

        // For the case when we have zero
        if (saveResult == 0)
        {
            if (values.Length > 0 && values[0] == 0)
            {
                Append(names[0]); // Zero was one of the enum values.
            }
            else
            {
                Append("0");
            }
        }
    }
}
