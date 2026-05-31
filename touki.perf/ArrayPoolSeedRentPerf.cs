// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace touki.perf;

/// <summary>
///  Isolates the per-call cost of the extglob engine's five seed buffers when
///  they are rented from <see cref="ArrayPool{T}"/> (uninitialized) versus
///  <c>stackalloc</c> (zero-initialized). This is the variable behind the
///  <c>RunEngine</c> seed swap: on .NET Framework 4.8.1 RyuJIT the pool path
///  regressed every <c>GlobExtGlobMatchPerf</c> row, while modern .NET RyuJIT
///  improved. The benchmark answers "why is the rental so expensive, and why
///  doesn't warmup hide it" by measuring the rent/return sequence directly.
/// </summary>
/// <remarks>
///  <para>
///   The buffer shapes mirror the engine seed exactly: a 28-byte frame struct
///   times 32, a 16-byte range struct times 128, the same range struct times 18
///   twice (the <c>work</c> and <c>rest</c> lists), and an int key buffer of 57.
///   Element counts drive the <see cref="ArrayPool{T}"/> bucket selection, and
///   the two same-sized range rentals deliberately hit the same bucket - the
///   thread-local cache holds only one array per bucket, so the second rental
///   misses the fast path and falls to the per-core locked stack.
///  </para>
///  <para>
///   Three distinct element types (frame, range, int) reproduce the engine's
///   three independent <c>ArrayPool&lt;T&gt;.Shared</c> instances. Total seed
///   footprint is ~3.7 KB, matching the zero-init the stackalloc path pays on
///   net481 unless the method is annotated with <c>[SkipLocalsInit]</c> (which
///   the Framework JIT does honor).
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class ArrayPoolSeedRentPerf
{
    private const int SeedFrameCount = 32;
    private const int SeedArenaCount = 128;
    // Mirrors CompiledGlobStrategy.MaxRangesDepth = (MaxExtGlobDepth * 2) + 2 = 18.
    private const int MaxRangesDepth = 18;
    private const int KeyLength = 3 + (MaxRangesDepth * 3);

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameLike
    {
        public int A, B, C, D, E, F, G;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RangeLike
    {
        public int A, B, C, D;
    }

    [GlobalSetup]
    public void Setup()
    {
        // Warm the pool exactly as a real workload would: rent and return each
        // bucket once so the thread-local caches are populated before measuring.
        for (int i = 0; i < 64; i++)
        {
            RentSeed();
        }
    }

    /// <summary>
    ///  Rents the five seed buffers, touches each once, and returns them. This is
    ///  what the <c>RunEngine</c> BufferScope swap does on every match.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int PoolRent()
    {
        return RentSeed();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RentSeed()
    {
        FrameLike[] frames = ArrayPool<FrameLike>.Shared.Rent(SeedFrameCount);
        RangeLike[] arena = ArrayPool<RangeLike>.Shared.Rent(SeedArenaCount);
        RangeLike[] work = ArrayPool<RangeLike>.Shared.Rent(MaxRangesDepth);
        RangeLike[] rest = ArrayPool<RangeLike>.Shared.Rent(MaxRangesDepth);
        int[] key = ArrayPool<int>.Shared.Rent(KeyLength);

        // Touch a slot in each so the rentals cannot be optimized away.
        frames[0].A = 1;
        arena[0].A = 1;
        work[0].A = 1;
        rest[0].A = 1;
        key[0] = 1;
        int sum = frames[0].A + arena[0].A + work[0].A + rest[0].A + key[0];

        ArrayPool<int>.Shared.Return(key);
        ArrayPool<RangeLike>.Shared.Return(rest);
        ArrayPool<RangeLike>.Shared.Return(work);
        ArrayPool<RangeLike>.Shared.Return(arena);
        ArrayPool<FrameLike>.Shared.Return(frames);
        return sum;
    }

    /// <summary>
    ///  Zero-initialized <c>stackalloc</c> of the same five seed buffers. On
    ///  net481 RyuJIT this pays a ~3.7 KB zeroing on entry that cannot be
    ///  suppressed; on modern .NET RyuJIT the zeroing dominates this path.
    /// </summary>
    [Benchmark]
    public int StackAllocSeed()
    {
        return StackSeed();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StackSeed()
    {
        Span<FrameLike> frames = stackalloc FrameLike[SeedFrameCount];
        Span<RangeLike> arena = stackalloc RangeLike[SeedArenaCount];
        Span<RangeLike> work = stackalloc RangeLike[MaxRangesDepth];
        Span<RangeLike> rest = stackalloc RangeLike[MaxRangesDepth];
        Span<int> key = stackalloc int[KeyLength];

        frames[0].A = 1;
        arena[0].A = 1;
        work[0].A = 1;
        rest[0].A = 1;
        key[0] = 1;
        return frames[0].A + arena[0].A + work[0].A + rest[0].A + key[0];
    }
}
