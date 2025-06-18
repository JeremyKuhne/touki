// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Wrapper for a <see cref="string"/> or <see cref="ReadOnlySpan{Char}"/>.
/// </summary>
/// <remarks>
///  Use where you would want to take a <see cref="ReadOnlySpan{Char}"/> but
///  also want to call <see cref="ToString()"/> without allocating a string
///  copy when you had a string to begin with.
/// </remarks>
public readonly ref struct StringSpan
{
    private readonly ReadOnlySpan<char> _span;
    private readonly string? _string;

    /// <summary>
    ///  Constructs a <see cref="StringSpan"/> from a <see cref="ReadOnlySpan{Char}"/>.
    /// </summary>
    public StringSpan(ReadOnlySpan<char> span)
    {
        _span = span;
        _string = null;
    }

    /// <summary>
    ///  Constructs a <see cref="StringSpan"/> from a <see langword="string"/>.
    /// </summary>
    /// <param name="value"></param>
    public StringSpan(string? value)
    {
        _string = value;
        _span = _string is null ? default : _string.AsSpan();
    }

    /// <summary>
    ///  Returns <see langword="true"/> if there is no content in the span or string.
    /// </summary>
    public bool IsEmpty => _span.IsEmpty;

    /// <summary>
    ///  Implicitly converts a <see langword="string"/> to a <see cref="StringSpan"/>.
    /// </summary>
    public static implicit operator StringSpan(string? value) => new(value);

    /// <summary>
    ///  Implicitly converts a <see cref="ReadOnlySpan{Char}"/> to a <see cref="StringSpan"/>.
    /// </summary>
    public static implicit operator StringSpan(ReadOnlySpan<char> span) => new(span);

    /// <summary>
    ///  Implicitly converts a <see cref="ReadOnlySpan{Char}"/> to a <see cref="StringSpan"/>.
    /// </summary>
    public static implicit operator ReadOnlySpan<char>(StringSpan span) => span._span;

    /// <summary>
    ///  Implicitly converts a <see cref="Span{Char}"/> to a <see cref="StringSpan"/>.
    /// </summary>
    public static implicit operator StringSpan(Span<char> span) => new(span);

    /// <inheritdoc/>
    public override string ToString() => _string ?? (_span.IsEmpty ? string.Empty : _span.ToString());

    /// <summary>
    ///  Returns the string representation of the <see cref="StringSpan"/> if it has content,
    ///  otherwise returns <see langword="null"/>.
    /// </summary>
    public string? ToStringOrNull() => _string is not null ? _string : _span.IsEmpty ? null : _span.ToString();
}
