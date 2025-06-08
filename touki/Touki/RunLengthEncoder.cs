// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Simple run length encoder (RLE) that works on spans.
/// </summary>
/// <remarks>
///  <para>
///   Format used is a byte for the count, followed by a byte for the value.
///  </para>
/// </remarks>
public static class RunLengthEncoder
{
    /// <summary>
    ///  Get the encoded length, in bytes, of the given data.
    /// </summary>
    /// <param name="data">The data to calculate the encoded length for.</param>
    /// <returns>The number of bytes required to encode the data using run-length encoding.</returns>
    public static int GetEncodedLength(ReadOnlySpan<byte> data)
    {
        SpanReader<byte> reader = new(data);

        int length = 0;
        while (reader.TryRead(out byte value))
        {
            int count = reader.AdvancePast(value) + 1;
            while (count > 0)
            {
                // 1 byte for the count, 1 byte for the value
                length += 2;
                count -= 0xFF;
            }
        }

        return length;
    }

    /// <summary>
    ///  Get the decoded length, in bytes, of the given encoded data.
    /// </summary>
    /// <param name="encoded">The run-length encoded data to calculate the decoded length for.</param>
    /// <returns>The number of bytes required to decode the encoded data.</returns>
    /// <remarks>
    ///  <para>
    ///   This method assumes the encoded data follows the expected format of count-value pairs.
    ///   If the encoded data has an odd length, the final byte is treated as a count with no corresponding value.
    ///  </para>
    /// </remarks>
    public static int GetDecodedLength(ReadOnlySpan<byte> encoded)
    {
        int length = 0;
        for (int i = 0; i < encoded.Length; i += 2)
        {
            length += encoded[i];
        }

        return length;
    }

    /// <summary>
    ///  Encode the given data into the given <paramref name="encoded"/> span.
    /// </summary>
    /// <param name="data">The data to encode using run-length encoding.</param>
    /// <param name="encoded">The span to write the encoded data to.</param>
    /// <param name="written">
    ///  When this method returns, contains the number of bytes written to the <paramref name="encoded"/> span.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if the encoding was successful;
    ///  <see langword="false"/> if the <paramref name="encoded"/> span was not large enough to hold the encoded data.
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   Runs longer than 255 bytes are split into multiple encoding pairs.
    ///   If encoding fails due to insufficient buffer space, partial data may have been written.
    ///  </para>
    /// </remarks>
    public static bool TryEncode(ReadOnlySpan<byte> data, Span<byte> encoded, out int written)
    {
        SpanReader<byte> reader = new(data);
        SpanWriter<byte> writer = new(encoded);

        while (reader.TryRead(out byte value))
        {
            int count = reader.AdvancePast(value) + 1;
            while (count > 0)
            {
                if (!writer.TryWrite((byte)Math.Min(count, 0xFF)) || !writer.TryWrite(value))
                {
                    written = writer.Position;
                    return false;
                }

                count -= 0xFF;
            }
        }

        written = writer.Position;
        return true;
    }

    /// <summary>
    ///  Decode the given run-length encoded data into the given <paramref name="data"/> span.
    /// </summary>
    /// <param name="encoded">The run-length encoded data to decode.</param>
    /// <param name="data">The span to write the decoded data to.</param>
    /// <returns>
    ///  <see langword="true"/> if the decoding was successful;
    ///  <see langword="false"/> if the <paramref name="data"/> span was not large enough to hold the decoded data
    ///  or if the <paramref name="encoded"/> data is malformed (odd length).
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   The encoded data must consist of count-value pairs. If the encoded data has an odd length,
    ///   the decoding will fail when it attempts to read a value for the final count.
    ///  </para>
    /// </remarks>
    public static bool TryDecode(ReadOnlySpan<byte> encoded, Span<byte> data)
    {
        SpanReader<byte> reader = new(encoded);
        SpanWriter<byte> writer = new(data);

        while (reader.TryRead(out byte count))
        {
            if (!reader.TryRead(out byte value) || !writer.TryWrite(count, value))
            {
                return false;
            }
        }

        return true;
    }
}
