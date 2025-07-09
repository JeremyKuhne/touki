// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public ref partial struct ValueStringBuilder
{
    // Undocumented exclusive limits on the range for Argument Hole Index and Argument Hole Alignment.
    private const int WidthLimit = 1000000; // Note:  -WidthLimit <  ArgAlign < WidthLimit

    // This is a copy of the StringBuilder.AppendFormatHelper with minor functional tweaks:
    //
    //  1. Has a small stackalloc span for formatting Variant value types into.
    //  2. Doesn't work with ISpanFormattable (the interface is currently internal).
    //  3. Uses Variant to format with no allocations for value types.
    //  4. Takes FormatString instead of string for format.
    //  5. Takes ReadOnlySpan<Variant> instead of ParamsArray.
    //  6. Code formatting is scrubbed a bit for clarity.

    // Note that Argument Hole parsing can be factored into a helper, perhaps taking a callback
    // delegate with (int index, int width, ReadOnlySpan<char> itemFormat) or something along those
    // lines. This would, of course, make the code a little slower, but the advantage of having
    // shareable logic (between StringBuilder, ValueStringBuilder, etc.) may be worth it.

    [ThreadStatic]
    private static Value[]? t_values;

    private static Value[] Values => t_values ??= new Value[4];

    private static char[] s_brackets { get; } = ['{', '}'];

    // These overloads should be conditioned between .NET and .NET Framework to leverage the ability to create
    // spans from refs in .NET.

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(string format, ReadOnlySpan<Value> args)
        => AppendFormat<Value>(format.AsSpan(), args);

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(ReadOnlySpan<char> format, ReadOnlySpan<Value> args)
        => AppendFormat<Value>(format, args);

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat<TArgument>(string format, TArgument arg) where TArgument : unmanaged
        => AppendFormat(format.AsSpan(), arg);

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat<TArgument>(ReadOnlySpan<char> format, TArgument arg) where TArgument : unmanaged
    {
        AppendFormat(format, new ReadOnlySpan<TArgument>(&arg, 1));
    }

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(string format, Value arg)
        => AppendFormat(format.AsSpan(), arg);

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(ReadOnlySpan<char> format, Value arg)
    {
        Values[0] = arg;
        AppendFormat(format, new ReadOnlySpan<Value>(Values, 0, 1));
    }

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(string format, Value arg1, Value arg2) => AppendFormat(format.AsSpan(), arg1, arg2);

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(ReadOnlySpan<char> format, Value arg1, Value arg2)
    {
        Values[0] = arg1;
        Values[1] = arg2;
        AppendFormat(format, new ReadOnlySpan<Value>(Values, 0, 2));
    }

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(string format, Value arg1, Value arg2, Value arg3) =>
        AppendFormat(format.AsSpan(), arg1, arg2, arg3);

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(ReadOnlySpan<char> format, Value arg1, Value arg2, Value arg3)
    {
        Values[0] = arg1;
        Values[1] = arg2;
        Values[2] = arg3;
        AppendFormat(format, new ReadOnlySpan<Value>(Values, 0, 3));
    }

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(string format, Value arg1, Value arg2, Value arg3, Value arg4) =>
        AppendFormat(format.AsSpan(), arg1, arg2, arg3, arg4);

    /// <inheritdoc cref="AppendFormat{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>
    public unsafe void AppendFormat(ReadOnlySpan<char> format, Value arg1, Value arg2, Value arg3, Value arg4)
    {
        Values[0] = arg1;
        Values[1] = arg2;
        Values[2] = arg3;
        Values[3] = arg4;
        AppendFormat(format, new ReadOnlySpan<Value>(Values, 0, 4));
    }

#if false
    // This is much easier to read, but the performance isn't too great on .NET Framework as it isn't able to
    // optimize quite as well. Leaving the code for reference.

    /// <summary>
    ///  Appends a formatted string to the current instance using the specified format and arguments.
    /// </summary>
    public void AppendFormatReader<TArgument>(ReadOnlySpan<char> format, ReadOnlySpan<TArgument> args)
    {
        SpanReader<char> reader = new(format);
        while (reader.TrySplitAny(s_brackets, out var literal))
        {
            // We found a '{' or '}'.

            // Append any literal before the next hole or end of the string.
            if (literal.Length > 0)
            {
                Append(literal);
            }

            // If we hit a '}' in the split any, the only legitimate case at this point is that it is an escaped bracket.
            // Grab the next character and check if it is an '}'. If it is, we append it and continue to the next iteration.
            // It is possible that we hit a poorly formatted string, such as "{}}0:5", we'll let it fail out in further
            // processing, rather than trying to check for every possible malformed case here.

            if (reader.TryPeek(out char next) && next == '}')
            {
                // Escaped bracket, add and continue.
                Append(next);
                reader.Advance(1);
                continue;
            }

            if (reader.End)
            {
                break;
            }

            // We've hit a hole {}, look for the index.
            if (!reader.TryReadPositiveInteger(out uint index) || index >= args.Length)
            {
                if (reader.TryPeek(out next) && next == '{')
                {
                    // Escaped bracket, add and continue.
                    Append(next);
                    reader.Advance(1);
                    continue;
                }

                // Invalid index, throw an error.
                FormatError();
            }

            if (!reader.TryRead(out next))
            {
                // We're in a hole, but there is nothing left to read.
                FormatError();
            }

            int alignment = 0;
            if (next == ',')
            {
                // Optional alignment, read it.
                bool negative = false;

                if (reader.TryPeek(out next) && next == '-')
                {
                    negative = true;
                    reader.Advance(1);
                }

                if (!reader.TryReadPositiveInteger(out uint alignmentValue)
                    || alignmentValue >= WidthLimit
                    || !reader.TryRead(out next))
                {
                    // Invalid alignment, throw an error.
                    FormatError();
                }

                alignment = negative ? (int)alignmentValue * -1 : (int)alignmentValue;
            }

            ReadOnlySpan<char> formatSpan = default;
            if (next == ':')
            {
                // Optional formatting, read it.
                if (!reader.TryReadTo('}', advancePastDelimiter: true, out formatSpan))
                {
                    // Invalid format, throw an error.
                    FormatError();
                }
            }
            else if (next != '}')
            {
                // Invalid character after index, throw an error.
                FormatError();
            }

            // Now add the formatted value.
            AppendFormatted(args[(int)index], alignment, formatSpan);
        }
    }
#endif

    /// <summary>
    ///  Appends a formatted string to the current instance using the specified format and arguments.
    /// </summary>
    public void AppendFormat<TArgument>(ReadOnlySpan<char> format, ReadOnlySpan<TArgument> args)
    {
        ReadOnlySpan<char> remaining = format;
        while (true)
        {
            int braceIndex = remaining.IndexOfAny('{', '}');
            if (braceIndex < 0)
            {
                // No more braces, just append the rest of the string.
                Append(remaining);
                break;
            }

            // We found a '{' or '}'.

            if (braceIndex > 0)
            {
                // Append any literal before the next hole.
                Append(remaining[..braceIndex]);
            }

            char brace = remaining[braceIndex];
            remaining = remaining[(braceIndex + 1)..];
            if (remaining.IsEmpty)
            {
                // We're in a hole or an escaped character, but there is nothing left to read.
                FormatError();
            }

            // If the next character is a bracket, we need to consider it escaped.

            // If we found a '}' in the find any, the only legitimate case at this point is that it is an escaped bracket.
            // Grab the next character and check if it is an '}'. If it is, we append it and continue to the next iteration.
            // It is possible that we hit a poorly formatted string, such as "{}}0:5", we'll let it fail out in further
            // processing, rather than trying to check for every possible malformed case here.

            char next = remaining[0];
            if (next is '}' or '{')
            {
                if (brace != next)
                {
                    FormatError();
                }

                Append(brace);
                remaining = remaining[1..];
                continue;
            }

            // Get our index for the hole argument.

            uint index = (uint)(next - '0');
            if (index > 9)
            {
                // Invalid index digit, throw an error.
                FormatError();
            }

            uint digit;

            remaining = remaining[1..];
            while (!remaining.IsEmpty && (digit = (uint)(remaining[0] - '0')) <= 9)
            {
                index = index * 10 + digit;
                remaining = remaining[1..];
            }

            if (index >= args.Length || remaining.IsEmpty)
            {
                FormatError();
            }

            int alignment = 0;
            if (remaining[0] == ',')
            {
                // Optional alignment, read it.

                remaining = remaining[1..];
                bool negative = false;
                if (!remaining.IsEmpty && remaining[0] == '-')
                {
                    negative = true;
                    remaining = remaining[1..];
                }

                uint width = (uint)(remaining[0] - '0');
                if (width > 9)
                {
                    // Invalid index digit, throw an error.
                    FormatError();
                }

                remaining = remaining[1..];

                while (!remaining.IsEmpty && (digit = (uint)(remaining[0] - '0')) <= 9)
                {
                    width = width * 10 + digit;
                    remaining = remaining[1..];
                }

                if (remaining.IsEmpty || width >= WidthLimit)
                {
                    FormatError();
                }

                alignment = negative ? -(int)width : (int)width;
            }

            ReadOnlySpan<char> itemFormat = default;
            if (remaining[0] == ':')
            {
                // Optional formatting, read it.

                remaining = remaining[1..];
                int end = remaining.IndexOf('}');
                if (end < 0)
                {
                    FormatError();
                }

                itemFormat = remaining[..end];
                remaining = remaining[end..];
            }

            if (remaining.IsEmpty || remaining[0] != '}')
            {
                FormatError();
            }

            remaining = remaining[1..];
            AppendFormatted(args[(int)index], alignment, itemFormat);
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FormatError() => throw new FormatException("Input string was not in a correct format.");

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value) => Append(value);

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted(string? value)
    {
        if (_hasCustomFormatter)
        {
            // If there's a custom formatter, always use it.
            AppendCustomFormatter(value, format: null);
            return;
        }

        if (value is null)
        {
            // If the value is null, just leave it blank.
            return;
        }

        Append(value.AsSpan());
    }

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted(string? value, int alignment = 0, string? format = null) =>
        // Format is meaningless for strings and doesn't make sense for someone to specify.  We have the overload
        // simply to disambiguate between ROS<char> and object, just in case someone does specify a format, as
        // string is implicitly convertible to both. Just delegate to the T-based implementation.
        AppendFormatted<object?>(value, alignment, format);

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted(object? value, int alignment = 0, string? format = null) =>
        // This overload is expected to be used rarely, only if either a) something strongly typed as object is
        // formatted with both an alignment and a format, or b) the compiler is unable to target type to T. It
        // exists purely to help make cases from (b) compile. Just delegate to the T-based implementation.
        AppendFormatted<object?>(value, alignment, format);

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted<T>(T value)
    {
        // This method could delegate to AppendFormatted with a null format, but explicitly passing
        // default as the format to TryFormat helps to improve code quality in some cases when TryFormat is inlined,
        // e.g. for Int32 it enables the JIT to eliminate code in the inlined method based on a length check on the format.

        // If there's a custom formatter, always use it.
        if (_hasCustomFormatter)
        {
            AppendCustomFormatter(value, format: null);
            return;
        }

        if (value is null)
        {
            // If the value is null, just leave it blank.
            return;
        }

        // Check first for IFormattable, even though we'll prefer to use ISpanFormattable, as the latter
        // requires the former.  For value types, it won't matter as the type checks devolve into
        // JIT-time constants.  For reference types, they're more likely to implement IFormattable
        // than they are to implement ISpanFormattable: if they don't implement either, we save an
        // interface check over first checking for ISpanFormattable and then for IFormattable, and
        // if it only implements IFormattable, we come out even: only if it implements both do we
        // end up paying for an extra interface check.

#if NETFRAMEWORK
        // On .NET Framework, directly format with the copy of the .NET 6 formatting code.
        if (TryAppendFormattedPrimitives(value, [], _formatProvider))
        {
            return;
        }
#endif

        // Attempting to avoid boxing by casting inline to allow constrained calls.
#pragma warning disable IDE0038 // Use pattern matching
        if (value is IFormattable)
        {
            if (value is ISpanFormattable)
            {
                int charsWritten;

                // The intent here is to avoid boxing for value types that implement ISpanFormattable by enabling a constrained call.
                //
                // Having difficulty validating this in unit tests. The .NET libraries code does the exact same thing, and I can
                // validate that it doesn't box for value types in my unit tests. Have tried every combination of code I can think
                // of without success. If the boxing is unavoidable, then other options can be considered, such as type checking for
                // frequently used runtime types and calling a constrained method directly.
                while (!((ISpanFormattable)value!).TryFormat(_chars[_length..], out charsWritten, default, _formatProvider))
                {
                    DoubleRemaining();
                }

                _length += charsWritten;
                return;
            }

            Append(((IFormattable)value).ToString(format: null, _formatProvider));
            return;
        }
#pragma warning restore IDE0038

        if (value.ToString() is string asString)
        {
            Append(asString);
            return;
        }
    }

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted<T>(T value, string? format) => AppendFormatted(value, (StringSpan)format);

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted<T>(T value, StringSpan format)
    {
        if (value is null)
        {
            // If the value is null, just leave it blank.
            return;
        }

        // If there's a custom formatter, always use it.
        if (_hasCustomFormatter)
        {
            AppendCustomFormatter(value, format);
            return;
        }

        if (typeof(T) == typeof(Value))
        {
            Unsafe.As<T, Value>(ref value).Format(ref this, format);
            return;
        }

#if NETFRAMEWORK
        // On .NET Framework, directly format with the copy of the .NET 6 formatting code.
        if (TryAppendFormattedPrimitives(value, format, _formatProvider))
        {
            return;
        }
#endif

        // Attempting to avoid boxing by casting inline to allow constrained calls.
#pragma warning disable IDE0038 // Use pattern matching
        if (value is IFormattable)
        {
            if (value is ISpanFormattable)
            {
                int charsWritten;

                // The intent here is to avoid boxing for value types that implement ISpanFormattable by enabling a constrained call.
                //
                // Having difficulty validating this in unit tests. The .NET libraries code does the exact same thing, and I can
                // validate that it doesn't box for value types in my unit tests. Have tried every combination of code I can think
                // of without success. If the boxing is unavoidable, then other options can be considered, such as type checking for
                // frequently used runtime types and calling a constrained method directly.
                while (!((ISpanFormattable)value!).TryFormat(_chars[_length..], out charsWritten, format, _formatProvider))
                {
                    DoubleRemaining();
                }

                _length += charsWritten;
                return;
            }

            Append(((IFormattable)value).ToString(format.ToStringOrNull(), _formatProvider));
            return;
        }
#pragma warning restore IDE0038

        if (value?.ToString() is string asString)
        {
            Append(asString);
            return;
        }
    }

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted<T>(T value, int alignment)
    {
        int startingPos = _length;
        AppendFormatted(value);
        if (alignment != 0)
        {
            AppendOrInsertAlignmentIfNeeded(startingPos, alignment);
        }
    }

    /// <inheritdoc cref="AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted<T>(T value, int alignment, StringSpan format)
    {
        int startingPos = _length;
        AppendFormatted(value, format);
        if (alignment != 0)
        {
            AppendOrInsertAlignmentIfNeeded(startingPos, alignment);
        }
    }

    /// <summary>Writes the specified value to the handler.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    /// <param name="alignment">
    ///  Minimum number of characters that should be written for this value. If the value is negative,
    ///  it indicates left-aligned and the required minimum is the absolute value.</param>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
    {
        bool leftAlign = false;
        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        int paddingRequired = alignment - value.Length;
        if (paddingRequired <= 0)
        {
            // The value is as large or larger than the required amount of padding,
            // so just write the value.
            AppendFormatted(value);
            return;
        }

        // Write the value along with the appropriate padding.
        EnsureRemaining(value.Length + paddingRequired);

        if (leftAlign)
        {
            value.CopyTo(_chars[_length..]);
            _length += value.Length;
            _chars.Slice(_length, paddingRequired).Fill(' ');
            _length += paddingRequired;
        }
        else
        {
            _chars.Slice(_length, paddingRequired).Fill(' ');
            _length += paddingRequired;
            value.CopyTo(_chars[_length..]);
            _length += value.Length;
        }
    }

    /// <summary>Formats the value using the custom formatter from the provider.</summary>
    /// <param name="value">The value to write.</param>
    /// <param name="format">The format string.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void AppendCustomFormatter<T>(T value, StringSpan format)
    {
        // This case is very rare, but we need to handle it prior to the other checks in case
        // a provider was used that supplied an ICustomFormatter which wanted to intercept the particular value.
        // We do the cast here rather than in the ctor, even though this could be executed multiple times per
        // formatting, to make the cast pay for play.
        Debug.Assert(_hasCustomFormatter);
        Debug.Assert(_formatProvider is not null);

        ICustomFormatter? formatter = (ICustomFormatter?)_formatProvider!.GetFormat(typeof(ICustomFormatter));
        Debug.Assert(
            formatter is not null,
            "An incorrectly written provider said it implemented ICustomFormatter, and then didn't");

        if (formatter is not null && formatter.Format(format.ToStringOrNull(), value, _formatProvider) is string customFormatted)
        {
            AppendLiteral(customFormatted);
        }
    }

    /// <summary>
    ///  Handles adding any padding required for aligning a formatted value in an interpolation expression.
    /// </summary>
    /// <param name="startingPosition">The position at which the written value started.</param>
    /// <param name="alignment">
    ///  Non-zero minimum number of characters that should be written for this value. If the value is
    ///  negative, it indicates left-aligned and the required minimum is the absolute value.
    /// </param>
    private void AppendOrInsertAlignmentIfNeeded(int startingPosition, int alignment)
    {
        Debug.Assert(startingPosition >= 0 && startingPosition <= _length);
        Debug.Assert(alignment != 0);

        int charsWritten = _length - startingPosition;

        bool leftAlign = false;
        if (alignment < 0)
        {
            leftAlign = true;
            alignment = -alignment;
        }

        int paddingNeeded = alignment - charsWritten;
        if (paddingNeeded > 0)
        {
            EnsureRemaining(paddingNeeded);

            if (leftAlign)
            {
                _chars.Slice(_length, paddingNeeded).Fill(' ');
            }
            else
            {
                _chars.Slice(startingPosition, charsWritten).CopyTo(_chars[(startingPosition + paddingNeeded)..]);
                _chars.Slice(startingPosition, paddingNeeded).Fill(' ');
            }

            _length += paddingNeeded;
        }
    }
}
