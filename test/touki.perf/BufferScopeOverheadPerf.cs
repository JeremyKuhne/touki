// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;
using System.Runtime.CompilerServices;
using Touki.Buffers;

namespace touki.perf;

/// <summary>
///  Measures the overhead of <see cref="BufferScope{T}"/> - the "start on a
///  stack buffer, fall back to an <see cref="ArrayPool{T}"/> rental when more
///  space is needed" pattern - against the hand-written equivalents on each
///  path, and confirms that the wrapper does not interfere with
///  <c>[SkipLocalsInit]</c>.
/// </summary>
/// <remarks>
///  <para>
///   <see cref="BufferScope{T}"/> is a <c>ref struct</c> that wraps a
///   caller-supplied <c>stackalloc</c> span and only rents from the pool when
///   the requested capacity exceeds the stack buffer. Because the
///   <c>stackalloc</c> lives in the caller, the caller's <c>[SkipLocalsInit]</c>
///   controls whether it is zeroed; the wrapper itself never clears memory.
///  </para>
///  <para>
///   The benchmarks come in pairs so the wrapper overhead is the difference
///   within each pair:
///   <list type="bullet">
///    <item><c>Direct_StackOnly</c> vs <c>Scope_StackOnly</c> - the stack-only
///     fast path (no rental); isolates the cost of constructing and disposing
///     the scope.</item>
///    <item><c>Direct_Rent</c> vs <c>Scope_Rent</c> - the grow path where the
///     requested size overflows the stack buffer and the scope rents.</item>
///    <item><c>Scope_StackOnly_Zeroed</c> - identical to <c>Scope_StackOnly</c>
///     but without <c>[SkipLocalsInit]</c>, so the delta to it shows the
///     wrapper passes the localsinit decision straight through to the caller.
///    </item>
///   </list>
///  </para>
///  <para>
///   Every method routes through a <c>[NoInlining]</c> helper so the wrapper
///   cannot be folded into the harness and a few slots are touched so the
///   buffer cannot be elided. The stack buffer is 256 bytes; the rented path
///   asks for 1024 bytes so it always overflows it.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class BufferScopeOverheadPerf
{
    private const int StackSize = 256;
    private const int RentSize = 1024;

    [GlobalSetup]
    public void Setup()
    {
        // Warm the bucket the rented path uses so Scope_Rent / Direct_Rent see a
        // steady-state thread-local cache hit rather than a cold miss.
        for (int i = 0; i < 64; i++)
        {
            byte[] warm = ArrayPool<byte>.Shared.Rent(RentSize);
            ArrayPool<byte>.Shared.Return(warm);
        }
    }

    // ---- Stack-only fast path (no rental) ----

    [Benchmark(Baseline = true)]
    [SkipLocalsInit]
    public int Direct_StackOnly()
    {
        Span<byte> buffer = stackalloc byte[StackSize];
        return Touch(buffer);
    }

    [Benchmark]
    [SkipLocalsInit]
    public int Scope_StackOnly()
    {
        using BufferScope<byte> scope = new(stackalloc byte[StackSize], StackSize);
        return Touch(scope.AsSpan());
    }

    [Benchmark]
    public int Scope_StackOnly_Zeroed()
    {
        using BufferScope<byte> scope = new(stackalloc byte[StackSize], StackSize);
        return Touch(scope.AsSpan());
    }

    // ---- Grow path (stack buffer too small, scope rents) ----

    [Benchmark]
    public int Direct_Rent()
    {
        byte[] array = ArrayPool<byte>.Shared.Rent(RentSize);
        int sum = Touch(array.AsSpan(0, RentSize));
        ArrayPool<byte>.Shared.Return(array);
        return sum;
    }

    [Benchmark]
    [SkipLocalsInit]
    public int Scope_Rent()
    {
        using BufferScope<byte> scope = new(stackalloc byte[StackSize], RentSize);
        return Touch(scope.AsSpan());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Touch(Span<byte> buffer)
    {
        buffer[0] = 1;
        buffer[^1] = 2;
        return buffer[0] + buffer[^1];
    }
}
