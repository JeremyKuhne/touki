// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Original source: .NET Runtime and Windows Forms source code.

using System.Buffers;
using System.Globalization;

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

    // We're using a byte array here to allow directly copying into Stream on .NET Framework. Byte arrays are always
    // going to be allocated on pointer size boundaries so they're safe for reinterpreting as Span<char>. With a
    // byte[] backing array we can call the byte [] virtual on Stream without worrying about type safety issues.
    //
    // We may also be able to use this with GREAT caution via Unsafe.As<byte[], char[]> as a last result. Doing so would
    // allow a buffer overrun as code would be using the prefixed array info for length and indices, so we would have
    // to be *extremely* careful. If any code would check the length of the array it would get the byte length, not
    // the char length.
    private byte[]? _arrayToReturnToPool;

    private Span<char> _chars;
    private int _position;

    // Optional provider to pass to IFormattable.ToString or ISpanFormattable.TryFormat calls.
    private readonly IFormatProvider? _formatProvider;

    // Custom formatters are very rare.  We want to support them, but it's ok if we make them more expensive
    // in order to make them as pay-for-play as possible.  So, we avoid adding another reference type field
    // to reduce the size of the handler and to reduce required zero'ing, by only storing whether the provider
    // provides a formatter, rather than actually storing the formatter.  This in turn means, if there is a
    // formatter, we pay for the extra interface call on each AppendFormatted that needs it.
    private readonly bool _hasCustomFormatter;

    /// <inheritdoc cref="ValueStringBuilder(int, int, IFormatProvider?, Span{char})"/>
    public ValueStringBuilder(int literalLength, int formattedCount) : this(literalLength, formattedCount, null)
    {
    }

    /// <inheritdoc cref="ValueStringBuilder(int, int, IFormatProvider?, Span{char})"/>
    public ValueStringBuilder(int literalLength, int formattedCount, IFormatProvider? provider)
        : this(Math.Max(MinimumArrayPoolLength, literalLength + (GuessedLengthPerHole * formattedCount)), provider)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct for use with interpolated strings.
    /// </summary>
    /// <param name="literalLength">The length of literal content in the interpolated string.</param>
    /// <param name="formattedCount">The number of formatted holes in the interpolated string.</param>
    /// <param name="provider">An optional format provider to use for formatting.</param>
    /// <param name="initialBuffer">The initial buffer to use.</param>
    public ValueStringBuilder(int literalLength, int formattedCount, IFormatProvider? provider, Span<char> initialBuffer)
        : this(initialBuffer, provider)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct with an initial buffer.
    /// </summary>
    /// <param name="initialBuffer">The initial buffer to use for the string builder.</param>
    public ValueStringBuilder(Span<char> initialBuffer, IFormatProvider? provider = null)
    {
        _arrayToReturnToPool = null;
        _chars = initialBuffer;
        _position = 0;
        _formatProvider = provider;
        _hasCustomFormatter = provider is not null && HasCustomFormatter(provider);
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ValueStringBuilder"/> struct with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity for the string builder.</param>
    public ValueStringBuilder(int initialCapacity, IFormatProvider? provider = null)
    {
        _arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(initialCapacity * sizeof(char));
        _chars = MemoryMarshal.Cast<byte, char>(_arrayToReturnToPool);
        _position = 0;
        _formatProvider = provider;
        _hasCustomFormatter = provider is not null && HasCustomFormatter(provider);
    }

    /// <summary>Gets whether the provider provides a custom formatter.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)] // only used in a few hot path call sites
    internal static bool HasCustomFormatter(IFormatProvider provider)
    {
        Debug.Assert(provider is not CultureInfo
            || provider.GetFormat(typeof(ICustomFormatter)) is null, "Expected CultureInfo to not provide a custom formatter");

        return
            // optimization to avoid GetFormat in the majority case
            provider.GetType() != typeof(CultureInfo)
            && provider.GetFormat(typeof(ICustomFormatter)) != null;
    }

    /// <summary>
    ///  Gets or sets the current length of the string builder.
    /// </summary>
    /// <value>The number of characters currently in the string builder.</value>
    public int Length
    {
        readonly get => _position;
        set
        {
            // Do not free if set to 0. This allows in-place building of strings.
            Debug.Assert(value >= 0);
            Debug.Assert(value <= _chars.Length);
            _position = value;
        }
    }

    /// <summary>
    ///  Clears the contents of the string builder, resetting its length to zero.
    /// </summary>
    public void Clear()
    {
        // Reset the position to 0, but do not clear the underlying array to allow in-place building.
        _position = 0;
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
            Grow(capacity - _position);
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
            Debug.Assert(index < _position);
            return ref _chars[index];
        }
    }

    /// <summary>
    ///  Converts the value of this builder to a <see cref="string"/>.
    /// </summary>
    /// <returns>A string representation of the value of this builder.</returns>
    public override readonly string ToString() => _chars[.._position].ToString();

    /// <summary>
    ///  Converts the value of this builder to a <see cref="string"/> and clears the builder.
    /// </summary>
    /// <returns>A string representation of the value of this builder.</returns>
    /// <remarks>
    ///  This method disposes the builder after converting to string, making it unusable afterwards.
    /// </remarks>
    public string ToStringAndDispose()
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

        return _chars[.._position];
    }

    /// <summary>
    ///  Returns a read-only span around the contents of the builder.
    /// </summary>
    /// <returns>A read-only span representing the current contents of the builder.</returns>
    public readonly ReadOnlySpan<char> AsSpan() => _chars[.._position];

    /// <summary>
    ///  Returns a read-only span around the contents of the builder starting from the specified index.
    /// </summary>
    /// <param name="start">The zero-based starting index.</param>
    /// <returns>A read-only span representing a portion of the builder contents.</returns>
    public readonly ReadOnlySpan<char> AsSpan(int start) => _chars[start.._position];

    /// <summary>
    ///  Returns a read-only span around the specified portion of the builder contents.
    /// </summary>
    /// <param name="start">The zero-based starting index.</param>
    /// <param name="length">The number of characters to include in the span.</param>
    /// <returns>A read-only span representing the specified portion of the builder contents.</returns>
    public readonly ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

    /// <summary>
    ///  Returns a span of the requested length at the current position in the builder that can be
    ///  used to directly append characters.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<char> AppendSpan(int length)
    {
        int origPos = _position;
        if (origPos > _chars.Length - length)
        {
            Grow(length);
        }

        _position = origPos + length;
        return _chars.Slice(origPos, length);
    }

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
        if (_chars[.._position].TryCopyTo(destination))
        {
            charsWritten = _position;
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
        if (_position > _chars.Length - count)
        {
            Grow(count);
        }

        int remaining = _position - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        _chars.Slice(index, count).Fill(value);
        _position += count;
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

        if (_position > (_chars.Length - count))
        {
            Grow(count);
        }

        int remaining = _position - index;
        _chars.Slice(index, remaining).CopyTo(_chars[(index + count)..]);
        s.CopyTo(_chars[index..]);
        _position += count;
    }

    /// <summary>
    ///  Appends the specified character to the end of this builder.
    /// </summary>
    /// <param name="c">The character to append.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(char c)
    {
        int pos = _position;
        if ((uint)pos < (uint)_chars.Length)
        {
            _chars[pos] = c;
            _position = pos + 1;
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

        int pos = _position;
        if (s.Length == 1 && (uint)pos < (uint)_chars.Length)
        {
            // Very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
            _chars[pos] = s[0];
            _position = pos + 1;
        }
        else
        {
            AppendSlow(s);
        }
    }

    private void AppendSlow(string s)
    {
        int pos = _position;
        if (pos > _chars.Length - s.Length)
        {
            Grow(s.Length);
        }

        s.CopyTo(_chars[pos..]);
        _position += s.Length;
    }

    /// <summary>
    ///  Appends the specified character a specified number of times to the end of this builder.
    /// </summary>
    /// <param name="c">The character to append.</param>
    /// <param name="count">The number of times to append the character.</param>
    public void Append(char c, int count)
    {
        if (_position > _chars.Length - count)
        {
            Grow(count);
        }

        Span<char> dst = _chars.Slice(_position, count);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = c;
        }

        _position += count;
    }

    /// <summary>
    ///  Appends characters from a pointer to the end of this builder.
    /// </summary>
    /// <param name="value">A pointer to the characters to append.</param>
    /// <param name="length">The number of characters to append.</param>
    public unsafe void Append(char* value, int length)
    {
        int pos = _position;
        if (pos > _chars.Length - length)
        {
            Grow(length);
        }

        Span<char> dst = _chars.Slice(_position, length);
        for (int i = 0; i < dst.Length; i++)
        {
            dst[i] = *value++;
        }

        _position += length;
    }

    /// <summary>
    ///  Appends the specified read-only span of characters to the end of this builder.
    /// </summary>
    /// <param name="value">The span of characters to append.</param>
    public void Append(scoped ReadOnlySpan<char> value)
    {
        int pos = _position;
        if (pos > _chars.Length - value.Length)
        {
            Grow(value.Length);
        }

        value.CopyTo(_chars[_position..]);
        _position += value.Length;
    }

    /// <inheritdoc cref="Append(ReadOnlySpan{char})"/>
    public void Append(string value) => Append(value.AsSpan());

    /// <inheritdoc cref="Append(ReadOnlySpan{char})"/>
    public void Append(StringSegment value) => Append(value.AsSpan());

    /// <summary>
    ///  Replace all occurrences of a character in the segment with another character.
    /// </summary>
    /// <returns>
    ///  The new <see cref="StringSegment"/> with the specified character replaced.
    /// </returns>
    public readonly void Replace(char oldValue, char newValue)
    {
        if (_position == 0 || oldValue == newValue)
        {
            return;
        }

        _chars[.._position].Replace(oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void GrowAndAppend(char c)
    {
        Grow(1);
        Append(c);
    }

    /// <summary>
    ///  Resize the internal buffer either by doubling current buffer size or
    ///  by adding <paramref name="additionalCapacityBeyondPos"/> to
    ///  <see cref="_position"/> whichever is greater.
    /// </summary>
    /// <param name="additionalCapacityBeyondPos">
    ///  Number of chars requested beyond current position.
    /// </param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int additionalCapacityBeyondPos)
    {
        Debug.Assert(additionalCapacityBeyondPos > 0);
        Debug.Assert(_position > _chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

        const uint ArrayMaxLength = 0x7FFFFFC7; // same as Array.MaxLength

        // Increase to at least the required size (_pos + additionalCapacityBeyondPos), but try
        // to double the size if possible, bounding the doubling to not go beyond the max array length.
        int newCapacity = (int)Math.Max(
            (uint)(_position + additionalCapacityBeyondPos),
            Math.Min((uint)_chars.Length * 2, ArrayMaxLength));

        // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
        // This could also go negative if the actual required length wraps around.
        byte[] poolArray = ArrayPool<byte>.Shared.Rent(newCapacity * sizeof(char));

        _chars[.._position].CopyTo(MemoryMarshal.Cast<byte, char>(poolArray));

        byte[]? toReturn = _arrayToReturnToPool;
        _chars = MemoryMarshal.Cast<byte, char>(poolArray);
        _arrayToReturnToPool = poolArray;
        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }

    private void DoubleRemaining() => Grow((_chars.Length - _position) * 2);

    private void EnsureRemaining(int length)
    {
        if (_position > _chars.Length - length)
        {
            Grow(length);
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
        byte[]? toReturn = _arrayToReturnToPool;

        // Clear the fields to prevent accidental reuse
        _arrayToReturnToPool = null;
        _chars = default;

        if (toReturn is not null)
        {
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }

    /// <summary>
    ///  Implicitly converts a <see cref="ValueStringBuilder"/> to a <see cref="string"/>.
    /// </summary>
    public static implicit operator ReadOnlySpan<char>(ValueStringBuilder builder) => builder._chars[..builder._position];

#pragma warning disable IDE0251 // Member can be made readonly

    /// <summary>
    ///  Writes the string to the specified stream.
    /// </summary>
    public void CopyTo(Stream stream)
    {
        if (_chars[.._position].IsEmpty)
        {
            return;
        }

        // Write the contents of the builder to the stream.
#if NET
        stream.Write(MemoryMarshal.Cast<char, byte>(_chars[.._position]));
#else
        if (_arrayToReturnToPool is null)
        {
            // If we don't have a rented array, we need to rent one.
            EnsureCapacity(_chars.Length + 1);
        }

        // If we have a rented array, write it out directly
        stream.Write(_arrayToReturnToPool, 0, _position * sizeof(char));
#endif
    }

    /// <summary>
    ///  Writes the string to the specified <see cref="System.IO.StreamWriter"/>.
    /// </summary>
    public void CopyTo<T>(T writer) where T : System.IO.StreamWriter
    {
        if (_chars[.._position].IsEmpty)
        {
            return;
        }

#if NET
        writer.Write(AsSpan());
#else
        if (typeof(T) != typeof(System.IO.StreamWriter))
        {
            throw new InvalidOperationException("Derived classes are not supported for safety.");
        }

        if (_arrayToReturnToPool is null)
        {
            // If we don't have a rented array, we need to rent one.
            EnsureCapacity(_chars.Length + 1);
        }

        // More details are above with _arrayToReturnToPool. This is a dangerous cast as the Length will be twice as
        // long as the actual length when cast to char[], but StreamWriter.Write doesn't index out of the given
        // bounds. One other option here would be to reflect all of the internals necessary to replicate, but that
        // would be costly.

        // Also not a terribly crazy idea to port the .NET implementation of StreamWriter, trim it down and seal it.

        char[] chars = Unsafe.As<byte[], char[]>(ref _arrayToReturnToPool!);
        writer.Write(chars, 0, _position);
#endif
    }

#pragma warning restore IDE0251
}
