﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Threading;
using System.Threading.Tasks;
#if !NET
using System.IO;
#endif
using Touki.Text;

namespace Touki.Io;

/// <summary>
///  Extension methods for <see cref="Stream"/>.
/// </summary>
public static partial class StreamExtensions
{
    /// <summary>
    ///  Reads a sequence of bytes from the current stream and advances the position
    ///  within the stream by the number of bytes read.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to read into.</param>
    /// <returns>The total number of bytes read into the buffer.</returns>
    public static int Read(this Stream stream, ArraySegment<byte> buffer)
        => buffer.Array is byte[] array
            ? stream.Read(array, buffer.Offset, buffer.Count)
            : 0;

    /// <summary>
    ///  Asynchronously reads a sequence of bytes from the current stream and
    ///  advances the position within the stream by the number of bytes read.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="buffer">The buffer to read into.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous read operation.</returns>
    public static Task<int> ReadAsync(
        this Stream stream,
        ArraySegment<byte> buffer,
        CancellationToken cancellationToken = default) => buffer.Array is byte[] array
            ? stream.ReadAsync(array, buffer.Offset, buffer.Count, cancellationToken)
            : Task.FromResult(0);

    /// <summary>
    ///  Writes a sequence of bytes to the current stream and advances the current
    ///  position within the stream by the number of bytes written.
    /// </summary>
    /// <param name="stream">The stream to write to.</param>
    /// <param name="buffer">The buffer to write from.</param>
    public static void Write(this Stream stream, ArraySegment<byte> buffer)
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
    /// <param name="stream">The stream to write to.</param>
    /// <param name="buffer">The buffer to write from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    public static Task WriteAsync(this Stream stream, ArraySegment<byte> buffer, CancellationToken cancellationToken = default)
        => buffer.Array is byte[] array
            ? stream.WriteAsync(array, buffer.Offset, buffer.Count, cancellationToken)
            : Task.CompletedTask;

    /// <summary>
    ///  Writes an interpolated string directly to a stream.
    /// </summary>
    public static void WriteFormatted(this Stream stream, ref ValueStringBuilder builder)
    {
        if (builder.Length > 0)
        {
            builder.CopyTo(stream);
            builder.Clear();
        }
    }

    /// <summary>
    ///  Writes an interpolated string directly to a <see cref="StreamWriter"/>.
    /// </summary>
    public static void WriteFormatted(this StreamWriter writer, ref ValueStringBuilder builder)
    {
        if (builder.Length > 0)
        {
            builder.CopyTo(writer);
            builder.Clear();
        }
    }

    /// <summary>
    ///  Allows writing a <see cref="StringSegment"/> to a <see cref="TextWriter"/>.
    /// </summary>
    public static void Write(this TextWriter writer, StringSegment value) => writer.Write(value.AsSpan());

    /// <summary>
    ///  Allows writing a <see cref="StringSegment"/> to a <see cref="TextWriter"/>.
    /// </summary>
    public static void WriteLine(this TextWriter writer, StringSegment value) => writer.WriteLine(value.AsSpan());
}
