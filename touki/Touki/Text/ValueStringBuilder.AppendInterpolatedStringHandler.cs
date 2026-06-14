// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Text;

public ref partial struct ValueStringBuilder
{
    /// <summary>
    ///  Provides an interpolated string handler for appending formatted text to a
    ///  <see cref="ValueStringBuilder"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [InterpolatedStringHandler]
    [NonCopyable]
    public ref struct InterpolatedStringHandler
    {
        private ValueStringBuilder _builder;

        /// <summary>
        ///  Initializes a new instance of the <see cref="InterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The length of literal content in the interpolated string.</param>
        /// <param name="formattedCount">The number of formatted holes in the interpolated string.</param>
        /// <param name="builder">The builder receiving the interpolated string.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public InterpolatedStringHandler(int literalLength, int formattedCount, ValueStringBuilder builder)
            => _builder = new(literalLength, formattedCount, builder._formatProvider);

        /// <inheritdoc cref="ValueStringBuilder.AppendLiteral(string?)"/>
        public void AppendLiteral(string value) => _builder.AppendLiteral(value);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, StringSpan)"/>
        public void AppendFormatted<T>(T value) => _builder.AppendFormatted(value);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(Value, StringSpan)"/>
        public void AppendFormatted(Value value) => _builder.AppendFormatted(value);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, string?)"/>
        public void AppendFormatted<T>(T value, string? format) => _builder.AppendFormatted(value, format);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(Value, string?)"/>
        public void AppendFormatted(Value value, string? format) => _builder.AppendFormatted(value, format);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, int)"/>
        public void AppendFormatted<T>(T value, int alignment) => _builder.AppendFormatted(value, alignment);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted{T}(T, int, StringSpan)"/>
        public void AppendFormatted<T>(T value, int alignment, string? format) =>
            _builder.AppendFormatted<T>(value, alignment, (StringSpan)format);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(ReadOnlySpan{char})"/>
        public void AppendFormatted(scoped ReadOnlySpan<char> value) => _builder.AppendFormatted(value);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
        public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null) =>
            _builder.AppendFormatted(value, alignment, format);

        /// <inheritdoc cref="ValueStringBuilder.AppendFormatted(string?)"/>
        public void AppendFormatted(string? value) => _builder.AppendFormatted(value);

        /// <inheritdoc cref="ValueStringBuilder.AsSpan()"/>
        public readonly ReadOnlySpan<char> AsSpan() => _builder.AsSpan();

        /// <inheritdoc cref="ValueStringBuilder.Dispose()"/>
        public void Dispose() => _builder.Dispose();
    }
}
