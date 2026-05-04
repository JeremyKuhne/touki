// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  Polyfills for <see cref="Span{T}"/> and <see cref="ReadOnlySpan{T}"/>
///  instance APIs added in modern .NET, exposed to .NET Framework via C#
///  extension types.
/// </summary>
public static partial class SpanExtensions
{
    extension(ReadOnlySpan<char> span)
    {
        /// <summary>
        ///  Returns an enumeration of lines over the provided span.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   The line terminators recognized are <c>"\r\n"</c>, <c>"\n"</c>, <c>"\r"</c>,
        ///   <c>"\f"</c> (U+000C), <c>"\x85"</c> (NEL),
        ///   <c>"\u2028"</c> (LS), and <c>"\u2029"</c> (PS).
        ///  </para>
        /// </remarks>
        public SpanLineEnumerator EnumerateLines() => new(span);
    }

    extension(Span<char> span)
    {
        /// <inheritdoc cref="EnumerateLines(ReadOnlySpan{char})"/>
        public SpanLineEnumerator EnumerateLines() => new(span);
    }

    /// <summary>
    ///  Enumerates the lines of a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
    /// </summary>
    public ref struct SpanLineEnumerator
    {
        private ReadOnlySpan<char> _remaining;
        private ReadOnlySpan<char> _current;
        private bool _isEnumeratorActive;

        internal SpanLineEnumerator(ReadOnlySpan<char> buffer)
        {
            _remaining = buffer;
            _current = default;
            _isEnumeratorActive = true;
        }

        /// <summary>Gets the line at the current position of the enumerator.</summary>
        public readonly ReadOnlySpan<char> Current => _current;

        /// <summary>Returns this instance as an enumerator.</summary>
        public readonly SpanLineEnumerator GetEnumerator() => this;

        /// <summary>
        ///  Advances the enumerator to the next line of the span.
        /// </summary>
        public bool MoveNext()
        {
            if (!_isEnumeratorActive)
            {
                return false;
            }

            ReadOnlySpan<char> remaining = _remaining;
            int index = IndexOfNewLineChar(remaining, out int stride);
            if (index >= 0)
            {
                _current = remaining[..index];
                _remaining = remaining[(index + stride)..];
            }
            else
            {
                _current = remaining;
                _remaining = default;
                _isEnumeratorActive = false;
            }

            return true;
        }

        // Recognized line terminators (matches BCL MemoryExtensions.EnumerateLines):
        //   CR (\r), LF (\n), CRLF (\r\n), FF (\f U+000C),
        //   NEL (U+0085), LS (U+2028), PS (U+2029).
        private static int IndexOfNewLineChar(ReadOnlySpan<char> source, out int stride)
        {
            stride = default;
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c <= '\r')
                {
                    if (c is '\r' or '\n' or '\f')
                    {
                        if (c == '\r' && (uint)(i + 1) < (uint)source.Length && source[i + 1] == '\n')
                        {
                            stride = 2;
                            return i;
                        }

                        stride = 1;
                        return i;
                    }
                }
                else if (c is '\u0085' or '\u2028' or '\u2029')
                {
                    stride = 1;
                    return i;
                }
            }

            return -1;
        }
    }
}
