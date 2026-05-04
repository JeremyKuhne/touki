// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  Memory&lt;T&gt; / ReadOnlyMemory&lt;T&gt; extensions for trimming.
/// </summary>
/// <remarks>
///  <para>
///   Provided on .NET Framework to match the .NET surface area. On modern .NET these are part of
///   <see cref="System.MemoryExtensions"/>.
///  </para>
/// </remarks>
public static partial class MemoryExtensions
{
    extension<T>(Memory<T> memory) where T : IEquatable<T>?
    {
        /// <summary>
        ///  Removes all leading and trailing occurrences of <paramref name="trimElement"/> from the memory region.
        /// </summary>
        public Memory<T> Trim(T trimElement) =>
            memory[GetTrimRange(memory.Span, trimElement, trimStart: true, trimEnd: true)];

        /// <summary>
        ///  Removes all leading occurrences of <paramref name="trimElement"/> from the memory region.
        /// </summary>
        public Memory<T> TrimStart(T trimElement) =>
            memory[GetTrimRange(memory.Span, trimElement, trimStart: true, trimEnd: false)];

        /// <summary>
        ///  Removes all trailing occurrences of <paramref name="trimElement"/> from the memory region.
        /// </summary>
        public Memory<T> TrimEnd(T trimElement) =>
            memory[GetTrimRange(memory.Span, trimElement, trimStart: false, trimEnd: true)];

        /// <summary>
        ///  Removes all leading and trailing occurrences of any element in <paramref name="trimElements"/>.
        /// </summary>
        public Memory<T> Trim(ReadOnlySpan<T> trimElements) =>
            memory[GetTrimRange(memory.Span, trimElements, trimStart: true, trimEnd: true)];

        /// <summary>
        ///  Removes all leading occurrences of any element in <paramref name="trimElements"/>.
        /// </summary>
        public Memory<T> TrimStart(ReadOnlySpan<T> trimElements) =>
            memory[GetTrimRange(memory.Span, trimElements, trimStart: true, trimEnd: false)];

        /// <summary>
        ///  Removes all trailing occurrences of any element in <paramref name="trimElements"/>.
        /// </summary>
        public Memory<T> TrimEnd(ReadOnlySpan<T> trimElements) =>
            memory[GetTrimRange(memory.Span, trimElements, trimStart: false, trimEnd: true)];
    }

    extension<T>(ReadOnlyMemory<T> memory) where T : IEquatable<T>?
    {
        /// <inheritdoc cref="Trim{T}(Memory{T}, T)"/>
        public ReadOnlyMemory<T> Trim(T trimElement) =>
            memory[GetTrimRange(memory.Span, trimElement, trimStart: true, trimEnd: true)];

        /// <inheritdoc cref="TrimStart{T}(Memory{T}, T)"/>
        public ReadOnlyMemory<T> TrimStart(T trimElement) =>
            memory[GetTrimRange(memory.Span, trimElement, trimStart: true, trimEnd: false)];

        /// <inheritdoc cref="TrimEnd{T}(Memory{T}, T)"/>
        public ReadOnlyMemory<T> TrimEnd(T trimElement) =>
            memory[GetTrimRange(memory.Span, trimElement, trimStart: false, trimEnd: true)];

        /// <inheritdoc cref="Trim{T}(Memory{T}, ReadOnlySpan{T})"/>
        public ReadOnlyMemory<T> Trim(ReadOnlySpan<T> trimElements) =>
            memory[GetTrimRange(memory.Span, trimElements, trimStart: true, trimEnd: true)];

        /// <inheritdoc cref="TrimStart{T}(Memory{T}, ReadOnlySpan{T})"/>
        public ReadOnlyMemory<T> TrimStart(ReadOnlySpan<T> trimElements) =>
            memory[GetTrimRange(memory.Span, trimElements, trimStart: true, trimEnd: false)];

