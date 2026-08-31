// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;
using System.Threading.Tasks;

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
        ///  Attempts to read <paramref name="count"/> bytes from the current stream and advances the position
        ///  within the stream.
        /// </summary>
        /// <param name="buffer">The buffer to write the data into.</param>
        /// <param name="offset">The byte offset in <paramref name="buffer"/> at which to begin writing data.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>
        ///  <see langword="true"/> if <paramref name="count"/> bytes were read; otherwise,
        ///  <see langword="false"/> if the end of the stream was reached first.
        /// </returns>
        /// <remarks>
        ///  <para>
        ///   When this method returns <see langword="false"/>, bytes read before the end of the stream remain in the
        ///   requested range of <paramref name="buffer"/>.
        ///  </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">A required reference is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///  <paramref name="offset"/> is negative, or <paramref name="count"/> does not identify a valid range in
        ///  <paramref name="buffer"/>.
        /// </exception>
        /// <exception cref="IOException">The stream reports reading more bytes than requested.</exception>
        public bool TryReadExactly(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            ArgumentOutOfRangeException.ThrowIfNegative(offset);

            return (uint)count > buffer.Length - offset
                ? throw new ArgumentOutOfRangeException(nameof(count))
                : TryReadExactly(stream, buffer.AsSpan(offset, count));
        }

        /// <summary>
        ///  Attempts to fill <paramref name="buffer"/> from the current stream and advances the position within the
        ///  stream.
        /// </summary>
        /// <param name="buffer">The buffer to fill.</param>
        /// <returns>
        ///  <see langword="true"/> if <paramref name="buffer"/> was filled; otherwise,
        ///  <see langword="false"/> if the end of the stream was reached first.
        /// </returns>
        /// <remarks>
        ///  <para>
        ///   When this method returns <see langword="false"/>, bytes read before the end of the stream remain at the
        ///   beginning of <paramref name="buffer"/>.
        ///  </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        /// <exception cref="IOException">The stream reports reading more bytes than requested.</exception>
        public bool TryReadExactly(Span<byte> buffer)
        {
            ArgumentNullException.ThrowIfNull(stream);

            while (!buffer.IsEmpty)
            {
                int read = stream.Read(buffer);
                if ((uint)read > (uint)buffer.Length)
                {
                    throw new IOException("Stream was too long.");
                }

                if (read == 0)
                {
                    return false;
                }

                buffer = buffer[read..];
            }

            return true;
        }

        /// <summary>
        ///  Reads a sequence of bytes from the current stream and advances the position
        ///  within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">The buffer to read into.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public int Read(ArraySegment<byte> buffer) => buffer.Array is byte[] array
            ? stream.Read(array, buffer.Offset, buffer.Count)
            : 0;

        /// <summary>
        ///  Asynchronously reads a sequence of bytes from the current stream and
        ///  advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">The buffer to read into.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous read operation.</returns>
        public Task<int> ReadAsync(
            ArraySegment<byte> buffer,
            CancellationToken cancellationToken = default) => buffer.Array is byte[] array
                ? stream.ReadAsync(array, buffer.Offset, buffer.Count, cancellationToken)
                : Task.FromResult(0);

        /// <summary>
        ///  Writes a sequence of bytes to the current stream and advances the current
        ///  position within the stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">The buffer to write from.</param>
        public void Write(ArraySegment<byte> buffer)
        {
            if (buffer.Array is byte[] array)
            {
                stream.Write(array, buffer.Offset, buffer.Count);
            }
        }

        /// <summary>
        ///  Asynchronously writes a sequence of bytes to the current stream and
        ///  advances the current position within the stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">The buffer to write from.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        public Task WriteAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default) =>
            buffer.Array is byte[] array
                ? stream.WriteAsync(array, buffer.Offset, buffer.Count, cancellationToken)
                : Task.CompletedTask;

        /// <summary>
        ///  Writes an interpolated string directly to a <see cref="Stream"/>.
        /// </summary>
        /// <param name="builder">The interpolated string builder to write and clear.</param>
        public void WriteFormatted(ref ValueStringBuilder builder)
        {
            if (builder.Length > 0)
            {
                builder.CopyTo(stream);
                builder.Clear();
            }
        }

#if NET
        /// <summary>
        ///  Writes a string directly to a <see cref="Stream"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Optimization overload that allows string literals to be used without creating a builder.
        ///  </para>
        /// </remarks>
        /// <param name="value">The string to write.</param>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void WriteFormatted(string value)
        {
            // While it would be nice to have this for .NET Framework, the only method we have on
            // stream takes a byte[] buffer. We can't reinterpret the string as a byte[].
            stream.Write(MemoryMarshal.AsBytes(value.AsSpan()));
        }
#endif
    }
}
