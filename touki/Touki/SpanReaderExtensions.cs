// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Extensions for <see cref="SpanReader{T}"/>.
/// </summary>
public static class SpanReaderExtensions
{
    /// <summary>
    ///  Tries to read an integer from the current position of the <see cref="SpanReader{T}"/>.
    /// </summary>
    /// <param name="reader">The <see cref="SpanReader{T}"/> to read from.</param>
    /// <param name="value">When successful, contains the read integer.</param>
    /// <returns><see langword="true"/> if an integer was successfully read; otherwise, <see langword="false"/>.</returns>
    public static bool TryReadPositiveInteger(this ref SpanReader<char> reader, out uint value)
    {
        // Read digits until we hit a non-digit character or the end of the span.
        value = default;
        bool foundDigit = false;

        while (reader.TryPeek(out char next) && char.IsDigit(next))
        {
            value = value * 10u + (uint)(next - '0');
            reader.Advance(1);
            foundDigit = true;
        }

        return foundDigit;
    }
}
