// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki.Fuzz;

// IDE0057 would collapse the Slice(...) calls below into the Range indexer, but this target
// deliberately exercises both Slice and the Range indexer as separate operations.
#pragma warning disable IDE0057 // Slice can be simplified

/// <summary>
///  Coverage-guided fuzz target for <see cref="StringSegment"/>.
/// </summary>
/// <remarks>
///  <para>
///   The fuzz input is interpreted both as the backing <see langword="string"/> (one <see langword="char"/>
///   per byte, so whitespace and delimiters occur naturally) and as an opcode stream. An <c>ops</c> reader
///   is only ever advanced forward to pull opcodes and arguments, while a separate <c>subject</c> segment is
///   the immutable value each operation derives a new segment from. Because <see cref="StringSegment"/> is a
///   value type, reassigning <c>subject</c> never moves the opcode cursor, so the loop always terminates.
///  </para>
///  <para>
///   Each derived operation is cross-checked against an oracle (the equivalent <see langword="string"/> or
///   <see cref="ReadOnlySpan{T}"/> operation), and structural invariants (length in range, <c>AsSpan</c> /
///   <c>ToString</c> agreement, <c>IsEmpty</c> consistency) are re-checked after every operation. Any
///   divergence or unexpected exception is reported to the fuzzer as a crash.
///  </para>
/// </remarks>
internal static class StringSegmentTarget
{
    // Cap the backing length so a fuzzer-grown input cannot allocate an unbounded string.
    private const int MaxChars = 1 << 12;

