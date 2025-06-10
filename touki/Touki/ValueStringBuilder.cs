// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original source: .NET Runtime and Windows Forms source code.

using System.Buffers;

namespace Touki;

/// <summary>
///  String builder struct that can be used to build strings in a memory-efficient way.
///  Allows using stack space for small strings.
/// </summary>
/// <remarks>
///  <para>
///   This class can also be used as an interpolated string handler, which allows you to get efficiently
///   formatted strings in your helper methods that take this as a ref parameter.
///  </para>
///  <para>
///   <code>
///    <![CDATA[
///      string Format(ref ValueStringBuilder builder)
///    ]]>
///   </code>
///  </para>
/// </remarks>
[InterpolatedStringHandler]
public ref partial struct ValueStringBuilder
{
    private const int GuessedLengthPerHole = 11;
    private const int MinimumArrayPoolLength = 256;

    private char[]? _arrayToReturnToPool;

    private Span<char> _chars;
    private int _pos;

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct for use with interpolated strings.
    /// </summary>
    /// <param name="literalLength">The length of literal content in the interpolated string.</param>
    /// <param name="formattedCount">The number of formatted holes in the interpolated string.</param>
    public ValueStringBuilder(int literalLength, int formattedCount)
    {
        _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(
            Math.Max(MinimumArrayPoolLength, literalLength + (GuessedLengthPerHole * formattedCount)));

        _chars = _arrayToReturnToPool;
        _pos = 0;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct with an initial buffer.
    /// </summary>
    /// <param name="initialBuffer">The initial buffer to use for the string builder.</param>
    public ValueStringBuilder(Span<char> initialBuffer)
    {
        _arrayToReturnToPool = null;
        _chars = initialBuffer;
        _pos = 0;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity for the string builder.</param>
    public ValueStringBuilder(int initialCapacity)
    {
        _arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
        _chars = _arrayToReturnToPool;
        _pos = 0;
    }

    /// <summary>
    ///  Gets or sets the current length of the string builder.
    /// </summary>
    /// <value>The number of characters currently in the string builder.</value>
    public int Length
    {
        readonly get => _pos;
        set
        {
            Debug.Assert(value >= 0);
            Debug.Assert(value <= _chars.Length);
            _pos = value;
        }
    }

    /// <summary>
    ///  Gets the maximum number of characters that can be contained in the memory allocated by the current instance.
    /// </summary>
    /// <value>The capacity of the current instance.</value>
    public readonly int Capacity => _chars.Length;

    /// <summary>
    ///  Ensures that the capacity of this builder is at least the specified value.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <remarks>
    ///  If the current capacity is less than the <paramref name="capacity"/> parameter,
    ///  the capacity is increased by calling the <see cref="Grow"/> method.
    /// </remarks>
    public void EnsureCapacity(int capacity)
    {
        // This is not expected to be called this with negative capacity
        Debug.Assert(capacity >= 0);

        // If the caller has a bug and calls this with negative capacity, make sure to call Grow to throw an exception.
        if ((uint)capacity > (uint)_chars.Length)
            Grow(capacity - _pos);
    }

    /// <summary>
    ///  Get a pinnable reference to the builder. Does not ensure there is a null char after <see cref="Length"/>
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This overload is pattern matched in the C# 7.3+ compiler so you can omit
    ///   the explicit method call, and write eg "fixed (char* c = builder)"
    ///  </para>
    /// </remarks>
    public readonly ref char GetPinnableReference() => ref MemoryMarshal.GetReference(_chars);

    /// <summary>
    ///  Get a pinnable reference to the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ref char GetPinnableReference(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }

        return ref MemoryMarshal.GetReference(_chars);
    }

    /// <summary>
    ///  Gets or sets the character at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the character to get or set.</param>
    /// <value>The character at the specified index.</value>
    /// <returns>A reference to the character at the specified index.</returns>
    public ref char this[int index]
    {
        get
        {
            Debug.Assert(index < _pos);
            return ref _chars[index];
        }
    }

    /// <summary>
    ///  Converts the value of this builder to a <see cref="string"/>.
    /// </summary>
    /// <returns>A string representation of the value of this builder.</returns>
    public override readonly string ToString() => _chars[.._pos].ToString();

    /// <summary>
    ///  Converts the value of this builder to a <see cref="string"/> and clears the builder.
    /// </summary>
    /// <returns>A string representation of the value of this builder.</returns>
    /// <remarks>
    ///  This method disposes the builder after converting to string, making it unusable afterwards.
    /// </remarks>
    public string ToStringAndClear()
    {
        string s = ToString();
        Dispose();
        return s;
    }

    /// <summary>
    ///  Returns the underlying storage of the builder.
    /// </summary>
    public readonly Span<char> RawChars => _chars;

    /// <summary>
    ///  Returns a span around the contents of the builder.
    /// </summary>
    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    public ReadOnlySpan<char> AsSpan(bool terminate)
    {
        if (terminate)
        {
            EnsureCapacity(Length + 1);
            _chars[Length] = '\0';
        }

        return _chars[.._pos];
    }

    /// <summary>
    ///  Returns a read-only span around the contents of the builder.
    /// </summary>
    /// <returns>A read-only span representing the current contents of the builder.</returns>
    public readonly ReadOnlySpan<char> AsSpan() => _chars[.._pos];

    /// <summary>
    ///  Returns a read-only span around the contents of the builder starting from the specified index.
    /// </summary>
    /// <param name="start">The zero-based starting index.</param>
    /// <returns>A read-only span representing a portion of the builder contents.</returns>
    public readonly ReadOnlySpan<char> AsSpan(int start) => _chars[start.._pos];

    /// <summary>
    ///  Returns a read-only span around the specified portion of the builder contents.
    /// </summary>
    /// <param name="start">The zero-based starting index.</param>
    /// <param name="length">The number of characters to include in the span.</param>
    /// <returns>A read-only span representing the specified portion of the builder contents.</returns>
    public readonly ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

    /// <summary>
    ///  Attempts to copy the contents of this builder to the destination span.
    /// </summary>
    /// <param name="destination">The destination span to copy the contents to.</param>
    /// <param name="charsWritten">When this method returns, contains the number of characters written to the destination.</param>
    /// <returns><see langword="true"/> if the copy operation was successful; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    ///  This method disposes the builder after attempting the copy operation, making it unusable afterwards.
    /// </remarks>
    public bool TryCopyTo(Span<char> destination, out int charsWritten)
    {
        if (_chars[.._pos].TryCopyTo(destination))
        {
            charsWritten = _pos;
            Dispose();
            return true;
        }
        else
        {
            charsWritten = 0;
            Dispose();
            return false;
        }
    }

    /// <summary>
    ///  Inserts the specified character a specified number of times at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the character.</param>
    /// <param name="value">The character to insert.</param>
    /// <param name="count">The number of times to insert the character.</param>
    public void Insert(int index, char value, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        _chars.Slice(index, count).Fill(value);
        _pos += count;
    }

    /// <summary>
    ///  Inserts the specified string at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which to insert the string.</param>
    /// <param name="s">The string to insert. If <see langword="null"/>, this method does nothing.</param>
    public void Insert(int index, string? s)
    {
        if (s is null)
        {
            return;
        }

        int count = s.Length;

        if (_pos > (_chars.Length - count))
        {
            Grow(count);
        }

        int remaining = _pos - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        s.CopyTo(_chars[index..]);
        _pos += count;
    }

    /// <summary>
    ///  Appends the specified character to the end of this builder.
    /// </summary>
    /// <param name="c">The character to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        int pos = _pos;
        if ((uint)pos < (uint)_chars.Length)
        {
            _chars[pos] = c;
            _pos = pos + 1;
        }
        else
        {
            GrowAndAppend(c);
        }
    }

    /// <summary>
    ///  Append a string to the builder. If the string is <see langword="null"/>, this method does nothing.
    /// </summary>
    /// <devdoc>
    ///  Name must be AppendLiteral to work with interpolated strings.
    /// </devdoc>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral(string? s)
    {
        if (s is null)
        {
            return;
        }

        int pos = _pos;
        if (s.Length == 1 && (uint)pos < (uint)_chars.Length)
        {
            // Very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
            _chars[pos] = s[0];
            _pos = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

    private void AppendSlow(string s)
    {
        int pos = _pos;
        if (pos > _chars.Length - s.Length)
        {
            Grow(s.Length);
        }

        s.CopyTo(_chars[pos..]);
        _pos += s.Length;
    }

    /// <summary>
    ///  Appends the formatted representation of a value that implements <see cref="ISpanFormattable"/> to this builder.
    /// </summary>
    /// <typeparam name="TFormattable">The type of the value to format, which must implement <see cref="ISpanFormattable"/>.</typeparam>
    /// <param name="value">The value to format and append.</param>
    public void AppendFormatted<TFormattable>(TFormattable value) where TFormattable : ISpanFormattable
    {
        int charsWritten;

        while (!value.TryFormat(_chars[_pos..], out charsWritten, format: default, provider: default))
        {
            Grow(1);
        }

        _pos += charsWritten;
        return;
    }

    /// <summary>
    ///  Appends the specified read-only span of characters to this builder.
    /// </summary>
    /// <param name="value">The span of characters to append.</param>
    public void AppendFormatted(ReadOnlySpan<char> value) => Append(value);

    /// <summary>
    ///  Appends the formatted representation of an integer to this builder.
    /// </summary>
    /// <param name="value">The value to format and append.</param>
    public void AppendFormatted(int value)
    {
#if NET
        AppendFormatted<int>(value);
#else
        // This at least avoids boxing for the common case of formatting an int. This could be improved further
        // by writing (or porting) an int formatting method that writes directly to the builder.
        AppendFormatted(value.ToString());
#endif
    }

    /// <summary>
    ///  Appends the formatted representation of an integer to this builder.
    /// </summary>
    /// <param name="value">The value to format and append.</param>
    public void AppendFormatted(long value)
    {
#if NET
        AppendFormatted<long>(value);
#else
        // This at least avoids boxing for the common case of formatting an int. This could be improved further
        // by writing (or porting) an int formatting method that writes directly to the builder.
        AppendFormatted(value.ToString());
#endif
    }

    /// <summary>
    ///  Appends the specified string to this builder.
    /// </summary>
    /// <param name="value">The string to append. If <see langword="null"/>, this method does nothing.</param>
    public void AppendFormatted(string? value) => Append(value.AsSpan());

    /// <summary>
    ///  Appends the string representation of the specified object to this builder.
    /// </summary>
    /// <param name="value">The object whose string representation is to be appended. If <see langword="null"/>, this method does nothing.</param>
    public void AppendFormatted(object? value) => AppendLiteral(value?.ToString());

    /// <summary>
    ///  Appends the specified character a specified number of times to the end of this builder.
    /// </summary>
    /// <param name="c">The character to append.</param>
    /// <param name="count">The number of times to append the character.</param>
    public void Append(char c, int count)
    {
        if (_pos > _chars.Length - count)
        {
            Grow(count);
        }

        Span<char> dst = _chars.Slice(_pos, count);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }

        _pos += count;
    }

    /// <summary>
    ///  Appends characters from a pointer to the end of this builder.
    /// </summary>
    /// <param name="value">A pointer to the characters to append.</param>
    /// <param name="length">The number of characters to append.</param>
    public unsafe void Append(char* value, int length)
    {
        int pos = _pos;
        if (pos > _chars.Length - length)
        {
            Grow(length);
        }

        Span<char> dst = _chars.Slice(_pos, length);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = *value++;
        }

        _pos += length;
    }

    /// <summary>
    ///  Appends the specified read-only span of characters to the end of this builder.
    /// </summary>
    /// <param name="value">The span of characters to append.</param>
    public void Append(ReadOnlySpan<char> value)
    {
        int pos = _pos;
        if (pos > _chars.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(_chars[_pos..]);
        _pos += value.Length;
    }

    /// <inheritdoc cref="Append(ReadOnlySpan{char})"/>
    public void Append(string value) => Append(value.AsSpan());

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    ///  Resize the internal buffer either by doubling current buffer size or
    ///  by adding <paramref name="additionalCapacityBeyondPos"/> to
    ///  <see cref="_pos"/> whichever is greater.
    /// </summary>
    /// <param name="additionalCapacityBeyondPos">
    ///  Number of chars requested beyond current position.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_pos > _chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

        const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

        // Increase to at least the required size (_pos + additionalCapacityBeyondPos), but try
        // to double the size if possible, bounding the doubling to not go beyond the max array length.
        int newCapacity = (int)Math.Max(
            (uint)(_pos + additionalCapacityBeyondPos),
            Math.Min((uint)_chars.Length * 2, ArrayMaxLength));

        // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
        // This could also go negative if the actual required length wraps around.
        char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

        _chars[.._pos].CopyTo(poolArray);

        char[]? toReturn = _arrayToReturnToPool;
        _chars = _arrayToReturnToPool = poolArray;
        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }

    /// <summary>
    ///  Releases all resources used by the <see cref="ValueStringBuilder"/>.
    /// </summary>
    /// <remarks>
    ///  This method returns any rented array back to the array pool and resets the builder to its default state.
    ///  After calling this method, the builder should not be used again.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        char[]? toReturn = _arrayToReturnToPool;
        this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
        if (toReturn is not null)
        {
            ArrayPool<char>.Shared.Return(toReturn);
        }
    }
}
