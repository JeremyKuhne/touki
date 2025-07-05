// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.ComponentModel;

namespace Touki;

/// <summary>
///  Span like <see langword="string"/> section wrapper that allows using like a span but also storing as a field in a class.
/// </summary>
/// <remarks>
///  <para>
///   The segment is immutable and does not allocate a new <see langword="string"/> unless necessary.
///  </para>
///  <para>
///   <see cref="ReadOnlyMemory{T}"/> (created via <see cref="MemoryExtensions.AsMemory(string?)"/> provides
///   some functionality similar to <see cref="StringSegment"/>, but it is not as optimized for <see langword="string"/>
///   operations. This struct leverages <see langword="string"/> methods to get good performance on .NET Framework.
///  </para>
///  <para>
///   Microsoft.Extensions.Primitives also provides a <see cref="StringSegment"/> struct, but it is not as optimized
///   as this one and does not have as much functionality.
///  </para>
/// </remarks>
public readonly struct StringSegment : IEquatable<StringSegment>, IEquatable<string>
{
    private readonly string _value;
    private readonly int _startIndex;
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
        _startIndex = 0;
        _length = 0;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringSegment"/> struct.
    /// </summary>
    /// <param name="value">The string to wrap.</param>
    public StringSegment(string value)
    {
        _value = value;
        _startIndex = 0;
        _length = value.Length;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringSegment"/> struct.
    /// </summary>
    /// <param name="value">The <see langword="string"/> to wrap.</param>
    /// <param name="startIndex">The starting position in the <see langword="string"/>.</param>
    /// <param name="length">The length of the segment.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown if <paramref name="startIndex"/> or <paramref name="length"/> are negative, or if
    ///  <paramref name="startIndex"/> + <paramref name="length"/> exceeds the length of <paramref name="value"/>.
    /// </exception>
    public StringSegment(string value, int startIndex, int length)
    {
        value ??= string.Empty;

        ArgumentOutOfRange.ThrowIfLessThan(startIndex, 0, nameof(startIndex));
        ArgumentOutOfRange.ThrowIfLessThan(length, 0, nameof(length));

        if (startIndex > value.Length - length && (startIndex != 0 || length != 0))
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex), "Start and length exceed the bounds of the string");
        }

        _value = value;
        _startIndex = startIndex;
        _length = length;
    }

    /// <summary>
    ///  Private constructor to avoid unnecessary validation.
    /// </summary>
    private StringSegment(int startIndex, int length, string value)
    {
        Debug.Assert(startIndex >= 0 && length >= 0);
        Debug.Assert(startIndex <= value.Length - length || startIndex == 0 && length == 0);

        _value = value;
        _startIndex = startIndex;
        _length = length;
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="StringSegment"/> struct.
    /// </summary>
    /// <param name="value">The <see langword="string"/> to wrap.</param>
    /// <param name="startIndex">The starting position in the <see langword="string"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown if <paramref name="startIndex"/> is negative, or exceeds the length of <paramref name="value"/>.
    /// </exception>
    public StringSegment(string value, int startIndex)
        : this(value, startIndex, value.Length - startIndex)
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
    public char this[int index] => _value![_startIndex + index];

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
            return new StringSegment(_startIndex + offset, length, _value);
        }
    }

    /// <summary>
    ///  Slices the segment to create a new segment starting at a specified index.
    /// </summary>
    /// <param name="startIndex">The start index.</param>
    /// <returns>A new <see cref="StringSegment"/> that starts at the specified index.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="startIndex"/> is negative or greater than <see cref="Length"/>.
    /// </exception>
    public StringSegment Slice(int startIndex) => (uint)startIndex > (uint)_length
        ? throw new ArgumentOutOfRangeException(nameof(startIndex))
        : new StringSegment(_startIndex + startIndex, _length - startIndex, _value);

    /// <summary>
    ///  Slices the segment to create a new segment starting at a specified index with the specified length.
    /// </summary>
    /// <param name="startIndex">The start index.</param>
    /// <param name="length">The length of the new segment.</param>
    /// <returns>A new <see cref="StringSegment"/> that starts at the specified index and has the specified length.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="startIndex"/> or <paramref name="length"/> are negative, or if
    ///  <paramref name="startIndex"/> + <paramref name="length"/> exceeds <see cref="Length"/>.
    /// </exception>
    public StringSegment Slice(int startIndex, int length) =>
        (uint)startIndex > (uint)_length || (uint)length > (uint)(_length - startIndex)
            ? throw new ArgumentOutOfRangeException(nameof(startIndex))
            : new StringSegment(_startIndex + startIndex, length, _value);

    /// <summary>
    ///  Splits on the next separator, or returns the entire segment if no separator is found.
    /// </summary>
    /// <returns>
    ///  <see langword="false"/> if the current segment is empty, otherwise <see langword="true"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySplit(char delimiter, out StringSegment left, out StringSegment right)
    {
        if (_length == 0)
        {
            left = default;
            right = default;
            return false;
        }

        int index = IndexOf(delimiter);
        if (index < 0)
        {
            left = this;
            right = default;
            return true;
        }

        left = this[..index];
        right = this[(index + 1)..];
        return true;
    }

    /// <summary>
    ///  Splits on the next separator, or returns the entire segment if no separator is found.
    /// </summary>
    /// <returns>
    ///  <see langword="false"/> if the current segment is empty, otherwise <see langword="true"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TrySplitAny(char value0, char value1, out StringSegment left, out StringSegment right)
    {
        // Optimizing for DirectorySeparatorChar == AltDirectorySeparatorChar
        if (value0 == value1)
        {
            return TrySplit(value0, out left, out right);
        }

        if (_length == 0)
        {
            left = default;
            right = default;
            return false;
        }

        int index = IndexOfAny(value0, value1);
        if (index < 0)
        {
            left = this;
            right = default;
            return true;
        }

        left = this[..index];
        right = this[(index + 1)..];
        return true;
    }

    /// <summary>
    ///  Returns <see langword="true"/> if the segment contains the specified <see langword="char"/>,
    ///  otherwise <see langword="false"/>.
    /// </summary>
    public bool Contains(char value) => IndexOf(value) >= 0;

    /// <summary>
    ///  Returns the index of the given <see langword="char"/> or -1 if not found.
    /// </summary>
    public int IndexOf(char value)
    {
        int index = _value.IndexOf(value, _startIndex, _length);
        return index < 0 ? -1 : index - _startIndex;
    }

    /// <summary>
    ///  Returns the index of the given <see langword="char"/>s or -1 if not found.
    /// </summary>
    public int IndexOfAny(char value0, char value1) => value0 == value1
        // Optimizing for DirectorySeparatorChar == AltDirectorySeparatorChar
        ? IndexOf(value0)
        : AsSpan().IndexOfAny(value0, value1);

    /// <summary>
    ///  Returns the index of the given <see langword="char"/>s or -1 if not found.
    /// </summary>
    public int IndexOfAny(ReadOnlySpan<char> values) => values.Length switch
    {
        0 => -1,
        1 => IndexOf(values[0]),
        2 => IndexOfAny(values[0], values[1]),
        _ => AsSpan().IndexOfAny(values),
    };

    /// <summary>
    ///  Returns the last index of the given <see langword="char"/> or -1 if not found.
    /// </summary>
    public int LastIndexOf(char value)
    {
        if (_length == 0)
        {
            return -1;
        }

        int index = _value.LastIndexOf(value, _startIndex + _length - 1, _length);
        return index < 0 ? -1 : index - _startIndex;
    }

    /// <summary>
    ///  Returns the last index of the given <see langword="char"/>s or -1 if not found.
    /// </summary>
    public int LastIndexOfAny(char value0, char value1) => value0 == value1
        // Optimizing for DirectorySeparatorChar == AltDirectorySeparatorChar
        ? LastIndexOf(value0)
        : AsSpan().LastIndexOfAny(value0, value1);

    /// <summary>
    ///  Returns the last index of the given <see langword="char"/>s or -1 if not found.
    /// </summary>
    public int LastIndexOfAny(ReadOnlySpan<char> values) => values.Length switch
    {
        0 => -1,
        1 => LastIndexOf(values[0]),
        2 => LastIndexOfAny(values[0], values[1]),
        _ => AsSpan().LastIndexOfAny(values),
    };

    /// <summary>
    ///  Returns <see langword="true"/> if the segment starts with the specified <see langword="string"/>,
    /// </summary>
    public bool StartsWith(string value, StringComparison comparison = StringComparison.Ordinal) =>
        value is not null && _length >= value.Length && string.Compare(_value, _startIndex, value, 0, value.Length, comparison) == 0;

    /// <summary>
    ///  Returns <see langword="true"/> if the segment starts with the specified <see cref="StringSegment"/>,
    /// </summary>
    public bool StartsWith(StringSegment value, StringComparison comparison = StringComparison.Ordinal) =>
        value._length <= _length && string.Compare(_value, _startIndex, value._value, value._startIndex, value._length, comparison) == 0;

    /// <summary>
    ///  Returns <see langword="true"/> if the segment starts with the specified <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public bool StartsWith(ReadOnlySpan<char> value, StringComparison comparison = StringComparison.Ordinal) =>
        value.Length <= _length && AsSpan().StartsWith(value, comparison);

    /// <summary>
    ///  Returns <see langword="true"/> if the segment ends with the specified <see langword="string"/>,
    /// </summary>
    public bool EndsWith(string value, StringComparison comparison = StringComparison.Ordinal) =>
        value is not null && _length >= value.Length && string.Compare(_value, _startIndex + _length - value.Length, value, 0, value.Length, comparison) == 0;

    /// <summary>
    ///  Returns <see langword="true"/> if the segment ends with the specified <see cref="StringSegment"/>,
    /// </summary>
    public bool EndsWith(StringSegment value, StringComparison comparison = StringComparison.Ordinal) =>
        value._length <= _length && string.Compare(_value, _startIndex + _length - value._length, value._value, value._startIndex, value._length, comparison) == 0;

    /// <summary>
    ///  Returns <see langword="true"/> if the segment ends with the specified <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public bool EndsWith(ReadOnlySpan<char> value, StringComparison comparison = StringComparison.Ordinal) =>
        value.Length <= _length && AsSpan(_length - value.Length).StartsWith(value, comparison);

    /// <summary>
    ///  Replace all occurrences of a character in the segment with another character.
    /// </summary>
    /// <returns>
    ///  The new <see cref="StringSegment"/> with the specified character replaced.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   This API will never allocate a new backing string unless the segment contains the character to be replaced.
    ///  </para>
    /// </remarks>
    public unsafe StringSegment Replace(char oldValue, char newValue)
    {
        if (_length == 0 || IndexOf(oldValue) < 0)
        {
            return this;
        }

        string newString = Strings.FastAllocateString(_length);
        fixed (char* pNewString = newString)
        {
            Span<char> rawString = new(pNewString, _length);
            AsSpan().CopyTo(rawString);
            rawString.Replace(oldValue, newValue);
        }

        return newString;
    }

    /// <summary>
    ///  Trims the segment by removing leading and trailing whitespace characters.
    /// </summary>
    /// <returns>The trimmed <see cref="StringSegment"/>.</returns>
    /// <remarks>
    ///  <para>
    ///   This API will never allocate a new backing string.
    ///  </para>
    ///  <para>
    ///   Whitespace characters are defined by <see cref="char.IsWhiteSpace(char)"/>,
    ///   which includes spaces, tabs, and other whitespace characters.
    ///  </para>
    /// </remarks>
    public unsafe StringSegment Trim()
    {
        if (_length == 0)
        {
            return this;
        }

        int start = _startIndex;
        int end = _startIndex + _length - 1;

        while (start <= end && char.IsWhiteSpace(_value[start]))
        {
            start++;
        }

        while (end >= start && char.IsWhiteSpace(_value[end]))
        {
            end--;
        }

        return new StringSegment(start, end - start + 1, _value);
    }

    /// <summary>
    ///  Trims the segment by removing the specified character.
    /// </summary>
    /// <returns>The trimmed <see cref="StringSegment"/>.</returns>
    /// <remarks>
    ///  <para>
    ///   This API will never allocate a new backing string.
    ///  </para>
    /// </remarks>
    public StringSegment Trim(char trimChar)
    {
        if (_length == 0)
        {
            return this;
        }

        int start = _startIndex;
        int end = _startIndex + _length - 1;

        while (start <= end && _value[start] == trimChar)
        {
            start++;
        }

        while (end >= start && _value[end] == trimChar)
        {
            end--;
        }

        return new StringSegment(start, end - start + 1, _value);
    }

    /// <summary>
    ///  Trims the segment by removing the specified characters.
    /// </summary>
    /// <inheritdoc cref="Trim(char)"/>
    public StringSegment Trim(char trimChar0, char trimChar1)
    {
        if (_length == 0)
        {
            return this;
        }

        int start = _startIndex;
        int end = _startIndex + _length - 1;

        while (start <= end && (_value[start] == trimChar0 || _value[start] == trimChar1))
        {
            start++;
        }

        while (end >= start && (_value[end] == trimChar0 || _value[end] == trimChar1))
        {
            end--;
        }

        return new StringSegment(start, end - start + 1, _value);
    }

    /// <summary>
    ///  Trims the segment by removing leading whitespace characters.
    /// </summary>
    /// <inheritdoc cref="Trim()"/>
    public StringSegment TrimStart()
    {
        if (_length == 0)
        {
            return this;
        }

        int start = _startIndex;

        while (start < _startIndex + _length && char.IsWhiteSpace(_value[start]))
        {
            start++;
        }

        return new StringSegment(start, _length - (start - _startIndex), _value);
    }

    /// <summary>
    ///  Trims the segment by removing leading specified character.
    /// </summary>
    /// <inheritdoc cref="Trim(char)"/>
    public StringSegment TrimStart(char trimChar)
    {
        if (_length == 0)
        {
            return this;
        }

        int start = _startIndex;

        while (start < _startIndex + _length && _value[start] == trimChar)
        {
            start++;
        }

        return new StringSegment(start, _length - (start - _startIndex), _value);
    }

    /// <summary>
    ///  Trims the segment by removing leading specified characters.
    /// </summary>
    /// <inheritdoc cref="Trim(char)"/>
    public StringSegment TrimStart(char trimChar0, char trimChar1)
    {
        if (_length == 0)
        {
            return this;
        }

        int start = _startIndex;
        while (start < _startIndex + _length && (_value[start] == trimChar0 || _value[start] == trimChar1))
        {
            start++;
        }

        return new StringSegment(start, _length - (start - _startIndex), _value);
    }

    /// <summary>
    ///  Trims the segment by removing trailing whitespace characters.
    /// </summary>
    /// <inheritdoc cref="Trim()"/>
    public unsafe StringSegment TrimEnd()
    {
        if (_length == 0)
        {
            return this;
        }

        int end = _startIndex + _length - 1;

        while (end >= _startIndex && char.IsWhiteSpace(_value[end]))
        {
            end--;
        }

        return new StringSegment(_startIndex, end - _startIndex + 1, _value);
    }

    /// <summary>
    ///  Trims the segment by removing trailing specified character.
    /// </summary>
    /// <inheritdoc cref="Trim(char)"/>
    public StringSegment TrimEnd(char trimChar)
    {
        if (_length == 0)
        {
            return this;
        }

        int end = _startIndex + _length - 1;
        while (end >= _startIndex && _value[end] == trimChar)
        {
            end--;
        }

        return new StringSegment(_startIndex, end - _startIndex + 1, _value);
    }

    /// <summary>
    ///  Trims the segment by removing trailing specified characters.
    /// </summary>
    /// <inheritdoc cref="Trim(char)"/>
    public StringSegment TrimEnd(char trimChar0, char trimChar1)
    {
        if (_length == 0)
        {
            return this;
        }

        int end = _startIndex + _length - 1;
        while (end >= _startIndex && (_value[end] == trimChar0 || _value[end] == trimChar1))
        {
            end--;
        }

        return new StringSegment(_startIndex, end - _startIndex + 1, _value);
    }

    /// <summary>
    ///  Creates a span over this segment.
    /// </summary>
    /// <returns>A span that represents the segment.</returns>
    public ReadOnlySpan<char> AsSpan() => _value.AsSpan(_startIndex, _length);

    /// <summary>
    ///  Creates a span over a portion of this segment.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <returns>A span that represents the specified portion of the segment.</returns>
    public ReadOnlySpan<char> AsSpan(int start) => (uint)start > (uint)_length
        ? throw new ArgumentOutOfRangeException(nameof(start))
        : _value.AsSpan(_startIndex + start, _length - start);

    /// <summary>
    ///  Creates a span over a portion of this segment.
    /// </summary>
    /// <param name="start">The start index.</param>
    /// <param name="length">The length of the span.</param>
    /// <returns>A span that represents the specified portion of the segment.</returns>
    public ReadOnlySpan<char> AsSpan(int start, int length) =>
        (uint)start > (uint)_length || (uint)length > (uint)(_length - start)
            ? throw new ArgumentOutOfRangeException(nameof(start))
            : _value.AsSpan(_startIndex + start, length);

    /// <summary>
    ///  Returns a <see langword="string"/> that represents the current segment.
    /// </summary>
    /// <returns>A <see langword="string"/> that represents the current segment.</returns>
    public override string ToString() => _length == 0
        ? string.Empty
        : _startIndex switch
        {
            0 when _length == _value.Length => _value,
            _ => _value.Substring(_startIndex, _length)
        };

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> to a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/>.
    /// </summary>
    /// <param name="segment">The segment to convert.</param>
    public static implicit operator ReadOnlySpan<char>(StringSegment segment) => segment.AsSpan();

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> to a string.
    /// </summary>
    /// <param name="segment">The segment to convert.</param>
    public static implicit operator string(StringSegment segment) => segment.ToString();

    /// <summary>
    ///  Implicitly converts a <see cref="string"/> to a <see cref="StringSegment"/>.
    /// </summary>
    /// <param name="value">The string to convert.</param>
    public static implicit operator StringSegment(string value) => new StringSegment(value);

    /// <summary>
    ///  Implicitly converts a <see cref="StringSegment"/> to a <see cref="ReadOnlyMemory{T}"/> of <see cref="char"/>.
    /// </summary>
    public static implicit operator ReadOnlyMemory<char>(StringSegment segment) =>
        segment._value.AsMemory(segment._startIndex, segment._length);

    /// <summary>
    ///  Gets a value indicating whether two segments are equal.
    /// </summary>
    /// <param name="other">The segment to compare with.</param>
    /// <returns><see langword="true"/> if the segments are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(StringSegment other) => Equals(other, StringComparison.Ordinal);

    /// <summary>
    ///  Gets a value indicating whether the segment equals the specified span.
    /// </summary>
    /// <param name="other">The span to compare with.</param>
    /// <param name="ignoreCase">Whether or not to ignore case.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(StringSegment other, bool ignoreCase) =>
        Equals(other, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    ///  Gets a value indicating whether the segment equals the specified span.
    /// </summary>
    /// <param name="other">The span to compare with.</param>
    /// <param name="comparison">The comparison to use.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public bool Equals(StringSegment other, StringComparison comparison) =>
        _length == other.Length
            && string.Compare(_value, _startIndex, other._value, other._startIndex, _length, comparison) == 0;

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
    public bool Equals(string? other) => other is not null && _length == other.Length && AsSpan().SequenceEqual(other.AsSpan());

    /// <summary>
    ///  Gets a value indicating whether the segment equals the specified object.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> if the segment equals the specified object; otherwise, <see langword="false"/>.</returns>
    public override bool Equals(object? obj) => (obj is StringSegment other && Equals(other))
        || (obj is string otherString && Equals(otherString));

    /// <summary>
    ///  Gets the hash code for the segment.
    /// </summary>
    /// <returns>A hash code for the segment.</returns>
    public override int GetHashCode() => Strings.GetHashCode(AsSpan());

    /// <summary>
    ///  The C# compiler pattern needed to pin the segment in memory.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public unsafe ref char GetPinnableReference()
    {
        if (IsEmpty)
        {
            // Return a null ref so the compiler emits a null pointer.
            return ref Unsafe.AsRef<char>(null);
        }

        return ref MemoryMarshal.GetReference(AsSpan());
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
