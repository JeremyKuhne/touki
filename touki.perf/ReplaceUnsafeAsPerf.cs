// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;

#pragma warning disable CS8500 // takes the address of, gets the size of, or declares a pointer to a managed type

namespace touki.perf;

/// <summary>
///  Investigates the net481 RyuJIT codegen discrepancy between
///  <c>Unsafe.As&lt;T, byte&gt;(ref methodParameter)</c> inside an
///  <see cref="MethodImplOptions.AggressiveInlining"/> method and the
///  same operation routed through a <c>[NoInlining]</c> primitive-typed
///  helper.
///
///  Use BenchmarkDotNet's <c>--disasm</c> (DisassemblyDiagnoser) to see
///  the actual x64 machine code RyuJIT emits for each shape on net481.
/// </summary>
[DisassemblyDiagnoser(maxDepth: 5, printSource: true, exportHtml: true)]
[SimpleJob(RuntimeMoniker.Net481, warmupCount: 1, iterationCount: 1, launchCount: 1)]
public class ReplaceUnsafeAsPerf
{
    private sbyte[] _data = null!;

    [GlobalSetup]
    public void Setup() => _data = [-1, 2, -1, 4, -1, 6, -1, 8];

    // ----- Buggy pattern (Unsafe.As on the AggressiveInlining method's parameter) -----

    [Benchmark(Baseline = true)]
    public sbyte Buggy_Inlined()
    {
        sbyte[] data = _data;
        ReplaceBuggy<sbyte>(data, (sbyte)(-1), (sbyte)0);
        sbyte result = data[0];
        // Restore so subsequent iterations see the same input.
        data[0] = -1;
        data[2] = -1;
        data[4] = -1;
        data[6] = -1;
        return result;
    }

    [Benchmark]
    public sbyte Fixed_NoInliningHelper()
    {
        sbyte[] data = _data;
        ReplaceFixed<sbyte>(data, (sbyte)(-1), (sbyte)0);
        sbyte result = data[0];
        data[0] = -1;
        data[2] = -1;
        data[4] = -1;
        data[6] = -1;
        return result;
    }

    [Benchmark]
    public sbyte SplitBlocks()
    {
        sbyte[] data = _data;
        ReplaceSplitBlocks<sbyte>(data, (sbyte)(-1), (sbyte)0);
        sbyte result = data[0];
        data[0] = -1;
        data[2] = -1;
        data[4] = -1;
        data[6] = -1;
        return result;
    }

    [Benchmark]
    public sbyte ExplicitMask()
    {
        sbyte[] data = _data;
        ReplaceExplicitMask<sbyte>(data, (sbyte)(-1), (sbyte)0);
        sbyte result = data[0];
        data[0] = -1;
        data[2] = -1;
        data[4] = -1;
        data[6] = -1;
        return result;
    }

    // ----- Implementations -----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReplaceBuggy<T>(Span<T> span, T oldValue, T newValue)
        where T : struct
    {
        // Mirrors the original SpanExtensions.Replace<T> shape.
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
        {
            byte oldByte = Unsafe.As<T, byte>(ref oldValue);
            byte newByte = Unsafe.As<T, byte>(ref newValue);
            fixed (T* p = span)
            {
                byte* ptr = (byte*)p;
                byte* end = ptr + span.Length;
                while (ptr < end)
                {
                    if (*ptr == oldByte)
                    {
                        *ptr = newByte;
                    }

                    ptr++;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReplaceFixed<T>(Span<T> span, T oldValue, T newValue)
        where T : struct
    {
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
        {
            byte oldByte = ReadAsByte(oldValue);
            byte newByte = ReadAsByte(newValue);
            fixed (T* p = span)
            {
                byte* ptr = (byte*)p;
                byte* end = ptr + span.Length;
                while (ptr < end)
                {
                    if (*ptr == oldByte)
                    {
                        *ptr = newByte;
                    }

                    ptr++;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static byte ReadAsByte<T>(T value) where T : struct
    {
        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<T, byte>(ref value);
        }

        return (byte)Unsafe.As<T, sbyte>(ref value);
    }

    // Separate blocks per signed/unsigned primitive. Reading sbyte through a
    // matching Unsafe.As<T, sbyte> followed by a (byte) cast forces a
    // conv.u1 in the IL, which RyuJIT lowers to either a movsx+movzx pair or
    // a constant fold to 0xFF for a literal -1 — never the buggy 32-bit
    // sign-extended compare immediate.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReplaceSplitBlocks<T>(Span<T> span, T oldValue, T newValue)
        where T : struct
    {
        if (typeof(T) == typeof(byte))
        {
            byte oldByte = Unsafe.As<T, byte>(ref oldValue);
            byte newByte = Unsafe.As<T, byte>(ref newValue);
            ReplaceByteLoop(span, oldByte, newByte);
            return;
        }

        if (typeof(T) == typeof(sbyte))
        {
            byte oldByte = (byte)Unsafe.As<T, sbyte>(ref oldValue);
            byte newByte = (byte)Unsafe.As<T, sbyte>(ref newValue);
            ReplaceByteLoop(span, oldByte, newByte);
            return;
        }
    }

    // Forces the JIT to materialize the byte truncation by ANDing with 0xFF
    // even after the buggy <T, byte> Unsafe.As propagation.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReplaceExplicitMask<T>(Span<T> span, T oldValue, T newValue)
        where T : struct
    {
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte))
        {
            byte oldByte = (byte)(Unsafe.As<T, byte>(ref oldValue) & 0xFF);
            byte newByte = (byte)(Unsafe.As<T, byte>(ref newValue) & 0xFF);
            ReplaceByteLoop(span, oldByte, newByte);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void ReplaceByteLoop<T>(Span<T> span, byte oldByte, byte newByte) where T : struct
    {
        fixed (T* p = span)
        {
            byte* ptr = (byte*)p;
            byte* end = ptr + span.Length;
            while (ptr < end)
            {
                if (*ptr == oldByte)
                {
                    *ptr = newByte;
                }

                ptr++;
            }
        }
    }
}
