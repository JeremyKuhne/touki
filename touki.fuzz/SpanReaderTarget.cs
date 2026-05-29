// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Fuzz;

/// <summary>
///  Coverage-guided fuzz target for <see cref="SpanReader{T}"/>.
/// </summary>
/// <remarks>
///  <para>
///   The fuzz input drives two independent <see cref="SpanReader{T}"/> instances over the same data: an
///   <c>ops</c> reader that is only ever advanced forward to pull opcodes (so the driving loop always makes
///   progress and terminates), and a <c>subject</c> reader that the selected operation is performed on. Each
///   opcode byte selects one subject operation and supplies its arguments. After every operation the
///   structural invariants are re-checked, so any out-of-bounds slice, unexpected exception, or
///   position/length inconsistency is reported to the fuzzer as a crash.
///  </para>
///  <para>
///   The two readers must stay separate: operations such as <see cref="SpanReader{T}.Reset"/>,
///   <see cref="SpanReader{T}.Rewind(int)"/>, and the position setter move the subject backward, which would
///   make the loop re-read the same opcodes forever if a single reader drove both roles.
///  </para>
/// </remarks>
internal static class SpanReaderTarget
{
    internal static void Run(ReadOnlySpan<byte> data)
    {
        int originalLength = data.Length;
        SpanReader<byte> ops = new(data);
        SpanReader<byte> subject = new(data);

        CheckInvariants(ref subject, originalLength);

        while (ops.TryRead(out byte op))
        {
            switch (op & 0x0F)
            {
                case 0:
                    subject.TryRead(out byte _);
                    break;
                case 1:
                    subject.TryPeek(out byte _);
                    break;
                case 2:
                    // count is in [0, 255], so never negative.
                    subject.TryRead(op, out ReadOnlySpan<byte> _);
                    break;
                case 3:
                    subject.TryRead(out int _);
                    break;
                case 4:
                    subject.TryRead(out uint _);
                    break;
                case 5:
                    subject.TryReadTo(op, out ReadOnlySpan<byte> _);
                    break;
                case 6:
                    subject.TryReadTo(op, advancePastDelimiter: false, out ReadOnlySpan<byte> _);
                    break;
                case 7:
                    subject.TryReadToAny([op, (byte)~op], advancePastDelimiter: true, out ReadOnlySpan<byte> _);
                    break;
                case 8:
                    subject.TrySplit(op, out ReadOnlySpan<byte> _);
                    break;
                case 9:
                    subject.TrySplitAny([op, (byte)(op + 1)], out ReadOnlySpan<byte> _);
                    break;
                case 10:
                    subject.AdvancePast(op);
                    break;
                case 11:
                    {
                        // Use a caller-scoped slice; TryAdvancePast's parameter is not scoped.
                        subject.TryAdvancePast(data.Length >= 1 ? data[..1] : data);
                        break;
                    }
                case 12:
                    {
                        // Advance only by an in-range amount so any throw is a real defect.
                        int remaining = subject.Length - subject.Position;
                        subject.Advance(op % (remaining + 1));
                        break;
                    }
                case 13:
                    {
                        // Rewind only by an in-range amount.
                        subject.Rewind(op % (subject.Position + 1));
                        break;
                    }
                case 14:
                    // Set Position to an in-range value and confirm it round-trips.
                    subject.Position = op % (subject.Length + 1);
                    break;
                case 15:
                    subject.Reset();
                    break;
            }

            CheckInvariants(ref subject, originalLength);
        }

        CheckInvariants(ref subject, originalLength);
    }

    private static void CheckInvariants(scoped ref SpanReader<byte> reader, int originalLength)
    {
        if (reader.Span.Length != originalLength)
        {
            throw new FuzzInvariantException(
                $"Span length changed: expected {originalLength}, got {reader.Span.Length}.");
        }

        if (reader.Position < 0 || reader.Position > reader.Length)
        {
            throw new FuzzInvariantException(
                $"Position {reader.Position} is outside [0, {reader.Length}].");
        }

        if (reader.Unread.Length != reader.Length - reader.Position)
        {
            throw new FuzzInvariantException(
                $"Unread length {reader.Unread.Length} != Length - Position ({reader.Length - reader.Position}).");
        }
    }
}
