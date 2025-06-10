// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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

    /// <summary>
    ///  Initializes a new instance of the <see cref="DefaultInterpolatedStringHandler"/> struct for use with interpolated strings.
    /// </summary>
    /// <param name="literalLength">The length of literal content in the interpolated string.</param>
    /// <param name="formattedCount">The number of formatted holes in the interpolated string.</param>
    public DefaultInterpolatedStringHandler(int literalLength, int formattedCount)
    {
        _builder = new ValueStringBuilder(literalLength, formattedCount);
    }

    /// <inheritdoc cref="ValueStringBuilder.Append(string)"/>
    public void AppendLiteral(string s) => _builder.Append(s);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{TFormattable}(TFormattable)"/>
    public void AppendFormatted<TFormattable>(TFormattable value) where TFormattable : ISpanFormattable =>
        _builder.AppendFormatted(value);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(object?)"/>
    public void AppendFormatted(object value) => _builder.AppendFormatted(value);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(int)"/>
    public void AppendFormatted(int value) => _builder.AppendFormatted(value);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(long)"/>
    public void AppendFormatted(long value) => _builder.AppendFormatted(value);

    /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(ReadOnlySpan{char})"/>
    public void AppendFormatted(ReadOnlySpan<char> value) => _builder.AppendFormatted(value);

    /// <inheritdoc cref="ValueStringBuilder.ToStringAndClear()"/>
    public string ToStringAndClear() => _builder.ToStringAndClear();
}
