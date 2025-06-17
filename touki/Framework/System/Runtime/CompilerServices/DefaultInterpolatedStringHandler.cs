// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Some code is from the .NET codebase, with minor modifications for clarity. See comments inline.
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Touki;

namespace System.Runtime.CompilerServices;

/// <summary>
///  Simple default implementation of an interpolated string handler
///  that uses a <see cref="ValueStringBuilder"/> to build the string.
/// </summary>
[InterpolatedStringHandler]
public ref struct DefaultInterpolatedStringHandler
{
    private ValueStringBuilder _builder;

    internal const int StackallocIntBufferSizeLimit = 128;
    internal const int StackallocCharBufferSizeLimit = 256;

    /// <inheritdoc cref="DefaultInterpolatedStringHandler(int, int, IFormatProvider?, Span{char})"/>/>
    public DefaultInterpolatedStringHandler(int literalLength, int formattedCount)
        : this(literalLength, formattedCount, provider: null) { }

    /// <inheritdoc cref="DefaultInterpolatedStringHandler(int, int, IFormatProvider?, Span{char})"/>/>
    public DefaultInterpolatedStringHandler(int literalLength, int formattedCount, IFormatProvider? provider) =>
        _builder = new ValueStringBuilder(literalLength, formattedCount, provider);

    /// <summary>Creates a handler used to translate an interpolated string into a <see cref="string"/>.</summary>
    /// <param name="literalLength">
    ///  The number of constant characters outside of interpolation expressions the interpolated string.
    /// </param>
    /// <param name="formattedCount">The number of interpolation expressions the interpolated string.</param>
    /// <param name="provider">An object that supplies culture-specific formatting information.</param>
    /// <param name="initialBuffer">
    ///  A buffer temporarily transferred to the handler for use as part of its formatting.  Contents may be overwritten.
    /// </param>
    public DefaultInterpolatedStringHandler(
        int literalLength,
        int formattedCount,
        IFormatProvider? provider,
        Span<char> initialBuffer) => _builder = new ValueStringBuilder(initialBuffer, provider);

    /// <inheritdoc cref="ValueStringBuilder.Append(string)"/>
    public void AppendLiteral(string s) => _builder.Append(s);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T)"/>
    public void AppendFormatted<T>(T value) => _builder.AppendFormatted(value);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, string?)"/>
    public void AppendFormatted<T>(T value, string? format) => _builder.AppendFormatted(value, format);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, int)"/>
    public void AppendFormatted<T>(T value, int alignment) => _builder.AppendFormatted(value, alignment);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, int, string?)"/>
    public void AppendFormatted<T>(T value, int alignment, string? format) =>
        _builder.AppendFormatted(value, alignment, format);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(ReadOnlySpan{char})"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value) => _builder.AppendFormatted(value);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
    public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null) =>
        _builder.AppendFormatted(value, alignment, format);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(string?)"/>
    public void AppendFormatted(string? value) => _builder.AppendFormatted(value);

    /// <summary>Gets the built <see cref="string"/>.</summary>
    /// <returns>The built string.</returns>
    public override readonly string ToString() => _builder.ToString();

    /// <inheritdoc cref="ValueStringBuilder.ToStringAndClear()"/>
    public string ToStringAndClear() => _builder.ToStringAndClear();
}
