// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace touki.perf;

/// <summary>
///  Compares candidate shapes for the polyfilled <c>SpanExtensions.StartsWith&lt;T&gt;(T)</c>.
///  All four shapes return the same boolean for in-range inputs; the differences are
///  purely codegen / dispatch quality on .NET Framework 4.8.1 RyuJIT vs modern RyuJIT.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 5, launchCount: 1)]
public class StartsWithPerf
{
    private byte[] _bytes = null!;
    private short[] _shorts = null!;
    private int[] _ints = null!;
    private long[] _longs = null!;
    private string[] _strings = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bytes = [1, 2, 3];
        _shorts = [-32768, 0, 32767];
        _ints = [int.MinValue, 0, int.MaxValue];
        _longs = [long.MinValue, 0L, long.MaxValue];
        _strings = ["alpha", "beta", "gamma"];
    }

    // --- byte ---

    [Benchmark(Baseline = true)]
    public bool Byte_EqualityComparer() => StartsWith_EqualityComparer<byte>(_bytes, 1);

    [Benchmark]
    public bool Byte_UnsafeOnParameter() => StartsWith_UnsafeOnParameter<byte>(_bytes, 1);

    [Benchmark]
    public bool Byte_UnsafeOnLocalCopy() => StartsWith_UnsafeOnLocalCopy<byte>(_bytes, 1);

    [Benchmark]
    public bool Byte_IEquatableDirect() => StartsWith_IEquatableDirect<byte>(_bytes, 1);

    // --- short ---

    [Benchmark]
    public bool Short_EqualityComparer() => StartsWith_EqualityComparer<short>(_shorts, -32768);

    [Benchmark]
    public bool Short_UnsafeOnParameter() => StartsWith_UnsafeOnParameter<short>(_shorts, -32768);

    [Benchmark]
    public bool Short_UnsafeOnLocalCopy() => StartsWith_UnsafeOnLocalCopy<short>(_shorts, -32768);

    [Benchmark]
    public bool Short_IEquatableDirect() => StartsWith_IEquatableDirect<short>(_shorts, -32768);

    // --- int ---

    [Benchmark]
    public bool Int_EqualityComparer() => StartsWith_EqualityComparer<int>(_ints, int.MinValue);

    [Benchmark]
    public bool Int_UnsafeOnParameter() => StartsWith_UnsafeOnParameter<int>(_ints, int.MinValue);

    [Benchmark]
    public bool Int_UnsafeOnLocalCopy() => StartsWith_UnsafeOnLocalCopy<int>(_ints, int.MinValue);

    [Benchmark]
    public bool Int_IEquatableDirect() => StartsWith_IEquatableDirect<int>(_ints, int.MinValue);

    // --- long ---

    [Benchmark]
    public bool Long_EqualityComparer() => StartsWith_EqualityComparer<long>(_longs, long.MinValue);

    [Benchmark]
    public bool Long_UnsafeOnParameter() => StartsWith_UnsafeOnParameter<long>(_longs, long.MinValue);

    [Benchmark]
    public bool Long_UnsafeOnLocalCopy() => StartsWith_UnsafeOnLocalCopy<long>(_longs, long.MinValue);

    [Benchmark]
    public bool Long_IEquatableDirect() => StartsWith_IEquatableDirect<long>(_longs, long.MinValue);

    // --- string (reference type) ---

    [Benchmark]
    public bool String_EqualityComparer() => StartsWith_EqualityComparer<string>(_strings, "alpha");

    [Benchmark]
    public bool String_IEquatableDirect() => StartsWith_IEquatableDirect<string>(_strings, "alpha");

    // ----------------------------------------------------------------------
    // Candidate implementations
    // ----------------------------------------------------------------------

    /// <summary>
    ///  Current shipped shape (commit 8ac1ce7). Goes through
    ///  <see cref="EqualityComparer{T}.Default"/>, which on net481 RyuJIT pays a
    ///  static-property load + virtual call + boxing-through-IEquatable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWith_EqualityComparer<T>(ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>?
    {
        return span.Length != 0 && EqualityComparer<T>.Default.Equals(span[0], value);
    }

    /// <summary>
    ///  Original Unsafe fast path (commit 4cdd2d6 + this PR's first version).
    ///  BUG: <see cref="Unsafe.As{TFrom, TTo}(ref TFrom)"/> on the parameter
    ///  <paramref name="value"/> can read the wrong byte if RyuJIT keeps the
    ///  parameter in a register or in a smaller-than-int stack slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWith_UnsafeOnParameter<T>(ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>?
    {
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) || typeof(T) == typeof(bool))
        {
            return span.Length != 0
                && Unsafe.As<T, byte>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, byte>(ref value);
        }

        if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            return span.Length != 0
                && Unsafe.As<T, ushort>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, ushort>(ref value);
        }

        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
        {
            return span.Length != 0
                && Unsafe.As<T, uint>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, uint>(ref value);
        }

        if (typeof(T) == typeof(long) || typeof(T) == typeof(ulong))
        {
            return span.Length != 0
                && Unsafe.As<T, ulong>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, ulong>(ref value);
        }

        return span.Length != 0 && EqualityComparer<T>.Default.Equals(span[0], value);
    }

    /// <summary>
    ///  Same as <see cref="StartsWith_UnsafeOnParameter{T}"/> but copies the parameter
    ///  to a local first so the address is taken from a definite stack slot.
    ///  This is the candidate fix for the negative-signed-primitive bug.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWith_UnsafeOnLocalCopy<T>(ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>?
    {
        if (typeof(T) == typeof(byte) || typeof(T) == typeof(sbyte) || typeof(T) == typeof(bool))
        {
            T local = value;
            return span.Length != 0
                && Unsafe.As<T, byte>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, byte>(ref local);
        }

        if (typeof(T) == typeof(char) || typeof(T) == typeof(short) || typeof(T) == typeof(ushort))
        {
            T local = value;
            return span.Length != 0
                && Unsafe.As<T, ushort>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, ushort>(ref local);
        }

        if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
        {
            T local = value;
            return span.Length != 0
                && Unsafe.As<T, uint>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, uint>(ref local);
        }

        if (typeof(T) == typeof(long) || typeof(T) == typeof(ulong))
        {
            T local = value;
            return span.Length != 0
                && Unsafe.As<T, ulong>(ref Unsafe.AsRef(in MemoryMarshal.GetReference(span)))
                    == Unsafe.As<T, ulong>(ref local);
        }

        return span.Length != 0 && EqualityComparer<T>.Default.Equals(span[0], value);
    }

    /// <summary>
    ///  Calls <see cref="IEquatable{T}.Equals(T)"/> directly on the value parameter.
    ///  For value types this is a constrained-call to the value's own Equals (no box,
    ///  no comparer indirection). For reference types we need an explicit null check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StartsWith_IEquatableDirect<T>(ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>?
    {
        if (span.Length == 0)
        {
            return false;
        }

        T first = span[0];
        return value is null ? first is null : value.Equals(first);
    }
}
