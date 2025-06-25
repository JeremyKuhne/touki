// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Span like string wrapper that allows using like a span but also storing as a field in a class.
/// </summary>
public readonly struct StringSegment : IEquatable<StringSegment>
{
    private readonly string _value;
    private readonly int _start;
    private readonly int _length;

    /// <summary>
    ///  Gets the length of the segment.
    /// </summary>
    public int Length => _length;

    /// <summary>
    ///  Gets a value indicating whether the segment is empty.
    /// </summary>
    public bool IsEmpty => _length == 0;

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringSegment"/> struct with an empty value.
    /// </summary>
    public StringSegment()
    {
        _value = string.Empty;
        _start = 0;
        _length = 0;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringSegment"/> struct.
    /// </summary>
    /// <param name="value">The string to wrap.</param>
    public StringSegment(string value)
    {
        _value = value;
        _start = 0;
        _length = value.Length;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringSegment"/> struct.
    /// </summary>
    /// <param name="value">The <see langword="string"/> to wrap.</param>
    /// <param name="start">The starting position in the <see langword="string"/>.</param>
    /// <param name="length">The length of the segment.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown if <paramref name="start"/> or <paramref name="length"/> are negative, or if
    ///  <paramref name="start"/> + <paramref name="length"/> exceeds the length of <paramref name="value"/>.
    /// </exception>
    public StringSegment(string value, int start, int length)
    {
        if (value is null)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start and length must be 0 for a null string");
        }

        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start cannot be negative");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length cannot be negative");
        }

        if (start > value.Length - length && (start != 0 || length != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Start and length exceed the bounds of the string");
        }

        _value = value;
        _start = start;
        _length = length;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringSegment"/> struct.
    /// </summary>
    /// <param name="value">The <see langword="string"/> to wrap.</param>
    /// <param name="start">The starting position in the <see langword="string"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown if <paramref name="start"/> is negative, or exceeds the length of <paramref name="value"/>.
    /// </exception>
    public StringSegment(string value, int start)
        : this(value, start, value.Length - start)
    {
    }

    /// <summary>
    ///  Gets the character at the specified index.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The character at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException">
    ///  Thrown if <paramref name="index"/> is negative or greater than or equal to <see cref="Length"/>.
    /// </exception>
    public char this[int index] => _value![_start + index];

    /// <summary>
    ///  Gets a segment from a range of this segment.
    /// </summary>
    /// <param name="range">The range.</param>
    /// <returns>A new <see cref="StringSegment"/> that represents the specified range.</returns>
    public StringSegment this[Range range]
    {
        get
        {
            (int offset, int length) = range.GetOffsetAndLength(_length);
            return new StringSegment(_value, _start + offset, length);
        }
    }

    /// <summary>
    ///  Slices the segment to create a new segment starting at a specified index.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <returns>A new <see cref="StringSegment"/> that starts at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="start"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    public StringSegment Slice(int start)
    {
        return (uint)start > (uint)_length
            ? throw new ArgumentOutOfRangeException(nameof(start))
            : new StringSegment(_value, _start + start, _length - start);
    }

    /// <summary>
    ///  Slices the segment to create a new segment starting at a specified index with the specified length.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The length of the new segment.</param>
    /// <returns>A new <see cref="StringSegment"/> that starts at the specified index and has the specified length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="start"/> or <paramref name="length"/> are negative, or if
    ///  <paramref name="start"/> + <paramref name="length"/> exceeds <see cref="Length"/>.
    /// </exception>
    public StringSegment Slice(int start, int length) =>
        (uint)start > (uint)_length || (uint)length > (uint)(_length - start)
            ? throw new ArgumentOutOfRangeException(nameof(start))
            : new StringSegment(_value, _start + start, length);

    /// <summary>
    ///  Returns the index of the given <see langword="char"/> or -1 if not found.
    /// </summary>
    public int IndexOf(char value)
    {
        int index = _value.IndexOf(value, _start, _length);
        return index < 0 ? -1 : index - _start;
    }

    /// <summary>
    ///  Creates a span over this segment.
    /// </summary>
    /// <returns>A span that represents the segment.</returns>
    public ReadOnlySpan<char> AsSpan() => _value.AsSpan(_start, _length);

    /// <summary>
    ///  Creates a span over a portion of this segment.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <returns>A span that represents the specified portion of the segment.</returns>
    public ReadOnlySpan<char> AsSpan(int start) => (uint)start > (uint)_length
        ? throw new ArgumentOutOfRangeException(nameof(start))
        : _value.AsSpan(_start + start, _length - start);

    /// <summary>
    ///  Creates a span over a portion of this segment.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The length of the span.</param>
    /// <returns>A span that represents the specified portion of the segment.</returns>
    public ReadOnlySpan<char> AsSpan(int start, int length) =>
        (uint)start > (uint)_length || (uint)length > (uint)(_length - start)
            ? throw new ArgumentOutOfRangeException(nameof(start))
            : _value.AsSpan(_start + start, length);

    /// <summary>
    ///  Returns a <see langword="string"/> that represents the current segment.
    /// </summary>
    /// <returns>A <see langword="string"/> that represents the current segment.</returns>
    public override string ToString() => _length == 0
        ? string.Empty
        : _start switch
        {
            0 when _length == _value.Length => _value,
            _ => _value.Substring(_start, _length)
        };

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> to a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
    /// </summary>
    /// <param name="segment">The segment to convert.</param>
    public static implicit operator ReadOnlySpan<char>(StringSegment segment) => segment.AsSpan();

    /// <summary>
    ///  Implicitly converts a <see cref="string"/> to a <see cref="StringSegment"/>.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    public static implicit operator StringSegment(string value) => new StringSegment(value);

    /// <summary>
    ///  Gets a value indicating whether two segments are equal.
    /// </summary>
    /// <param name="other">The segment to compare with.</param>
    /// <returns><see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(StringSegment other) => _length == other._length && AsSpan().SequenceEqual(other.AsSpan());

    /// <summary>
    ///  Gets a value indicating whether the segment equals the specified span.
    /// </summary>
    /// <param name="other">The span to compare with.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(ReadOnlySpan<char> other) => _length == other.Length && AsSpan().SequenceEqual(other);

    /// <summary>
    ///  Gets a value indicating whether the segment equals the specified <see langword="string"/>.
    /// </summary>
    /// <param name="other">The span to compare with.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(string other) => _length == other.Length && AsSpan().SequenceEqual(other.AsSpan());

    /// <summary>
    ///  Gets a value indicating whether the segment equals the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> if the segment equals the specified object; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => obj is StringSegment other && Equals(other);

    /// <summary>
    ///  Gets the hash code for the segment.
    /// </summary>
    /// <returns>A hash code for the segment.</returns>
    public override int GetHashCode()
    {
        ReadOnlySpan<char> span = AsSpan();
        int hash = 0;

        for (int i = 0; i < span.Length; i++)
        {
            hash = unchecked((hash * 31) + span[i]);
        }

        return hash;
    }

    /// <summary>
    ///  Gets a value indicating whether two segments are equal.
    /// </summary>
    /// <param name="left">The first segment.</param>
    /// <param name="right">The second segment.</param>
    /// <returns><see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(StringSegment left, StringSegment right) => left.Equals(right);

    /// <summary>
    ///  Gets a value indicating whether two segments are not equal.
    /// </summary>
    /// <param name="left">The first segment.</param>
    /// <param name="right">The second segment.</param>
    /// <returns><see langword="true"/> if the segments are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(StringSegment left, StringSegment right) => !left.Equals(right);
}
