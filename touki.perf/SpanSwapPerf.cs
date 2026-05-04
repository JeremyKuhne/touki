// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;

namespace touki.perf;

/// <summary>
///  Compares manual temp-variable swap vs tuple-deconstruction swap on Span elements.
///  Validates whether the IDE0180 ("use tuple swap") rewrite in
///  <c>SpanExtensions.Sort.cs</c> regresses on net472/net481 RyuJIT vs modern .NET.
/// </summary>
[MemoryDiagnoser]
public class SpanSwapPerf
{
    private const int Length = 1024;
    private readonly int[] _keys = new int[Length];
    private readonly int[] _values = new int[Length];

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < Length; i++)
        {
            _keys[i] = i;
            _values[i] = i;
        }
    }

    [Benchmark(Baseline = true)]
    public int Single_Temp()
    {
        Span<int> span = _keys;
        for (int i = 0; i < span.Length - 1; i++)
        {
            SwapTemp(span, i, i + 1);
        }

        return span[0];
    }

    [Benchmark]
    public int Single_Tuple()
    {
        Span<int> span = _keys;
        for (int i = 0; i < span.Length - 1; i++)
        {
            SwapTuple(span, i, i + 1);
        }

        return span[0];
    }

    [Benchmark]
    public int Paired_Temp()
    {
        Span<int> keys = _keys;
        Span<int> values = _values;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            SwapPairedTemp(keys, values, i, i + 1);
        }

        return keys[0] ^ values[0];
    }

    [Benchmark]
    public int Paired_Tuple()
    {
        Span<int> keys = _keys;
        Span<int> values = _values;
        for (int i = 0; i < keys.Length - 1; i++)
        {
            SwapPairedTuple(keys, values, i, i + 1);
        }

        return keys[0] ^ values[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapTemp<T>(Span<T> span, int i, int j)
    {
        T temp = span[i];
        span[i] = span[j];
        span[j] = temp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapTuple<T>(Span<T> span, int i, int j) =>
        (span[i], span[j]) = (span[j], span[i]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapPairedTemp<TKey, TValue>(Span<TKey> keys, Span<TValue> items, int i, int j)
    {
        TKey tk = keys[i];
        keys[i] = keys[j];
        keys[j] = tk;
        TValue tv = items[i];
        items[i] = items[j];
        items[j] = tv;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapPairedTuple<TKey, TValue>(Span<TKey> keys, Span<TValue> items, int i, int j)
    {
        (keys[i], keys[j]) = (keys[j], keys[i]);
        (items[i], items[j]) = (items[j], items[i]);
    }
}
