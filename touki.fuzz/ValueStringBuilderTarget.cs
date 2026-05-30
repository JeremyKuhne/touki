// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

using StringBuilder = System.Text.StringBuilder;

namespace Touki.Fuzz;

/// <summary>
///  Coverage-guided fuzz target for <see cref="ValueStringBuilder"/>.
/// </summary>
/// <remarks>
///  <para>
///   The fuzz input is an opcode stream that drives a sequence of append / insert / truncate / clear
///   operations on a <see cref="ValueStringBuilder"/> seeded with a stack buffer. A
///   <see cref="StringBuilder"/> mirrors every operation as an oracle; after each step the builder's
///   <see cref="ValueStringBuilder.Length"/>, capacity bound, and full contents must match the
///   oracle.
///  </para>
///  <para>
///   Counts and the post-operation total length are capped so a fuzzer-grown input cannot request an
///   unbounded allocation. The <see cref="ValueStringBuilder.Length"/> setter is only ever
///   shrunk: growing it would expose buffer contents the builder does not zero, which the oracle cannot
///   predict. Any divergence or unexpected exception is reported to the fuzzer as a crash.
///  </para>
/// </remarks>
internal static class ValueStringBuilderTarget
{
    // Cap total length and per-operation repeats so a grown input cannot allocate without bound.
    private const int MaxLength = 1 << 16;
    private const int MaxRepeat = 64;

    internal static void Run(ReadOnlySpan<byte> data)
    {
        SpanReader<byte> ops = new(data);

        Span<char> initialBuffer = stackalloc char[64];
        Span<char> chunk = stackalloc char[MaxRepeat];
        ValueStringBuilder builder = new(initialBuffer);
        StringBuilder oracle = new();

        try
        {
            CheckInvariants(ref builder, oracle);

            while (ops.TryRead(out byte op))
            {
                switch (op & 0x07)
                {
                    case 0:
                        {
                            char c = (char)NextByte(ref ops);
                            if (oracle.Length < MaxLength)
                            {
                                builder.Append(c);
                                oracle.Append(c);
                            }

                            break;
                        }
                    case 1:
                        {
                            char c = (char)NextByte(ref ops);
                            int repeat = NextByte(ref ops) % MaxRepeat;
                            if (oracle.Length + repeat <= MaxLength)
                            {
                                builder.Append(c, repeat);
                                oracle.Append(c, repeat);
                            }

                            break;
                        }
                    case 2:
                        {
                            // Append a slice of the remaining op bytes interpreted as chars.
                            int length = NextByte(ref ops) % MaxRepeat;
                            int produced = 0;
                            while (produced < length && ops.TryRead(out byte b))
                            {
                                chunk[produced++] = (char)b;
                            }

                            ReadOnlySpan<char> slice = chunk[..produced];
                            if (oracle.Length + produced <= MaxLength)
                            {
                                builder.Append(slice);
                                foreach (char c in slice)
                                {
                                    oracle.Append(c);
                                }
                            }

                            break;
                        }
                    case 3:
                        {
                            string s = ReadString(ref ops);
                            if (oracle.Length + s.Length <= MaxLength)
                            {
                                builder.Append(s);
                                oracle.Append(s);
                            }

                            break;
                        }
                    case 4:
                        {
                            // Insert(index, char, count) at an in-range index.
                            int index = oracle.Length == 0 ? 0 : NextByte(ref ops) % (oracle.Length + 1);
                            char c = (char)NextByte(ref ops);
                            int repeat = NextByte(ref ops) % MaxRepeat;
                            if (oracle.Length + repeat <= MaxLength)
                            {
                                builder.Insert(index, c, repeat);
                                oracle.Insert(index, new string(c, repeat));
                            }

                            break;
                        }
                    case 5:
                        {
                            // Insert(index, string) at an in-range index.
                            int index = oracle.Length == 0 ? 0 : NextByte(ref ops) % (oracle.Length + 1);
                            string s = ReadString(ref ops);
                            if (oracle.Length + s.Length <= MaxLength)
                            {
                                builder.Insert(index, s);
                                oracle.Insert(index, s);
                            }

                            break;
                        }
                    case 6:
                        {
                            // Truncate via the Length setter (shrink only - growing exposes stale buffer).
                            int newLength = oracle.Length == 0 ? 0 : NextByte(ref ops) % (oracle.Length + 1);
                            builder.Length = newLength;
                            oracle.Length = newLength;
                            break;
                        }
                    default:
                        builder.Clear();
                        oracle.Clear();
                        break;
                }

                CheckInvariants(ref builder, oracle);
            }

            if (!string.Equals(builder.ToString(), oracle.ToString(), StringComparison.Ordinal))
            {
                throw new FuzzInvariantException("Final ToString() diverged from the StringBuilder oracle.");
            }
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static byte NextByte(ref SpanReader<byte> ops) => ops.TryRead(out byte value) ? value : (byte)0;

    private static string ReadString(ref SpanReader<byte> ops)
    {
        int length = NextByte(ref ops) % MaxRepeat;
        if (length == 0)
        {
            return string.Empty;
        }

        char[] chars = new char[length];
        int produced = 0;
        while (produced < length && ops.TryRead(out byte b))
        {
            chars[produced++] = (char)b;
        }

        return new string(chars, 0, produced);
    }

    private static void CheckInvariants(scoped ref ValueStringBuilder builder, StringBuilder oracle)
    {
        if (builder.Length != oracle.Length)
        {
            throw new FuzzInvariantException($"Length {builder.Length} != oracle length {oracle.Length}.");
        }

        if (builder.Length > builder.Capacity)
        {
            throw new FuzzInvariantException($"Length {builder.Length} exceeds Capacity {builder.Capacity}.");
        }

        ReadOnlySpan<char> span = builder.AsSpan(terminate: false);
        if (span.Length != builder.Length)
        {
            throw new FuzzInvariantException($"AsSpan length {span.Length} != Length {builder.Length}.");
        }

        if (!span.SequenceEqual(oracle.ToString().AsSpan()))
        {
            throw new FuzzInvariantException("Builder contents diverged from the StringBuilder oracle.");
        }
    }
}
