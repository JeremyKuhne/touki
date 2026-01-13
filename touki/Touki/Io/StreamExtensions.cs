// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;
using System.Threading.Tasks;

namespace Touki.Io;

/// <summary>
///  Extension methods for <see cref="Stream"/>.
/// </summary>
public static partial class TextWriterExtensions
{
    /// <param name="stream">The target stream.</param>
    extension(Stream stream)
    {
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
        public Task WriteAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken = default)
            => buffer.Array is byte[] array
                ? stream.WriteAsync(array, buffer.Offset, buffer.Count, cancellationToken)
                : Task.CompletedTask;

        /// <summary>
        ///  Writes an interpolated string directly to a <see cref="Stream"/>.
        /// </summary>
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
