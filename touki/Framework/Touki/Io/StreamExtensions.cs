// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;
using System.IO;

namespace Touki.Io;

/// <summary>
///  Extension methods for <see cref="Stream"/>.
/// </summary>
public static partial class StreamExtensions
{
    extension(TextWriter writer)
    {
        /// <summary>
        ///  Allows writing a <see cref="ReadOnlySpan{Char}"/> to a <see cref="TextWriter"/>.
        /// </summary>
        public void Write(ReadOnlySpan<char> value)
        {
            if (value.Length > 0)
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(value.Length);
                value.CopyTo(buffer);
                writer.Write(buffer, 0, value.Length);
                ArrayPool<char>.Shared.Return(buffer);
            }
        }

        /// <summary>
        ///  Allows writing a <see cref="ReadOnlySpan{Char}"/> to a <see cref="TextWriter"/>.
        /// </summary>
        public void WriteLine(ReadOnlySpan<char> value)
        {
            if (value.Length > 0)
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(value.Length);
                value.CopyTo(buffer);
                writer.Write(buffer, 0, value.Length);
                ArrayPool<char>.Shared.Return(buffer);
            }

            writer.WriteLine();
        }
    }
}