        /// <inheritdoc cref="TrimEnd{T}(Memory{T}, ReadOnlySpan{T})"/>
        public ReadOnlyMemory<T> TrimEnd(ReadOnlySpan<T> trimElements) =>
            memory[GetTrimRange(memory.Span, trimElements, trimStart: false, trimEnd: true)];
    }

    extension(Memory<char> memory)
    {
        /// <summary>
        ///  Removes all leading and trailing whitespace characters from a character memory region.
        /// </summary>
        public Memory<char> Trim() => memory[GetWhiteSpaceTrimRange(memory.Span, trimStart: true, trimEnd: true)];

        /// <summary>
        ///  Removes all leading whitespace characters from a character memory region.
        /// </summary>
        public Memory<char> TrimStart() => memory[GetWhiteSpaceTrimRange(memory.Span, trimStart: true, trimEnd: false)];

        /// <summary>
        ///  Removes all trailing whitespace characters from a character memory region.
        /// </summary>
        public Memory<char> TrimEnd() => memory[GetWhiteSpaceTrimRange(memory.Span, trimStart: false, trimEnd: true)];
    }

    extension(ReadOnlyMemory<char> memory)
    {
        /// <inheritdoc cref="Trim(Memory{char})"/>
        public ReadOnlyMemory<char> Trim() =>
            memory[GetWhiteSpaceTrimRange(memory.Span, trimStart: true, trimEnd: true)];

        /// <inheritdoc cref="TrimStart(Memory{char})"/>
        public ReadOnlyMemory<char> TrimStart() =>
            memory[GetWhiteSpaceTrimRange(memory.Span, trimStart: true, trimEnd: false)];

        /// <inheritdoc cref="TrimEnd(Memory{char})"/>
        public ReadOnlyMemory<char> TrimEnd() =>
            memory[GetWhiteSpaceTrimRange(memory.Span, trimStart: false, trimEnd: true)];
    }

    private static Range GetTrimRange<T>(ReadOnlySpan<T> span, T trimElement, bool trimStart, bool trimEnd)
        where T : IEquatable<T>?
    {
        int start = 0;
        if (trimStart)
        {
            for (; start < span.Length; start++)
            {
                if (!Equal(span[start], trimElement))
                {
                    break;
                }
            }
        }

        int end = span.Length - 1;
        if (trimEnd)
        {
            for (; end >= start; end--)
            {
                if (!Equal(span[end], trimElement))
                {
                    break;
                }
            }
        }

        return new Range(start, end + 1);
    }

    private static Range GetTrimRange<T>(ReadOnlySpan<T> span, ReadOnlySpan<T> trimElements, bool trimStart, bool trimEnd)
        where T : IEquatable<T>?
    {
        if (trimElements.IsEmpty)
        {
            return new Range(0, span.Length);
        }

        int start = 0;
        if (trimStart)
        {
            for (; start < span.Length; start++)
            {
                if (trimElements.IndexOf(span[start]) < 0)
                {
                    break;
                }
            }
        }

        int end = span.Length - 1;
        if (trimEnd)
        {
            for (; end >= start; end--)
            {
                if (trimElements.IndexOf(span[end]) < 0)
                {
                    break;
                }
            }
        }

        return new Range(start, end + 1);
    }

    private static Range GetWhiteSpaceTrimRange(ReadOnlySpan<char> span, bool trimStart, bool trimEnd)
    {
        int start = 0;
        if (trimStart)
        {
            for (; start < span.Length; start++)
            {
                if (!char.IsWhiteSpace(span[start]))
                {
                    break;
                }
            }
        }

        int end = span.Length - 1;
        if (trimEnd)
        {
            for (; end >= start; end--)
            {
                if (!char.IsWhiteSpace(span[end]))
                {
                    break;
                }
            }
        }

        return new Range(start, end + 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Equal<T>(T item, T value) where T : IEquatable<T>? =>
        value is null ? item is null : value.Equals(item);
}
