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

    /// <summary>
    ///  Optimized version of <see cref="AppendFormatReader{TArgument}(ReadOnlySpan{char}, ReadOnlySpan{TArgument})"/>.
    /// </summary>
    public void AppendFormat<TArgument>(ReadOnlySpan<char> format, ReadOnlySpan<TArgument> args)
    {
        ReadOnlySpan<char> remaining = format;
        while (true)
        {
            int braceIndex = remaining.IndexOfAny('{', '}');
            if (braceIndex < 0)
            {
                Append(remaining);
                break;
            }

            if (braceIndex > 0)
            {
                Append(remaining[..braceIndex]);
            }

            remaining = remaining[braceIndex..];
            char c = remaining[0];
            remaining = remaining[1..];

            if (c == '}')
            {
                if (!remaining.IsEmpty && remaining[0] == '}')
                {
                    Append('}');
                    remaining = remaining[1..];
                    continue;
                }

                FormatError();
            }
            else if (!remaining.IsEmpty && remaining[0] == '{')
            {
                Append('{');
                remaining = remaining[1..];
                continue;
            }

            if (remaining.IsEmpty || (uint)(remaining[0] - '0') > 9)
            {
                FormatError();
            }

            uint index = (uint)(remaining[0] - '0');
            remaining = remaining[1..];
            while (!remaining.IsEmpty && (uint)(remaining[0] - '0') <= 9)
            {
                index = index * 10 + (uint)(remaining[0] - '0');
                remaining = remaining[1..];
            }

            if (index >= args.Length || remaining.IsEmpty)
            {
                FormatError();
            }

            int alignment = 0;
            if (remaining[0] == ',')
            {
                remaining = remaining[1..];
                bool negative = false;
                if (!remaining.IsEmpty && remaining[0] == '-')
                {
                    negative = true;
                    remaining = remaining[1..];
                }

                uint width = 0;
                bool gotDigit = false;
                while (!remaining.IsEmpty && (uint)(remaining[0] - '0') <= 9)
                {
                    width = width * 10 + (uint)(remaining[0] - '0');
                    remaining = remaining[1..];
                    gotDigit = true;
                    if (width >= WidthLimit)
                    {
                        FormatError();
                    }
                }

                if (!gotDigit || remaining.IsEmpty)
                {
                    FormatError();
                }

                alignment = negative ? -(int)width : (int)width;
            }

            ReadOnlySpan<char> itemFormat = default;
            if (remaining[0] == ':')
            {
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
}
