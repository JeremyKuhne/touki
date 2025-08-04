// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace touki.perf;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class StoreArray
{
    private static readonly byte[] s_byteArray = new byte[10];
    private static readonly ArraySegment<byte> s_byteSegment = new(s_byteArray);
    private static readonly ArraySegment<byte> s_emptyByteSegment = new(s_byteArray, 0, 0);

    [Benchmark(Baseline = true)]
    public byte[] InOutByteArray()
    {
        Value value = Value.Create(s_byteArray);
        value.TryGetValue(out byte[] result);
        return result;
    }

    [Benchmark]
    public ArraySegment<byte> InOutByteSegment()
    {
        Value value = s_byteSegment;
        value.TryGetValue(out ArraySegment<byte> result);
        return result;
    }

    [Benchmark]
    public ArraySegment<byte> InOutEmptyByteSegment()
    {
        Value value = s_emptyByteSegment;
        value.TryGetValue(out ArraySegment<byte> result);
        return result;
    }
}
