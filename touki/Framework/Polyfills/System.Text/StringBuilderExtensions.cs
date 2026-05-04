// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Text;

/// <summary>
///  Extension methods for <see cref="StringBuilder"/>.
/// </summary>
public static partial class StringBuilderExtensions
{
    extension(StringBuilder builder)
    {
        /// <summary>
        ///  GetChunks returns ChunkEnumerator that follows the IEnumerable pattern and
        ///  thus can be used in a C# 'foreach' statements to retrieve the data in the StringBuilder
        ///  as chunks (ReadOnlyMemory) of characters.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   On .NET Core the returned type is nested in StringBuilder, which we cannot do. As such,
        ///   the full type name has to be different and you must assign this to <see langword="var"/> if you
        ///   need to put it in a local to enable cross compilation.
        ///  </para>
        /// </remarks>
        public ChunkEnumerator GetChunks() => new ChunkEnumerator(builder);

        /// <summary>
        ///  Copies the characters from a specified segment of this instance to a destination
        ///  <see cref="Span{T}"/> of <see cref="char"/>.
        /// </summary>
        /// <param name="sourceIndex">
        ///  The starting position in this instance where characters will be copied from. The index is zero-based.
        /// </param>
        /// <param name="destination">The writable span where characters will be copied.</param>
        /// <param name="count">The number of characters to be copied.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        ///  <paramref name="sourceIndex"/> or <paramref name="count"/> is less than zero,
        ///  or <paramref name="sourceIndex"/> is greater than the length of this instance.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///  <paramref name="sourceIndex"/> + <paramref name="count"/> is greater than the length of this instance,
        ///  or <paramref name="count"/> is greater than the length of <paramref name="destination"/>.
        /// </exception>
        public void CopyTo(int sourceIndex, Span<char> destination, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            }

            if ((uint)sourceIndex > (uint)builder.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), "Index was out of range.");
            }

            if (sourceIndex > builder.Length - count)
            {
                throw new ArgumentException("Index and count must refer to a location within the string builder.", nameof(sourceIndex));
            }

            if (count > destination.Length)
            {
                throw new ArgumentException("Destination is too short.", nameof(destination));
            }

            if (count == 0)
            {
                return;
            }

            int chunkOffset = 0;
            int destOffset = 0;
            int remaining = count;
            int sourceOffset = sourceIndex;

            foreach (ReadOnlyMemory<char> chunk in builder.GetChunks())
            {
                if (remaining == 0)
                {
                    break;
                }

                int chunkLength = chunk.Length;
                int chunkEnd = chunkOffset + chunkLength;

                if (sourceOffset < chunkEnd)
                {
                    int startInChunk = sourceOffset - chunkOffset;
                    int available = chunkLength - startInChunk;
                    int toCopy = Math.Min(available, remaining);

                    chunk.Span.Slice(startInChunk, toCopy).CopyTo(destination[destOffset..]);

                    destOffset += toCopy;
                    sourceOffset += toCopy;
                    remaining -= toCopy;
                }

                chunkOffset = chunkEnd;
            }
        }
    }
}
