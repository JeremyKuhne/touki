// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Measures cache hits and alternating-key lookup allocations on .NET Framework 4.8.1 RyuJIT
///  and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class IndexedStringResourceTableLookupPerf
{
    /// <summary>
    ///  Resource backings used to separate cache overhead from string decoding.
    /// </summary>
    public enum ReaderKind
    {
        /// <summary>
        ///  A reader that returns existing strings without allocating.
        /// </summary>
        AllocationFree,

        /// <summary>
        ///  An indexed reader over managed resource bytes.
        /// </summary>
        Memory,

        /// <summary>
        ///  An indexed reader over a seekable resource stream.
        /// </summary>
        Stream
    }

    [AllowNull]
    private IndexedStringResourceTable _table;

    /// <summary>
    ///  The resource reader backing the table.
    /// </summary>
    [Params(ReaderKind.AllocationFree, ReaderKind.Memory, ReaderKind.Stream)]
    public ReaderKind Reader { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        byte[] resources;
        using (System.IO.MemoryStream stream = new())
        {
            using ResourceWriter writer = new(stream);
            writer.AddResource("Greeting", "Hello");
            writer.AddResource("Farewell", "Goodbye");
            writer.Generate();
            resources = stream.ToArray();
        }

        IStringResourceReader reader = Reader switch
        {
            ReaderKind.AllocationFree => new AllocationFreeStringResourceReader(),
            ReaderKind.Memory => new RawResourceReader(resources),
            ReaderKind.Stream => new StreamStringResourceReader(
                new System.IO.MemoryStream(resources, writable: false)),
            _ => throw new InvalidOperationException("The resource reader kind is invalid.")
        };

        _table = IndexedStringResourceTable.Create(reader, StringResourceManagerOptions.None);
        _ = _table.Lookup("Farewell");
    }

    [GlobalCleanup]
    public void Cleanup() => _table.Dispose();

    [Benchmark(Baseline = true)]
    public int RepeatedName()
    {
        return _table.Lookup("Farewell")?.Length ?? 0;
    }

    [Benchmark(OperationsPerInvoke = 2)]
    public int AlternatingNames()
    {
        string? greeting = _table.Lookup("Greeting");
        string? farewell = _table.Lookup("Farewell");
        return (greeting?.Length ?? 0) + (farewell?.Length ?? 0);
    }

    private sealed class AllocationFreeStringResourceReader : IStringResourceReader
    {
        public int ResourceCount => 2;

        public ResourceTypeCode GetResourceTypeCode(int index) => ResourceTypeCode.String;

        public string GetResourceName(int index) => index == 0 ? "Greeting" : "Farewell";

        public string GetString(int index) => index == 0 ? "Hello" : "Goodbye";

        public string? Lookup(string name)
        {
            return name switch
            {
                "Greeting" => "Hello",
                "Farewell" => "Goodbye",
                _ => null
            };
        }

        public void Dispose()
        {
        }
    }
}
