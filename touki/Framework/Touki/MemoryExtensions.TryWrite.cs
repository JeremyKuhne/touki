// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Adapted from dotnet/runtime MemoryExtensions.TryWriteInterpolatedStringHandler.cs (MIT licensed).

namespace Touki;

public static partial class MemoryExtensions
{
    /// <summary>
    ///  Writes the specified interpolated string to the character span.
    /// </summary>
    /// <param name="destination">The span into which to write the value.</param>
    /// <param name="handler">The interpolated string.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
    /// <returns><see langword="true"/> if the entire interpolated string fit; otherwise, <see langword="false"/>.</returns>
    public static bool TryWrite(
        this Span<char> destination,
        [InterpolatedStringHandlerArgument(nameof(destination))] ref TryWriteInterpolatedStringHandler handler,
        out int charsWritten)
    {
        if (handler._success)
        {
            charsWritten = handler._pos;
            return true;
        }

        charsWritten = 0;
        return false;
    }

    /// <summary>
    ///  Writes the specified interpolated string to the character span.
    /// </summary>
    public static bool TryWrite(
        this Span<char> destination,
        IFormatProvider? provider,
        [InterpolatedStringHandlerArgument(nameof(destination), nameof(provider))] ref TryWriteInterpolatedStringHandler handler,
        out int charsWritten) =>
        TryWrite(destination, ref handler, out charsWritten);

