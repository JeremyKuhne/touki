﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Diagnostics;

/// <summary>
///  Provides an interpolated string handler for <see cref="Debug.Assert(bool)"/> that only formats when the assert fails.
/// </summary>
[InterpolatedStringHandler]
public ref struct AssertInterpolatedStringHandler
{
    private Touki.Text.ValueStringBuilder _builder;
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
            _builder = new Touki.Text.ValueStringBuilder(literalLength, formattedCount);
            _shouldAppend = shouldAppend = true;
        }
    }

    /// <inheritdoc cref="Touki.Text.ValueStringBuilder.AppendLiteral(string?)"/>
    public void AppendLiteral(string? value)
    {
        if (_shouldAppend)
        {
            _builder.AppendLiteral(value);
        }
    }

    /// <inheritdoc cref="Touki.Text.ValueStringBuilder.AppendFormatted{T}(T, Touki.StringSpan)"/>
    public void AppendFormatted<T>(T value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <inheritdoc cref="Touki.Text.ValueStringBuilder.AppendFormatted{T}(T,int)"/>
    public void AppendFormatted<T>(T value, int alignment)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value, alignment);
        }
    }

    /// <inheritdoc cref="Touki.Text.ValueStringBuilder.AppendFormatted{T}(T, int, Touki.StringSpan)"/>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted<T>(value, alignment, format);
        }
    }

    /// <inheritdoc cref="Touki.Text.ValueStringBuilder.AppendFormatted(ReadOnlySpan{char})"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <inheritdoc cref="Touki.Text.ValueStringBuilder.AppendFormatted(ReadOnlySpan{char},int,string?)"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value, alignment, format);
        }
    }

    /// <inheritdoc cref="Touki.Text.ValueStringBuilder.AppendFormatted(string?)"/>
    public void AppendFormatted(string? value)
    {
        if (_shouldAppend)
        {
            _builder.AppendFormatted(value);
        }
    }

    /// <summary>Gets the built string and clears the handler.</summary>
    public string ToStringAndClear() => _shouldAppend ? _builder.ToStringAndDispose() : string.Empty;
}
