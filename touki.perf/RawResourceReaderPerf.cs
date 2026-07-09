// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;
using System.Resources;
using System.Runtime.InteropServices;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures <see cref="RawResourceReader"/> against the runtime's
///  <see cref="System.Resources.ResourceReader"/> (the oracle) on both .NET Framework 4.8.1 RyuJIT
///  and modern .NET RyuJIT.
/// </summary>
/// <remarks>
///  <para>
///   Each logical operation - open a reader and look up one value, and a repeated lookup on a cached
///   reader - is measured for the oracle and for <see cref="RawResourceReader"/> over two backings: a
///   regular seekable stream (the oracle's array-allocating path) and an
///   <see cref="System.IO.UnmanagedMemoryStream"/> / native memory (its pointer fast path). The
///   <c>[MemoryDiagnoser]</c> column is the point of the comparison: <see cref="RawResourceReader"/>
///   allocates nothing for open, lookup, or read, where the oracle allocates its index arrays and a
///   <c>byte[]</c> per value.
///  </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net481, warmupCount: 1, iterationCount: 3, launchCount: 1)]
[SimpleJob(RuntimeMoniker.HostProcess, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class RawResourceReaderPerf
{
    private const int ResourceCount = 200;

    private byte[] _bytes = null!;
    private string _path = null!;
    private string _lookupKey = null!;
    private readonly byte[] _scratch = new byte[256];

    private ResourceReader _cachedOracle = null!;
    private RawResourceReader _cachedRaw = null!;

    private unsafe byte* _nativePointer;
    private int _nativeLength;
    private NativeMemoryManager _nativeManager = null!;
    private ReadOnlyMemory<byte> _nativeMemory;

    [GlobalSetup]
    public unsafe void Setup()
    {
        System.IO.MemoryStream stream = new();
        using (ResourceWriter writer = new(stream))
        {
            for (int i = 0; i < ResourceCount; i++)
            {
                writer.AddResource($"resource_{i}", $"value_{i}");
            }

            writer.Generate();
        }

        _bytes = stream.ToArray();
        _lookupKey = $"resource_{ResourceCount / 2}";

        _path = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllBytes(_path, _bytes);

        _cachedOracle = new ResourceReader(new System.IO.MemoryStream(_bytes));
        _cachedRaw = new RawResourceReader(_bytes);

        _nativeLength = _bytes.Length;
        _nativePointer = (byte*)Marshal.AllocHGlobal(_nativeLength);
        _bytes.AsSpan().CopyTo(new Span<byte>(_nativePointer, _nativeLength));
        _nativeManager = new NativeMemoryManager(_nativePointer, _nativeLength);
        _nativeMemory = _nativeManager.Memory;
    }

    [GlobalCleanup]
    public unsafe void Cleanup()
    {
        _cachedOracle?.Dispose();
        _cachedRaw?.Dispose();

        if (_nativePointer is not null)
        {
            Marshal.FreeHGlobal((nint)_nativePointer);
            _nativePointer = null;
        }

        if (_path is not null && System.IO.File.Exists(_path))
        {
            System.IO.File.Delete(_path);
        }
    }

    [Benchmark(Baseline = true)]
    public int Oracle_DiskStream_OpenLookup()
    {
        using ResourceReader reader = new(new System.IO.FileStream(
            _path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read));
        reader.GetResourceData(_lookupKey, out _, out byte[] data);
        return data.Length;
    }

    [Benchmark]
    public int Raw_MappedFile_OpenLookup()
    {
        using RawResourceReader reader = RawResourceReader.CreateFromFile(_path);
        reader.TryFindResource(_lookupKey, out ResourceLocation location);
        reader.TryGetResourceData(location.Index, _scratch, out int written);
        return written;
    }

    [Benchmark]
    public unsafe int Oracle_UnmanagedStream_OpenLookup()
    {
        using ResourceReader reader = new(new System.IO.UnmanagedMemoryStream(_nativePointer, _nativeLength));
        reader.GetResourceData(_lookupKey, out _, out byte[] data);
        return data.Length;
    }

    [Benchmark]
    public int Raw_NativeMemory_OpenLookup()
    {
        RawResourceReader reader = new(_nativeMemory);
        reader.TryFindResource(_lookupKey, out ResourceLocation location);
        reader.TryGetResourceData(location.Index, _scratch, out int written);
        return written;
    }

    [Benchmark]
    public int Oracle_CachedReader_Lookup()
    {
        _cachedOracle.GetResourceData(_lookupKey, out _, out byte[] data);
        return data.Length;
    }

    [Benchmark]
    public int Raw_CachedReader_Lookup()
    {
        _cachedRaw.TryFindResource(_lookupKey, out ResourceLocation location);
        _cachedRaw.TryGetResourceData(location.Index, _scratch, out int written);
        return written;
    }

    private sealed unsafe class NativeMemoryManager : MemoryManager<byte>
    {
        private readonly byte* _pointer;
        private readonly int _length;

        public NativeMemoryManager(byte* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        public override Span<byte> GetSpan() => new(_pointer, _length);

        public override MemoryHandle Pin(int elementIndex = 0) => new(_pointer + elementIndex);

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
