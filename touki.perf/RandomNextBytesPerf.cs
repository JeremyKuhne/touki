// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.InteropServices;

namespace touki.perf;

#pragma warning disable CA5394 // Random is insecure - benchmark only

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class RandomNextBytesPerf
{
    private Random _random = null!;
    private byte[] _buffer = null!;

    [Params(16, 64, 256, 1024)]
    public int N;

    [GlobalSetup]
    public void Setup()
    {
        _random = new Random(12345);
        _buffer = new byte[N];
    }

    /// <summary>
    ///  Current implementation: allocate temp byte[], call Random.NextBytes(byte[]), copy.
    /// </summary>
    [Benchmark(Baseline = true)]
    public byte AllocateAndCopy()
    {
        Span<byte> buffer = _buffer;
        byte[] temp = new byte[buffer.Length];
        _random.NextBytes(temp);
        temp.AsSpan().CopyTo(buffer);
        return buffer[buffer.Length - 1];
    }

    /// <summary>
    ///  Optimized: when the runtime type is exactly <see cref="Random"/>, fill the
    ///  destination span directly using the documented per-byte algorithm.
    /// </summary>
    [Benchmark]
    public byte TypeCheckAndFill()
    {
        Span<byte> buffer = _buffer;
        if (typeof(Random) == _random.GetType())
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(_random.Next() % (byte.MaxValue + 1));
            }

            return buffer[buffer.Length - 1];
        }

        byte[] temp = new byte[buffer.Length];
        _random.NextBytes(temp);
        temp.AsSpan().CopyTo(buffer);
        return buffer[buffer.Length - 1];
    }

    /// <summary>
    ///  Same algorithm as <see cref="TypeCheckAndFill"/>, but with the per-byte loop
    ///  unrolled 4x. Reduces loop overhead and bounds-check counts.
    /// </summary>
    [Benchmark]
    public byte TypeCheckAndFillUnrolled()
    {
        Span<byte> buffer = _buffer;
        if (typeof(Random) == _random.GetType())
        {
            Random random = _random;
            int length = buffer.Length;
            int unrollEnd = length & ~3;
            int i = 0;
            for (; i < unrollEnd; i += 4)
            {
                buffer[i] = (byte)(random.Next() % (byte.MaxValue + 1));
                buffer[i + 1] = (byte)(random.Next() % (byte.MaxValue + 1));
                buffer[i + 2] = (byte)(random.Next() % (byte.MaxValue + 1));
                buffer[i + 3] = (byte)(random.Next() % (byte.MaxValue + 1));
            }

            for (; i < length; i++)
            {
                buffer[i] = (byte)(random.Next() % (byte.MaxValue + 1));
            }

            return buffer[length - 1];
        }

        byte[] temp = new byte[buffer.Length];
        _random.NextBytes(temp);
        temp.AsSpan().CopyTo(buffer);
        return buffer[buffer.Length - 1];
    }

    /// <summary>
    ///  Unsafe variant of <see cref="TypeCheckAndFill"/>: pin the destination span and
    ///  use a pointer-based per-byte loop without unrolling.
    /// </summary>
    [Benchmark]
    public unsafe byte TypeCheckAndFillUnsafeNoUnroll()
    {
        Span<byte> buffer = _buffer;
        if (typeof(Random) == _random.GetType())
        {
            Random random = _random;
            int length = buffer.Length;

            fixed (byte* pStart = &MemoryMarshal.GetReference(buffer))
            {
                byte* p = pStart;
                byte* end = pStart + length;

                while (p < end)
                {
                    *p++ = (byte)(random.Next() % (byte.MaxValue + 1));
                }
            }

            return buffer[length - 1];
        }

        byte[] temp = new byte[buffer.Length];
        _random.NextBytes(temp);
        temp.AsSpan().CopyTo(buffer);
        return buffer[buffer.Length - 1];
    }

    /// <summary>
    ///  Unsafe variant: pin the destination span and use pointer arithmetic to
    ///  avoid bounds checks entirely.
    /// </summary>
    [Benchmark]
    public unsafe byte TypeCheckAndFillUnsafe()
    {
        Span<byte> buffer = _buffer;
        if (typeof(Random) == _random.GetType())
        {
            Random random = _random;
            int length = buffer.Length;

            fixed (byte* pStart = &MemoryMarshal.GetReference(buffer))
            {
                byte* p = pStart;
                byte* end = pStart + length;
                byte* unrollEnd = pStart + (length & ~3);

                while (p < unrollEnd)
                {
                    p[0] = (byte)(random.Next() % (byte.MaxValue + 1));
                    p[1] = (byte)(random.Next() % (byte.MaxValue + 1));
                    p[2] = (byte)(random.Next() % (byte.MaxValue + 1));
                    p[3] = (byte)(random.Next() % (byte.MaxValue + 1));
                    p += 4;
                }

                while (p < end)
                {
                    *p++ = (byte)(random.Next() % (byte.MaxValue + 1));
                }
            }

            return buffer[length - 1];
        }

        byte[] temp = new byte[buffer.Length];
        _random.NextBytes(temp);
        temp.AsSpan().CopyTo(buffer);
        return buffer[buffer.Length - 1];
    }
}
