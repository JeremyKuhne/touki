// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;
using System.Runtime.CompilerServices;

namespace touki.perf;

/// <summary>
///  Finds the buffer size at which zero-initializing a <c>stackalloc</c> local
///  becomes more expensive than renting an (uninitialized) array from
///  <see cref="ArrayPool{T}"/>. The sweep is run for two rental shapes - a
///  thread-local cache hit (the first rental of a bucket) and a per-core locked
///  stack hit (the second rental of the same bucket while the first is still
///  checked out) - on both target frameworks.
/// </summary>
/// <remarks>
///  <para>
///   The zeroing path uses a dynamically sized <c>stackalloc byte[Size]</c> with
///   the default <c>localsinit</c> flag, so the buffer is cleared on every call;
///   its cost grows with <see cref="Size"/>. The rental path returns memory of at
///   least the requested size without clearing it, so its cost is roughly flat
///   across sizes (bucket math plus, for the locked path, a lock). The crossover
///   is where the rising zeroing line meets the flat rental line.
///  </para>
///  <para>
///   <see cref="RentTls"/> rents and returns one array; after warmup the bucket's
///   one-deep thread-local slot is populated, so this is the fast path.
///   <see cref="RentLocked"/> rents two same-bucket arrays before returning
///   either: the first drains the thread-local slot, forcing the second onto the
///   per-core locked stack. The marginal cost of that second rental is
///   <c>RentLocked - RentTls</c>.
///  </para>
///  <para>
///   Every buffer is routed through a <c>[NoInlining]</c> helper and a few slots
///   are touched so neither the allocation nor the zeroing can be elided.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class ArrayPoolCrossoverPerf
{
    [Params(64, 128, 256, 512, 1024, 2048, 4096, 8192)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Populate the thread-local slot for every bucket this sweep touches so
        // RentTls measures a steady-state cache hit rather than a cold miss.
        for (int i = 0; i < 64; i++)
        {
            RentOne(Size);
            RentTwo(Size);
        }
    }

    /// <summary>
    ///  Zero-initialized <c>stackalloc</c> of <see cref="Size"/> bytes. The cost
    ///  is the <c>localsinit</c> clear and scales with the byte count.
    /// </summary>
    [Benchmark(Baseline = true)]
    public int ZeroStack() => ZeroStackCore(Size);

    /// <summary>
    ///  Single rent plus return - the thread-local cache fast path.
    /// </summary>
    [Benchmark]
    public int RentTls() => RentOne(Size);

    /// <summary>
    ///  Two same-bucket rentals before returning either, so the second falls to
    ///  the per-core locked stack. The marginal second-rental cost is this row
    ///  minus <see cref="RentTls"/>.
    /// </summary>
    [Benchmark]
    public int RentLocked() => RentTwo(Size);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ZeroStackCore(int size)
    {
        Span<byte> buffer = stackalloc byte[size];
        buffer[0] = 1;
        buffer[size - 1] = 2;
        return buffer[0] + buffer[size - 1];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RentOne(int size)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
        buffer[0] = 1;
        buffer[size - 1] = 2;
        int sum = buffer[0] + buffer[size - 1];
        ArrayPool<byte>.Shared.Return(buffer);
        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int RentTwo(int size)
    {
        byte[] first = ArrayPool<byte>.Shared.Rent(size);
        byte[] second = ArrayPool<byte>.Shared.Rent(size);
        first[0] = 1;
        second[size - 1] = 2;
        int sum = first[0] + second[size - 1];
        ArrayPool<byte>.Shared.Return(second);
        ArrayPool<byte>.Shared.Return(first);
        return sum;
    }
}
