// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.IO;

/// <summary>
///  Polyfills span-based <see cref="Stream"/> APIs.
/// </summary>
public static class StreamExtensions
{
    private const int MaxByteArrayLength = 0X7FFFFFC7;

    /// <param name="stream">The target stream.</param>
    extension(Stream stream)
    {
        /// <summary>
        ///  Reads a sequence of bytes from the current stream and advances the position within the stream by the
        ///  number of bytes read.
        /// </summary>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <returns>The total number of bytes read into <paramref name="buffer"/>.</returns>
        /// <remarks>
        ///  <para>
        ///   Empty buffers dispatch through the virtual <see cref="Stream.Read(byte[], int, int)"/> overload with a
        ///   shared empty array. For non-empty buffers, exact <see cref="MemoryStream"/> instances with publicly visible
        ///   buffers are read directly; other streams use a pooled intermediate array and dispatch through that virtual
        ///   overload.
        ///  </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        /// <exception cref="IOException">The stream reports reading more bytes than requested.</exception>
        public int Read(Span<byte> buffer)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (buffer.IsEmpty)
            {
                int read = stream.Read([], 0, 0);
                if (read != 0)
                {
                    throw new IOException("Stream was too long.");
                }

                return 0;
            }

            if (stream.GetType() == typeof(MemoryStream)
                && ((MemoryStream)stream).CanRead
                && ((MemoryStream)stream).TryGetBuffer(out ArraySegment<byte> segment))
            {
                MemoryStream memoryStream = (MemoryStream)stream;
                int position = (int)memoryStream.Position;
                int available = (int)Math.Min(memoryStream.Length - position, buffer.Length);
                if (available <= 0)
                {
                    return 0;
                }

                segment.AsSpan(position, available).CopyTo(buffer);
                memoryStream.Position = position + available;
                return available;
            }

            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            try
            {
                int read = stream.Read(sharedBuffer, 0, buffer.Length);
                if ((uint)read > (uint)buffer.Length)
                {
                    throw new IOException("Stream was too long.");
                }

                new ReadOnlySpan<byte>(sharedBuffer, 0, read).CopyTo(buffer);
                return read;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sharedBuffer);
            }
        }

        /// <summary>
        ///  Writes a sequence of bytes to the current stream and advances the current position within this stream by
        ///  the number of bytes written.
        /// </summary>
        /// <param name="buffer">The bytes to write.</param>
        /// <remarks>
        ///  <para>
        ///   Empty buffers dispatch through the virtual <see cref="Stream.Write(byte[], int, int)"/> overload with a
        ///   shared empty array. For non-empty buffers, exact <see cref="MemoryStream"/> instances with publicly visible
        ///   buffers are written directly; other streams use a pooled intermediate array and dispatch through that
        ///   virtual overload.
        ///  </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        public void Write(ReadOnlySpan<byte> buffer)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (buffer.IsEmpty)
            {
                stream.Write([], 0, 0);
                return;
            }

            if (stream.GetType() == typeof(MemoryStream)
                && ((MemoryStream)stream).CanWrite
                && ((MemoryStream)stream).TryGetBuffer(out ArraySegment<byte> segment))
            {
                MemoryStream memoryStream = (MemoryStream)stream;
                long position = memoryStream.Position;
                long end = position + buffer.Length;
                if (end <= MaxByteArrayLength - segment.Offset)
                {
                    int endPosition = (int)end;
                    if (endPosition > memoryStream.Length)
                    {
                        memoryStream.SetLength(endPosition);
                        memoryStream.TryGetBuffer(out segment);
                    }

                    buffer.CopyTo(segment.AsSpan((int)position));
                    memoryStream.Position = endPosition;
                    return;
                }
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
