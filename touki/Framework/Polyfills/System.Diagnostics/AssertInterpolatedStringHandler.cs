// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Diagnostics;

/// <summary>
///  Provides an interpolated string handler for <see cref="Debug.Assert(bool)"/> that only formats when the assert fails.
/// </summary>
// Only invoked from Debug.Assert overloads, which are [Conditional("DEBUG")] and unreachable in Release coverage runs.
// Owns a ValueStringBuilder (itself [NonCopyable]); copying the handler would copy the builder, so it propagates the
// constraint. The compiler only ever uses an interpolated string handler by ref, so this is free here.
[InterpolatedStringHandler]
[ExcludeFromCodeCoverage]
[Touki.NonCopyable]
public ref struct AssertInterpolatedStringHandler
{
    private ValueStringBuilder _builder;
    private readonly bool _shouldAppend;

    /// <summary>Creates an instance of the handler.</summary>
    /// <param name="literalLength">The length of literal content in the interpolated string.</param>
    /// <param name="formattedCount">The number of interpolation holes in the interpolated string.</param>
    /// <param name="condition">The condition Boolean passed to the consuming method.</param>
    /// <param name="shouldAppend">Indicates whether formatting should proceed.</param>
    public AssertInterpolatedStringHandler(int literalLength, int formattedCount, bool condition, out bool shouldAppend)
    {
        if (condition)
        {
            _builder = default;
            _shouldAppend = shouldAppend = false;
        }
        else
        {
            _builder = new ValueStringBuilder(literalLength, formattedCount);
            _shouldAppend = shouldAppend = true;
        }
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendLiteral(string?)"/>
    public void AppendLiteral(string? value)
    {
        if (_shouldAppend)
        {
            _builder.AppendLiteral(value);
        }
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, StringSpan)"/>
    public void AppendFormatted<T>(T value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T,int)"/>
    public void AppendFormatted<T>(T value, int alignment)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value, alignment);
        }
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, int, StringSpan)"/>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted<T>(value, alignment, format);
        }
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(ReadOnlySpan{char})"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(ReadOnlySpan{char},int,string?)"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value, alignment, format);
        }
    }

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(string?)"/>
    public void AppendFormatted(string? value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <summary>Gets the built string and clears the handler.</summary>
    public string ToStringAndClear()
    {
        if (!_shouldAppend)
        {
            return string.Empty;
        }

        string result = _builder.ToString();
        _builder.Dispose();
        return result;
    }
}
