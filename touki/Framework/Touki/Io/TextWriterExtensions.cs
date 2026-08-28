// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public static partial class TextWriterExtensions
{
    /// <param name="writer">The writer to write to.</param>
    extension(TextWriter writer)
    {
        /// <summary>
        ///  Allows writing a <see cref="ReadOnlySpan{Char}"/> to a <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="value">The characters to write.</param>
        public void Write(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
            {
                return;
            }

            if (writer is StringWriter stringWriter)
            {
                stringWriter.GetStringBuilder().AppendSpan(value);
                return;
            }

            // Fall back to renting a buffer
            char[] buffer = ArrayPool<char>.Shared.Rent(value.Length);
            value.CopyTo(buffer);
            writer.Write(buffer, 0, value.Length);
            ArrayPool<char>.Shared.Return(buffer);
        }

        /// <summary>
        ///  Allows writing a <see cref="ReadOnlySpan{Char}"/> to a <see cref="TextWriter"/>.
        /// </summary>
        /// <param name="value">The characters to write before the line terminator.</param>
        public void WriteLine(ReadOnlySpan<char> value)
        {
            if (value.Length == 0)
            {
                writer.WriteLine();
                return;
            }

            if (writer is StringWriter stringWriter)
            {
                stringWriter.GetStringBuilder().AppendSpan(value);
                writer.WriteLine();
                return;
            }

            char[] buffer = ArrayPool<char>.Shared.Rent(value.Length);
            value.CopyTo(buffer);
            writer.Write(buffer, 0, value.Length);
            ArrayPool<char>.Shared.Return(buffer);

            writer.WriteLine();
        }
    }
}
