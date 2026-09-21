// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Compares mapped and seekable file backings for first indexed string lookup on .NET Framework
///  4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class StringResourceFileBackingPerf
{
    [AllowNull]
    private string _path;

    [AllowNull]
    private string _lookupKey;

    /// <summary>
    ///  The number of strings in the resource table.
    /// </summary>
    [Params(1, 16, 256)]
    public int ResourceCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Join(Path.GetTempPath(), $"{nameof(StringResourceFileBackingPerf)}-{Guid.NewGuid():N}.resources");
        using ResourceWriter writer = new(_path);
        for (int i = 0; i < ResourceCount; i++)
        {
            writer.AddResource($"resource_{i:D4}", $"value_{i:D4}");
        }

        writer.Generate();
        _lookupKey = $"resource_{ResourceCount / 2:D4}";
    }

    [GlobalCleanup]
    public void Cleanup() => System.IO.File.Delete(_path);

    [Benchmark(Baseline = true)]
    public int MappedFile_FirstLookup()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            RawResourceReader.CreateFromFile(_path),
            StringResourceManagerOptions.None);
        StringResourceLookupKind result = table.Lookup(_lookupKey, out string? value);
        return result == StringResourceLookupKind.Found && value is not null ? value.Length : 0;
    }

    [Benchmark]
    public int FileStream_FirstLookup()
    {
        System.IO.FileStream stream = new(
            _path,
            System.IO.FileMode.Open,
            System.IO.FileAccess.Read,
            System.IO.FileShare.Read);

        using IndexedStringResourceTable table = StringResourceTableLoader.LoadIndexedTableFromResourcesStream(
            stream,
            StringResourceManagerOptions.None);
        StringResourceLookupKind result = table.Lookup(_lookupKey, out string? value);
        return result == StringResourceLookupKind.Found && value is not null ? value.Length : 0;
    }
}