    internal static void Run(ReadOnlySpan<byte> data)
    {
        SpanReader<byte> ops = new(data);

        int count = Math.Min(data.Length, MaxChars);
        char[] chars = new char[count];
        for (int i = 0; i < count; i++)
        {
            chars[i] = (char)data[i];
        }

        string backing = new(chars);
        StringSegment subject = new(backing);

        CheckInvariants(subject, backing);

        while (ops.TryRead(out byte op))
        {
            switch (op & 0x0F)
            {
                case 0:
                    {
                        // Slice(start) with an in-range start must match AsSpan(start).
                        int start = subject.Length == 0 ? 0 : NextByte(ref ops) % (subject.Length + 1);
                        StringSegment sliced = subject.Slice(start);
                        if (!sliced.AsSpan().SequenceEqual(subject.AsSpan(start)))
                        {
                            throw new FuzzInvariantException("Slice(start) diverged from AsSpan(start).");
                        }

                        subject = sliced;
                        break;
                    }
                case 1:
                    {
                        // Slice(start, length) in range must match AsSpan(start, length).
                        int start = subject.Length == 0 ? 0 : NextByte(ref ops) % (subject.Length + 1);
                        int remaining = subject.Length - start;
                        int length = remaining == 0 ? 0 : NextByte(ref ops) % (remaining + 1);
                        StringSegment sliced = subject.Slice(start, length);
                        if (sliced.Length != length || !sliced.AsSpan().SequenceEqual(subject.AsSpan(start, length)))
                        {
                            throw new FuzzInvariantException("Slice(start, length) diverged from AsSpan(start, length).");
                        }

                        subject = sliced;
                        break;
                    }
                case 2:
                    {
                        // The Range indexer must match the equivalent AsSpan slice.
                        int start = subject.Length == 0 ? 0 : NextByte(ref ops) % (subject.Length + 1);
                        int remaining = subject.Length - start;
                        int length = remaining == 0 ? 0 : NextByte(ref ops) % (remaining + 1);
                        StringSegment ranged = subject[start..(start + length)];
                        if (!ranged.AsSpan().SequenceEqual(subject.AsSpan(start, length)))
                        {
                            throw new FuzzInvariantException("Range indexer diverged from AsSpan slice.");
                        }

                        subject = ranged;
                        break;
                    }
                case 3:
                    {
                        // Trim() must match string.Trim() and stay a sub-range.
                        StringSegment trimmed = subject.Trim();
                        if (!trimmed.AsSpan().SequenceEqual(subject.ToString().Trim().AsSpan()))
                        {
                            throw new FuzzInvariantException("Trim() diverged from string.Trim().");
                        }

                        subject = trimmed;
                        break;
                    }
                case 4:
                    {
                        char c = (char)NextByte(ref ops);
                        StringSegment trimmed = subject.Trim(c);
                        if (!trimmed.AsSpan().SequenceEqual(subject.ToString().Trim(c).AsSpan()))
                        {
                            throw new FuzzInvariantException("Trim(char) diverged from string.Trim(char).");
                        }

                        subject = trimmed;
                        break;
                    }
                case 5:
                    {
                        char c = (char)NextByte(ref ops);
                        StringSegment trimmed = subject.TrimStart(c);
                        if (!trimmed.AsSpan().SequenceEqual(subject.ToString().TrimStart(c).AsSpan()))
                        {
                            throw new FuzzInvariantException("TrimStart(char) diverged from string.TrimStart(char).");
                        }

                        subject = trimmed;
                        break;
                    }
                case 6:
                    {
                        char c = (char)NextByte(ref ops);
                        StringSegment trimmed = subject.TrimEnd(c);
                        if (!trimmed.AsSpan().SequenceEqual(subject.ToString().TrimEnd(c).AsSpan()))
                        {
                            throw new FuzzInvariantException("TrimEnd(char) diverged from string.TrimEnd(char).");
                        }

                        subject = trimmed;
                        break;
                    }
                case 7:
                    {
                        // IndexOf(char) must match the span result.
                        char c = (char)NextByte(ref ops);
                        int actual = subject.IndexOf(c);
                        int expected = subject.AsSpan().IndexOf(c);
                        if (actual != expected)
                        {
                            throw new FuzzInvariantException($"IndexOf {actual} != span IndexOf {expected}.");
                        }

                        break;
                    }
                case 8:
                    {
                        // LastIndexOf(char) must match the span result.
                        char c = (char)NextByte(ref ops);
                        int actual = subject.LastIndexOf(c);
                        int expected = subject.AsSpan().LastIndexOf(c);
                        if (actual != expected)
                        {
                            throw new FuzzInvariantException($"LastIndexOf {actual} != span LastIndexOf {expected}.");
                        }

                        break;
                    }
                case 9:
                    {
                        // TrySplit must partition the segment around the first delimiter.
                        char delimiter = (char)NextByte(ref ops);
                        if (subject.TrySplit(delimiter, out StringSegment left, out StringSegment right))
                        {
                            int index = subject.IndexOf(delimiter);
                            if (index < 0)
                            {
                                if (!left.AsSpan().SequenceEqual(subject.AsSpan()) || right.Length != 0)
                                {
                                    throw new FuzzInvariantException("TrySplit with no delimiter did not return the whole segment.");
                                }
                            }
                            else if (!left.AsSpan().SequenceEqual(subject.AsSpan(0, index))
                                || !right.AsSpan().SequenceEqual(subject.AsSpan(index + 1)))
                            {
                                throw new FuzzInvariantException("TrySplit produced an inconsistent partition.");
                            }

                            // Continue splitting the remainder so later iterations make progress.
                            subject = right;
                        }
                        else if (subject.Length != 0)
                        {
                            throw new FuzzInvariantException("TrySplit returned false for a non-empty segment.");
                        }

                        break;
                    }
                case 10:
                    {
                        // Replace must match string.Replace and keep the length stable.
                        char oldValue = (char)NextByte(ref ops);
                        char newValue = (char)NextByte(ref ops);
                        StringSegment replaced = subject.Replace(oldValue, newValue);
                        string expected = subject.ToString().Replace(oldValue, newValue);
                        if (replaced.Length != subject.Length || !replaced.AsSpan().SequenceEqual(expected.AsSpan()))
                        {
                            throw new FuzzInvariantException("Replace diverged from string.Replace.");
                        }

                        subject = replaced;
                        break;
                    }
                case 11:
                    {
                        // A fresh segment over the current ToString() must be equal and share a hash code.
                        StringSegment other = new(subject.ToString());
                        if (!subject.Equals(other))
                        {
                            throw new FuzzInvariantException("Segment not equal to a fresh segment over its own ToString().");
                        }

                        if (subject.GetHashCode() != other.GetHashCode())
                        {
                            throw new FuzzInvariantException("Equal segments produced different hash codes.");
                        }

                        break;
                    }
                case 12:
                    {
                        // The indexer must match the span at a sampled position.
                        if (subject.Length != 0)
                        {
                            int index = NextByte(ref ops) % subject.Length;
                            if (subject[index] != subject.AsSpan()[index])
                            {
                                throw new FuzzInvariantException("Indexer diverged from AsSpan.");
                            }
                        }

                        break;
                    }
                default:
                    // Reset to the full backing segment so later opcodes can explore from the top again.
                    subject = new(backing);
                    break;
            }

            CheckInvariants(subject, backing);
        }
    }

    private static byte NextByte(ref SpanReader<byte> ops) => ops.TryRead(out byte value) ? value : (byte)0;

    private static void CheckInvariants(StringSegment segment, string backing)
    {
        if (segment.Length < 0 || segment.Length > backing.Length)
        {
            throw new FuzzInvariantException($"Length {segment.Length} is outside [0, {backing.Length}].");
        }

        ReadOnlySpan<char> span = segment.AsSpan();
        if (span.Length != segment.Length)
        {
            throw new FuzzInvariantException($"AsSpan length {span.Length} != Length {segment.Length}.");
        }

        string asString = segment.ToString();
        if (asString.Length != segment.Length || !asString.AsSpan().SequenceEqual(span))
        {
            throw new FuzzInvariantException("ToString() diverged from AsSpan().");
        }

        if (segment.IsEmpty != (segment.Length == 0))
        {
            throw new FuzzInvariantException($"IsEmpty ({segment.IsEmpty}) inconsistent with Length ({segment.Length}).");
        }
    }
}
