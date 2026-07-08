// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Extension methods for <see cref="Stream"/>.
/// </summary>
public static partial class StreamExtensions
{
    /// <param name="stream">The target stream.</param>
    extension(Stream stream)
    {
        /// <summary>
        ///  Writes a sequence of bytes to the current stream and advances the current position within this stream by
        ///  the number of bytes written.
        /// </summary>
        public void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return;
            }

            // Fast path for publicly visible MemoryStreams: write straight into the backing array
            // instead of renting a temporary and copying twice. SetLength grows the length (and the
            // capacity, with amortized doubling) so the written region becomes part of the stream; it
            // throws for a non-expandable stream exactly as Write would.
            if (stream is MemoryStream memoryStream && memoryStream.TryGetBuffer(out ArraySegment<byte> segment))
            {
                int position = (int)memoryStream.Position;
                int end = checked(position + buffer.Length);
                if (end > memoryStream.Length)
                {
                    // Growth may reallocate the backing array, so re-acquire the segment afterward.
                    memoryStream.SetLength(end);
                    memoryStream.TryGetBuffer(out segment);
                }

                buffer.CopyTo(segment.AsSpan(position));
                memoryStream.Position = end;
                return;
            }

            byte[] temp = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                buffer.CopyTo(temp);
                stream.Write(temp, 0, buffer.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temp);
            }
        }
    }
}
