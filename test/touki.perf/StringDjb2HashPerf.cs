// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace touki.perf;

/// <summary>
///  Compares the previous scalar resource-name hash with the shared DJB2 helper on .NET Framework
///  4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class StringDjb2HashPerf
{
    private string _value = string.Empty;

    /// <summary>
    ///  The number of UTF-16 code units to hash.
    /// </summary>
    [Params(1, 8, 32, 64, 127, 1024)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        Random random = new(12345);
        char[] characters = new char[Length];
        for (int i = 0; i < characters.Length; i++)
        {
            characters[i] = (char)random.Next(32, ushort.MaxValue);
        }

        _value = new(characters);
    }

    [Benchmark(Baseline = true)]
    public int ScalarSpan()
    {
        ReadOnlySpan<char> value = _value;
        uint hash = 5381;
        for (int i = 0; i < value.Length; i++)
        {
            hash = ((hash << 5) + hash) ^ value[i];
        }

        return (int)hash;
    }

    [Benchmark]
    public int SpanUnrolled4()
    {
        ReadOnlySpan<char> value = _value;
        uint hash = 5381;
        int index = 0;
        int unrolledLength = value.Length & ~3;
        while (index < unrolledLength)
        {
            hash = ((hash << 5) + hash) ^ value[index];
            hash = ((hash << 5) + hash) ^ value[index + 1];
            hash = ((hash << 5) + hash) ^ value[index + 2];
            hash = ((hash << 5) + hash) ^ value[index + 3];
            index += 4;
        }

        while (index < value.Length)
        {
            hash = ((hash << 5) + hash) ^ value[index++];
        }

        return (int)hash;
    }

    [Benchmark]
    public int RefUnrolled4()
    {
        ReadOnlySpan<char> value = _value;
        ref char first = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(value);
        uint hash = 5381;
        int index = 0;
        int unrolledLength = value.Length & ~3;
        while (index < unrolledLength)
        {
            hash = ((hash << 5) + hash) ^ System.Runtime.CompilerServices.Unsafe.Add(ref first, index);
            hash = ((hash << 5) + hash) ^ System.Runtime.CompilerServices.Unsafe.Add(ref first, index + 1);
            hash = ((hash << 5) + hash) ^ System.Runtime.CompilerServices.Unsafe.Add(ref first, index + 2);
            hash = ((hash << 5) + hash) ^ System.Runtime.CompilerServices.Unsafe.Add(ref first, index + 3);
            index += 4;
        }

        while (index < value.Length)
        {
            hash = ((hash << 5) + hash) ^ System.Runtime.CompilerServices.Unsafe.Add(ref first, index++);
        }

        return (int)hash;
    }

#if NET
    [Benchmark]
    public int Vector128Load()
    {
        ReadOnlySpan<char> value = _value;
        ref char firstChar = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(value);
        ref ushort first = ref System.Runtime.CompilerServices.Unsafe.As<char, ushort>(ref firstChar);
        uint hash = 5381;
        int index = 0;
        int width = System.Runtime.Intrinsics.Vector128<ushort>.Count;
        int vectorizedLength = value.Length - width;
        while (index <= vectorizedLength)
        {
            System.Runtime.Intrinsics.Vector128<ushort> characters =
                System.Runtime.Intrinsics.Vector128.LoadUnsafe(ref first, (nuint)index);

            for (int lane = 0; lane < width; lane++)
            {
                hash = ((hash << 5) + hash)
                    ^ System.Runtime.Intrinsics.Vector128.GetElement(characters, lane);
            }

            index += width;
        }

        while (index < value.Length)
        {
            hash = ((hash << 5) + hash) ^ System.Runtime.CompilerServices.Unsafe.Add(ref first, index++);
        }

        return (int)hash;
    }
#endif

    [Benchmark]
    public int SharedDjb2Helper() => string.GetDJB2HashCode(_value);
}