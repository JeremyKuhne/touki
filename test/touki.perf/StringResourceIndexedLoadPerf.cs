// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Resources;
using Touki.Resources;

namespace touki.perf;

/// <summary>
///  Compares eager materialization with indexed first lookup on .NET Framework 4.8.1 RyuJIT and
///  modern .NET RyuJIT.
/// </summary>
[MemoryDiagnoser]
public class StringResourceIndexedLoadPerf
{
    /// <summary>
    ///  Representative resource table shapes.
    /// </summary>
    public enum ResourceShape
    {
        /// <summary>
        ///  Short names and values in a string-only table.
        /// </summary>
        Typical,

        /// <summary>
        ///  Names padded by 128 characters and 256-character values.
        /// </summary>
        LongNameAndValue,

        /// <summary>
        ///  Short strings plus one ignored non-string resource.
        /// </summary>
        Mixed
    }

    [AllowNull]
    private byte[] _resources;

    [AllowNull]
    private string _lookupKey;

    private StringResourceManagerOptions _options;

    /// <summary>
    ///  The number of strings in the resource table.
    /// </summary>
    [Params(1, 256, 1024)]
    public int ResourceCount { get; set; }

    /// <summary>
    ///  The shape of the resource table.
    /// </summary>
    [Params(ResourceShape.Typical, ResourceShape.LongNameAndValue, ResourceShape.Mixed)]
    public ResourceShape Shape { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        using System.IO.MemoryStream stream = new();
        using (ResourceWriter writer = new(stream))
        {
            (int NamePadding, int ValueLength, bool IncludeNonString) shape = Shape switch
            {
                ResourceShape.Typical => (0, 8, false),
                ResourceShape.LongNameAndValue => (128, 256, false),
                ResourceShape.Mixed => (0, 8, true),
                _ => throw new InvalidOperationException("The resource shape is invalid.")
            };

            string prefix = new('n', shape.NamePadding);
            string value = new('v', shape.ValueLength);
            for (int i = 0; i < ResourceCount; i++)
            {
                writer.AddResource($"{prefix}resource_{i:D4}", value);
            }

            if (shape.IncludeNonString)
            {
                writer.AddResource("non_string", 42);
            }

            writer.Generate();
            _lookupKey = $"{prefix}resource_{ResourceCount / 2:D4}";
        }

        _resources = stream.ToArray();
        _options = Shape == ResourceShape.Mixed
            ? StringResourceManagerOptions.IgnoreNonStringResources
            : StringResourceManagerOptions.None;
    }

    [Benchmark(Baseline = true)]
    public int EagerTable_LoadLookup()
    {
        StringResourceTable table = StringResourceTableLoader.LoadTableFromResourcesFile(
            _resources,
            _options);
        return table.Strings.TryGetValue(_lookupKey, out string? value) ? value.Length : 0;
    }

    [Benchmark]
    public int Indexed_StrictScanLookup()
    {
        using IndexedStringResourceTable table = IndexedStringResourceTable.Create(
            new RawResourceReader(_resources),
            _options);
        StringResourceLookupKind result = table.Lookup(_lookupKey, out string? value);
        return result == StringResourceLookupKind.Found && value is not null ? value.Length : 0;
    }
}