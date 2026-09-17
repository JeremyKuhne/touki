// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Fuzz;

/// <summary>
///  Coverage-guided fuzz target for <see cref="SpanWriter{T}"/>.
/// </summary>
/// <remarks>
///  <para>
///   A fixed-capacity backing buffer is sized from the first input byte, then the remaining bytes drive a
///   sequence of writer operations. Arguments to <see cref="SpanWriter{T}.Advance(int)"/>,
///   <see cref="SpanWriter{T}.Rewind(int)"/>, and the position setter are always computed in-range, so any
///   thrown exception or position/length inconsistency is a real defect surfaced to the fuzzer as a crash.
///  </para>
/// </remarks>
internal static class SpanWriterTarget
{
    private const int MaxCapacity = 256;

    internal static void Run(ReadOnlySpan<byte> data)
    {
        SpanReader<byte> ops = new(data);

        // First byte selects the backing buffer capacity in [0, MaxCapacity].
        int capacity = ops.TryRead(out byte capacityByte) ? capacityByte % (MaxCapacity + 1) : 0;

        Span<byte> backing = stackalloc byte[MaxCapacity];
        backing = backing[..capacity];
        SpanWriter<byte> writer = new(backing);

        CheckInvariants(ref writer, capacity);

        while (ops.TryRead(out byte op))
        {
            switch (op & 0x07)
            {
                case 0:
                    writer.TryWrite(op);
                    break;
                case 1:
                    {
                        ops.TryRead(out byte value);
                        // count is in [0, 255], so never negative.
                        writer.TryWrite(op, value);
                        break;
                    }
                case 2:
                    {
                        // Write a slice of the remaining op bytes.
                        ops.TryRead(op % (MaxCapacity + 1), out ReadOnlySpan<byte> values);
                        writer.TryWrite(values);
                        break;
                    }
                case 3:
                    {
                        // Advance only by an in-range amount so any throw is a real defect.
                        int remaining = writer.Length - writer.Position;
                        writer.Advance(op % (remaining + 1));
                        break;
                    }
                case 4:
                    // Rewind only by an in-range amount.
                    writer.Rewind(op % (writer.Position + 1));
                    break;
                case 5:
                    // Set Position to an in-range value.
                    writer.Position = op % (writer.Length + 1);
                    break;
                case 6:
                    writer.Reset();
                    break;
                case 7:
                    _ = writer.End;
                    break;
            }

            CheckInvariants(ref writer, capacity);
        }

        CheckInvariants(ref writer, capacity);
    }

    private static void CheckInvariants(scoped ref SpanWriter<byte> writer, int capacity)
    {
        if (writer.Span.Length != capacity)
        {
            throw new FuzzInvariantException(
                $"Span length changed: expected {capacity}, got {writer.Span.Length}.");
        }

        if (writer.Position < 0 || writer.Position > writer.Length)
        {
            throw new FuzzInvariantException(
                $"Position {writer.Position} is outside [0, {writer.Length}].");
        }

        if (writer.End != (writer.Position == writer.Length))
        {
            throw new FuzzInvariantException(
                $"End ({writer.End}) inconsistent with Position {writer.Position} and Length {writer.Length}.");
        }
    }
}