    /// <summary>
    ///  Provides a handler used by the language compiler to format interpolated strings into character spans.
    /// </summary>
    [InterpolatedStringHandler]
    public ref struct TryWriteInterpolatedStringHandler
    {
        private readonly Span<char> _destination;
        private readonly IFormatProvider? _provider;
        internal int _pos;
        internal bool _success;
        private readonly bool _hasCustomFormatter;

        /// <summary>Creates a handler used to write an interpolated string into a <see cref="Span{T}"/>.</summary>
        public TryWriteInterpolatedStringHandler(
            int literalLength,
            int formattedCount,
            Span<char> destination,
            out bool shouldAppend)
        {
            _destination = destination;
            _provider = null;
            _pos = 0;
            _success = shouldAppend = destination.Length >= literalLength;
            _hasCustomFormatter = false;
            _ = formattedCount;
        }

        /// <inheritdoc cref="TryWriteInterpolatedStringHandler(int, int, Span{char}, out bool)"/>
        public TryWriteInterpolatedStringHandler(
            int literalLength,
            int formattedCount,
            Span<char> destination,
            IFormatProvider? provider,
            out bool shouldAppend)
        {
            _destination = destination;
            _provider = provider;
            _pos = 0;
            _success = shouldAppend = destination.Length >= literalLength;
            _hasCustomFormatter = provider is not null && HasCustomFormatter(provider);
            _ = formattedCount;
        }

        /// <summary>Writes the specified string to the handler.</summary>
        public bool AppendLiteral(string value)
        {
            if (value.AsSpan().TryCopyTo(_destination[_pos..]))
            {
                _pos += value.Length;
                return true;
            }

            return Fail();
        }

        /// <summary>Writes the specified value to the handler.</summary>
        public bool AppendFormatted<T>(T value)
        {
            if (_hasCustomFormatter)
            {
                return AppendCustomFormatter(value, format: null);
            }

            string? s;
            if (value is IFormattable)
            {
                if (value is ISpanFormattable sf)
                {
                    if (sf.TryFormat(_destination[_pos..], out int written, default, _provider))
                    {
                        _pos += written;
                        return true;
                    }

                    return Fail();
                }

                s = ((IFormattable)value).ToString(format: null, _provider);
            }
            else
            {
                s = value?.ToString();
            }

            return s is null || AppendStringDirect(s);
        }

        /// <summary>Writes the specified value to the handler with the specified format.</summary>
        public bool AppendFormatted<T>(T value, string? format)
        {
            if (_hasCustomFormatter)
            {
                return AppendCustomFormatter(value, format);
            }

            string? s;
            if (value is IFormattable)
            {
                if (value is ISpanFormattable sf)
                {
                    if (sf.TryFormat(_destination[_pos..], out int written, format.AsSpan(), _provider))
                    {
                        _pos += written;
                        return true;
                    }

                    return Fail();
                }

                s = ((IFormattable)value).ToString(format, _provider);
            }
            else
            {
                s = value?.ToString();
            }

            return s is null || AppendStringDirect(s);
        }

        /// <summary>Writes the specified value to the handler with the specified alignment.</summary>
        public bool AppendFormatted<T>(T value, int alignment) => AppendFormatted(value, alignment, format: null);

        /// <summary>Writes the specified value to the handler with the specified alignment and format.</summary>
        public bool AppendFormatted<T>(T value, int alignment, string? format)
        {
            int startingPos = _pos;
            if (AppendFormatted(value, format))
            {
                return alignment == 0 || TryAppendOrInsertAlignmentIfNeeded(startingPos, alignment);
            }

            return Fail();
        }

        /// <summary>Writes the specified character span to the handler.</summary>
        public bool AppendFormatted(scoped ReadOnlySpan<char> value)
        {
            if (value.TryCopyTo(_destination[_pos..]))
            {
                _pos += value.Length;
                return true;
            }

            return Fail();
        }

        /// <summary>Writes the specified character span to the handler with alignment and format.</summary>
        public bool AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
        {
            _ = format;
            bool leftAlign = false;
            if (alignment < 0)
            {
                leftAlign = true;
                alignment = -alignment;
            }

            int padding = alignment - value.Length;
            if (padding <= 0)
            {
                return AppendFormatted(value);
            }

            if (_destination.Length - _pos < value.Length + padding)
            {
                return Fail();
            }

            if (leftAlign)
            {
                value.CopyTo(_destination[_pos..]);
                _pos += value.Length;
                _destination.Slice(_pos, padding).Fill(' ');
                _pos += padding;
            }
            else
            {
                _destination.Slice(_pos, padding).Fill(' ');
                _pos += padding;
                value.CopyTo(_destination[_pos..]);
                _pos += value.Length;
            }

            return true;
        }

        /// <summary>Writes the specified string to the handler.</summary>
        public bool AppendFormatted(string? value)
        {
            if (!_hasCustomFormatter
                && value is not null
                && value.AsSpan().TryCopyTo(_destination[_pos..]))
            {
                _pos += value.Length;
                return true;
            }

            return AppendFormattedSlow(value);
        }

        /// <summary>Writes the specified string to the handler with alignment and format.</summary>
        public bool AppendFormatted(string? value, int alignment = 0, string? format = null) =>
            AppendFormatted<string?>(value, alignment, format);

        /// <summary>Writes the specified object to the handler.</summary>
        public bool AppendFormatted(object? value, int alignment = 0, string? format = null) =>
            AppendFormatted<object?>(value, alignment, format);

        private bool AppendFormattedSlow(string? value)
        {
            if (_hasCustomFormatter)
            {
                return AppendCustomFormatter(value, format: null);
            }

            if (value is null)
            {
                return true;
            }

            return AppendStringDirect(value);
        }

        private bool AppendStringDirect(string value)
        {
            if (value.AsSpan().TryCopyTo(_destination[_pos..]))
            {
                _pos += value.Length;
                return true;
            }

            return Fail();
        }

        private bool AppendCustomFormatter<T>(T value, string? format)
        {
            ICustomFormatter? formatter = (ICustomFormatter?)_provider?.GetFormat(typeof(ICustomFormatter));
            Debug.Assert(formatter is not null);

            string? formatted = formatter!.Format(format, value, _provider);
            return formatted is null || AppendStringDirect(formatted);
        }

        private bool TryAppendOrInsertAlignmentIfNeeded(int startingPos, int alignment)
        {
            int charsWritten = _pos - startingPos;

            bool leftAlign = false;
            if (alignment < 0)
            {
                leftAlign = true;
                alignment = -alignment;
            }

            int paddingRequired = alignment - charsWritten;
            if (paddingRequired <= 0)
            {
                return true;
            }

            if (_destination.Length - _pos < paddingRequired)
            {
                return Fail();
            }

            if (leftAlign)
            {
                _destination.Slice(_pos, paddingRequired).Fill(' ');
            }
            else
            {
                _destination.Slice(startingPos, charsWritten).CopyTo(_destination[(startingPos + paddingRequired)..]);
                _destination.Slice(startingPos, paddingRequired).Fill(' ');
            }

            _pos += paddingRequired;
            return true;
        }

        private bool Fail()
        {
            _success = false;
            return false;
        }

        private static bool HasCustomFormatter(IFormatProvider provider)
        {
            Debug.Assert(provider is not null);
            return provider!.GetType() != typeof(System.Globalization.CultureInfo)
                && provider.GetFormat(typeof(ICustomFormatter)) is not null;
        }
    }
}
