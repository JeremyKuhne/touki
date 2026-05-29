// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Fuzz;

/// <summary>
///  Coverage-guided fuzz target for <see cref="RunLengthEncoder"/>.
/// </summary>
/// <remarks>
///  <para>
///   The first input byte selects a mode and the remaining bytes are the payload. Even modes treat the
///   payload as raw data and exercise the encode path plus a full encode/decode round-trip; odd modes treat
///   the payload as already-encoded data and exercise the decode path against arbitrary (possibly malformed)
///   input. The asserted properties are:
///  </para>
///  <para>
///   - A buffer sized by <see cref="RunLengthEncoder.GetEncodedLength"/> is always large enough for
///     <see cref="RunLengthEncoder.TryEncode"/>, and the reported <c>written</c> count equals it.
///  </para>
///  <para>
///   - Encoding then decoding reproduces the original data exactly, and
///     <see cref="RunLengthEncoder.GetDecodedLength"/> of freshly encoded data equals the original length.
///  </para>
///  <para>
///   - <see cref="RunLengthEncoder.TryDecode"/> on arbitrary bytes never throws and never writes past a
///     destination sized by <see cref="RunLengthEncoder.GetDecodedLength"/>; it returns <see langword="false"/>
///     for odd-length (malformed) input and for a destination that is one byte too small.
///  </para>
/// </remarks>
internal static class RunLengthTarget
{
    // Cap allocations so a fuzzer-grown input cannot request an unbounded buffer.
    private const int MaxDecodedLength = 1 << 20;

    internal static void Run(ReadOnlySpan<byte> data)
    {
        SpanReader<byte> reader = new(data);
        if (!reader.TryRead(out byte mode))
        {
            return;
        }

        ReadOnlySpan<byte> payload = reader.Unread;

        if ((mode & 1) == 0)
        {
            EncodeRoundtrip(payload);
        }
        else
        {
            DecodeArbitrary(payload);
        }
    }

    private static void EncodeRoundtrip(ReadOnlySpan<byte> data)
    {
        int encodedLength = RunLengthEncoder.GetEncodedLength(data);
        if (encodedLength < 0)
        {
            throw new FuzzInvariantException($"GetEncodedLength returned negative value {encodedLength}.");
        }

        byte[] encoded = new byte[encodedLength];
        if (!RunLengthEncoder.TryEncode(data, encoded, out int written))
        {
            throw new FuzzInvariantException(
                $"TryEncode failed with a GetEncodedLength-sized buffer ({encodedLength} bytes).");
        }

        if (written != encodedLength)
        {
            throw new FuzzInvariantException(
                $"TryEncode wrote {written} bytes but GetEncodedLength reported {encodedLength}.");
        }

        int decodedLength = RunLengthEncoder.GetDecodedLength(encoded);
        if (decodedLength != data.Length)
        {
            throw new FuzzInvariantException(
                $"GetDecodedLength {decodedLength} != original length {data.Length}.");
        }

        byte[] decoded = new byte[data.Length];
        if (!RunLengthEncoder.TryDecode(encoded, decoded))
        {
            throw new FuzzInvariantException("TryDecode failed on data produced by TryEncode.");
        }

        if (!decoded.AsSpan().SequenceEqual(data))
        {
            throw new FuzzInvariantException("Encode/decode round-trip did not reproduce the original data.");
        }
    }

    private static void DecodeArbitrary(ReadOnlySpan<byte> encoded)
    {
        int decodedLength = RunLengthEncoder.GetDecodedLength(encoded);
        if (decodedLength < 0)
        {
            throw new FuzzInvariantException($"GetDecodedLength returned negative value {decodedLength}.");
        }

        if (decodedLength > MaxDecodedLength)
        {
            return;
        }

        // A destination sized by GetDecodedLength must always be large enough for the writes TryDecode
        // performs, so it can only fail for malformed (odd-length) input - never throw or overflow.
        byte[] decoded = new byte[decodedLength];
        bool success = RunLengthEncoder.TryDecode(encoded, decoded);

        bool malformed = (encoded.Length & 1) != 0;
        if (malformed && success)
        {
            throw new FuzzInvariantException("TryDecode reported success for odd-length (malformed) input.");
        }

        if (!malformed && !success)
        {
            throw new FuzzInvariantException(
                "TryDecode failed for even-length input with a GetDecodedLength-sized destination.");
        }

        // A destination that is one byte too small must fail gracefully, never throw.
        if (decodedLength > 0)
        {
            byte[] tooSmall = new byte[decodedLength - 1];
            if (RunLengthEncoder.TryDecode(encoded, tooSmall))
            {
                throw new FuzzInvariantException(
                    $"TryDecode reported success with an undersized destination ({decodedLength - 1} bytes).");
            }
        }
    }
}
