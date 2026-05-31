// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace touki.perf;

/// <summary>
///  Measures the cost of zero-initializing stack scratch buffers, how that cost
///  scales with size and element type, and whether <c>[SkipLocalsInit]</c> and
///  the fixed-buffer-plus-<see cref="Unsafe.SkipInit{T}"/> pattern actually
///  suppress the zeroing on each target framework.
/// </summary>
/// <remarks>
///  <para>
///   This is the evidence base for the ArrayPool-versus-zeroing decision guide.
///   The headline comparison is ".NET Framework 4.8.1 RyuJIT" (no tiered JIT,
///   weaker inlining, and - the question this benchmark answers - does it honor
///   the absence of the <c>localsinit</c> flag?) against "modern .NET RyuJIT".
///  </para>
///  <para>
///   Every method routes its <c>stackalloc</c> through a <c>[NoInlining]</c>
///   helper so the allocation cannot be hoisted out or folded away, and returns
///   a value derived from a few touched slots so dead-code elimination cannot
///   delete the buffer.
///  </para>
/// </remarks>
[MemoryDiagnoser]
public class StackZeroInitPerf
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Range16
    {
        public int A, B, C, D;
    }

    // ---- Zeroing cost by size (default localsinit: the buffer is zeroed) ----

    [Benchmark]
    public int Zero_Byte_256() => TouchBytes256();

    [Benchmark]
    public int Zero_Byte_1024() => TouchBytes1024();

    [Benchmark]
    public int Zero_Byte_4096() => TouchBytes4096();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TouchBytes256()
    {
        Span<byte> s = stackalloc byte[256];
        s[0] = 1;
        s[255] = 2;
        return s[0] + s[255];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TouchBytes1024()
    {
        Span<byte> s = stackalloc byte[1024];
        s[0] = 1;
        s[1023] = 2;
        return s[0] + s[1023];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TouchBytes4096()
    {
        Span<byte> s = stackalloc byte[4096];
        s[0] = 1;
        s[4095] = 2;
        return s[0] + s[4095];
    }

    // ---- Struct vs primitive at an equal ~4 KB byte size ----
    // 4096 bytes as byte[4096], int[1024], and Range16[256]. If zeroing is a
    // byte-wise memset the element type should not matter for equal byte size.

    [Benchmark]
    public int Zero_Int_1024() => TouchInts1024();

    [Benchmark]
    public int Zero_Struct16_256() => TouchStruct256();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TouchInts1024()
    {
        Span<int> s = stackalloc int[1024];
        s[0] = 1;
        s[1023] = 2;
        return s[0] + s[1023];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int TouchStruct256()
    {
        Span<Range16> s = stackalloc Range16[256];
        s[0].A = 1;
        s[255].D = 2;
        return s[0].A + s[255].D;
    }

    // ---- [SkipLocalsInit] effect at 4 KB ----
    // Same 4 KB stackalloc with and without the localsinit flag. On modern .NET
    // RyuJIT the SkipInit variant drops the zeroing; whether net481 RyuJIT does
    // is exactly what this pair measures.

    [Benchmark]
    public int Zeroed_4096() => ZeroedHelper();

    [Benchmark]
    public int SkipInit_4096() => SkipInitHelper();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int ZeroedHelper()
    {
        Span<byte> s = stackalloc byte[4096];
        s[0] = 1;
        s[4095] = 2;
        return s[0] + s[4095];
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [SkipLocalsInit]
    private static int SkipInitHelper()
    {
        Span<byte> s = stackalloc byte[4096];
        s[0] = 1;
        s[4095] = 2;
        return s[0] + s[4095];
    }

    // ---- Fixed-buffer ref struct + Unsafe.SkipInit (the no-zero scratch
    // pattern). The struct carries a 4 KB inline fixed buffer; SkipInit leaves
    // it uninitialized, and a scoped Span property exposes it. Available on both
    // TFMs because Unsafe.SkipInit ships in the netstandard Unsafe surface. ----

    [Benchmark]
    public int FixedBufferSkipInit() => FixedBufferHelper();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int FixedBufferHelper()
    {
        Unsafe.SkipInit(out Scratch4K scratch);
        Span<byte> s = scratch.AsSpan();
        s[0] = 1;
        s[4095] = 2;
        return s[0] + s[4095];
    }

    private unsafe struct Scratch4K
    {
        public fixed byte Buffer[4096];

        [UnscopedRef]
        public Span<byte> AsSpan() => new(Unsafe.AsPointer(ref Buffer[0]), 4096);
    }
}
