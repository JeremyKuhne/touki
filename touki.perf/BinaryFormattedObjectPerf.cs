// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if BINARYFORMAT_UPSTREAM
extern alias BinaryFormatUpstream;

using UpstreamBinaryFormattedObject = BinaryFormatUpstream::BinaryFormat.BinaryFormattedObject;
#endif
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using BenchmarkDotNet.Configs;
using Touki.Resources;

namespace touki.perf;

#pragma warning disable SYSLIB0011 // BinaryFormatter is the intentional benchmark baseline.
#pragma warning disable SYSLIB0050 // Serialization infrastructure is required by the benchmark payload.
#pragma warning disable CA2300 // Only trusted payloads generated during GlobalSetup are deserialized.
#pragma warning disable CA2301 // Only trusted payloads generated during GlobalSetup are deserialized.
#pragma warning disable CA2302 // Only trusted payloads generated during GlobalSetup are deserialized.
#pragma warning disable CA5362 // Cycles and shared references are intentional benchmark inputs.

/// <summary>
///  Compares trusted NRBF deserialization through <see cref="BinaryFormatter"/> and
///  <see cref="BinaryFormattedObject"/> on .NET Framework 4.8.1 RyuJIT and modern .NET RyuJIT.
/// </summary>
/// <remarks>
///  Payload creation, serialization, type registration, and semantic validation happen outside the measured region.
///  Both end-to-end methods consume identical bytes from reusable streams. The parse-only row measures NRBF decoding
///  separately without comparing it to complete deserialization. On .NET 10, the materialization-only row deserializes
///  a bounded batch of independently parsed record graphs exactly once each. When <c>BINARYFORMAT_UPSTREAM</c> is
///  defined, the same operations also run against <c>JeremyKuhne/binaryformat</c> commit
///  <c>aaa1dd1bf7ee8ce626b82c3c55343dfee4a71743</c>.
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BinaryFormattedObjectPerf
{
#if BINARYFORMAT_UPSTREAM
    private const string BinaryFormatUpstreamCommit = "aaa1dd1bf7ee8ce626b82c3c55343dfee4a71743";
#endif
#if NET10_0
    private const int MaterializationBatchSize = 64;
#endif
    private const int PrimitiveArrayLength = 1024;
    private const int StringListCount = 128;
    private const int TreeDepth = 7;
    private const int GraphNodeCount = 128;
    private const int SerializableValueCount = 256;

    private BinaryFormatter _binaryFormatter = null!;
    private System.IO.MemoryStream _binaryFormatterStream = null!;
    private System.IO.MemoryStream _binaryFormattedObjectStream = null!;
#if BINARYFORMAT_UPSTREAM
    private System.IO.MemoryStream _upstreamBinaryFormattedObjectStream = null!;
#endif
#if NET10_0
    private BinaryFormattedObject[] _materializationBatch = null!;
#endif
    private RegisteredTypeResolver _resolver = null!;

    [Params(
        "Int32Array_1K",
        "StringList_128",
        "CustomObject",
        "ObjectTree_127",
        "SharedCycle_128",
        "SerializableCallback")]
    public string Scenario { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
#if NET
        AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
#endif
#if BINARYFORMAT_UPSTREAM
        System.Reflection.Assembly upstreamAssembly = typeof(UpstreamBinaryFormattedObject).Assembly;
        System.Reflection.AssemblyInformationalVersionAttribute? upstreamVersionAttribute =
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<
                System.Reflection.AssemblyInformationalVersionAttribute>(upstreamAssembly);
        System.Reflection.AssemblyConfigurationAttribute? upstreamConfigurationAttribute =
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<
                System.Reflection.AssemblyConfigurationAttribute>(upstreamAssembly);
        string? upstreamVersion = upstreamVersionAttribute?.InformationalVersion;
        if (upstreamVersion is null
            || !upstreamVersion.EndsWith($"+{BinaryFormatUpstreamCommit}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The upstream BinaryFormat assembly must be built from commit '{BinaryFormatUpstreamCommit}'.");
        }

        if (upstreamConfigurationAttribute?.Configuration != "Release")
        {
            throw new InvalidOperationException("The upstream BinaryFormat assembly must be built in Release.");
        }
#endif

        object payload = CreatePayload();
        byte[] payloadBytes;

        using (System.IO.MemoryStream serializationStream = new())
        {
            BinaryFormatter serializer = new();
            serializer.Serialize(serializationStream, payload);
            payloadBytes = serializationStream.ToArray();
        }

        _binaryFormatter = new();
        _binaryFormatterStream = new(payloadBytes, writable: false);
        _binaryFormattedObjectStream = new(payloadBytes, writable: false);
#if BINARYFORMAT_UPSTREAM
        _upstreamBinaryFormattedObjectStream = new(payloadBytes, writable: false);
#endif
        _resolver = CreateResolver();

        ValidateRepeatedDeserialization(
            DeserializeWithBinaryFormatterForValidation(),
            DeserializeWithBinaryFormatterForValidation());
        ValidateRepeatedDeserialization(
            DeserializeWithBinaryFormattedObjectForValidation(),
            DeserializeWithBinaryFormattedObjectForValidation());
#if BINARYFORMAT_UPSTREAM
        ValidateRepeatedDeserialization(
            DeserializeWithUpstreamBinaryFormattedObjectForValidation(),
            DeserializeWithUpstreamBinaryFormattedObjectForValidation());
#endif
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _binaryFormatterStream?.Dispose();
        _binaryFormattedObjectStream?.Dispose();
#if BINARYFORMAT_UPSTREAM
        _upstreamBinaryFormattedObjectStream?.Dispose();
#endif
    }

    private object DeserializeWithBinaryFormatterForValidation()
    {
        _binaryFormatterStream.Position = 0;
        return _binaryFormatter.Deserialize(_binaryFormatterStream);
    }

    private object DeserializeWithBinaryFormattedObjectForValidation()
    {
        _binaryFormattedObjectStream.Position = 0;
        BinaryFormattedObject formattedObject = new(_binaryFormattedObjectStream, _resolver);
        return formattedObject.Deserialize();
    }

#if BINARYFORMAT_UPSTREAM
    private object DeserializeWithUpstreamBinaryFormattedObjectForValidation()
    {
        _upstreamBinaryFormattedObjectStream.Position = 0;
        UpstreamBinaryFormattedObject formattedObject = new(_upstreamBinaryFormattedObjectStream);
        return formattedObject.Deserialize();
    }
#endif

    [BenchmarkCategory("EndToEnd"), Benchmark(Baseline = true)]
    public object BinaryFormatter_Deserialize()
    {
        _binaryFormatterStream.Position = 0;
        return _binaryFormatter.Deserialize(_binaryFormatterStream);
    }

    [BenchmarkCategory("EndToEnd"), Benchmark]
    public object BinaryFormattedObject_ParseAndDeserialize()
    {
        _binaryFormattedObjectStream.Position = 0;
        BinaryFormattedObject formattedObject = new(_binaryFormattedObjectStream, _resolver);
        return formattedObject.Deserialize();
    }

#if BINARYFORMAT_UPSTREAM
    [BenchmarkCategory("EndToEnd"), Benchmark]
    public object BinaryFormatAaa1dd1_ParseAndDeserialize()
    {
        _upstreamBinaryFormattedObjectStream.Position = 0;
        UpstreamBinaryFormattedObject formattedObject = new(_upstreamBinaryFormattedObjectStream);
        return formattedObject.Deserialize();
    }
#endif

    [BenchmarkCategory("ParseOnly"), Benchmark(Baseline = true)]
    public BinaryFormattedObject BinaryFormattedObject_Parse()
    {
        _binaryFormattedObjectStream.Position = 0;
        return new BinaryFormattedObject(_binaryFormattedObjectStream, _resolver);
    }

#if BINARYFORMAT_UPSTREAM
    [BenchmarkCategory("ParseOnly"), Benchmark]
    public UpstreamBinaryFormattedObject BinaryFormatAaa1dd1_Parse()
    {
        _upstreamBinaryFormattedObjectStream.Position = 0;
        return new UpstreamBinaryFormattedObject(_upstreamBinaryFormattedObjectStream);
    }
#endif

#if NET10_0
    [IterationSetup(Target = nameof(BinaryFormattedObject_DeserializeRecords))]
    public void SetupMaterializationBatch()
    {
        BinaryFormattedObject[] batch = new BinaryFormattedObject[MaterializationBatchSize];
        for (int index = 0; index < batch.Length; index++)
        {
            _binaryFormattedObjectStream.Position = 0;
            batch[index] = new BinaryFormattedObject(_binaryFormattedObjectStream, _resolver);
        }

        _materializationBatch = batch;
    }

    [IterationCleanup(Target = nameof(BinaryFormattedObject_DeserializeRecords))]
    public void CleanupMaterializationBatch() => _materializationBatch = null!;

    [BenchmarkCategory("MaterializeOnly"), Benchmark(Baseline = true, OperationsPerInvoke = MaterializationBatchSize)]
    public object BinaryFormattedObject_DeserializeRecords()
    {
        object result = null!;
        foreach (BinaryFormattedObject formattedObject in _materializationBatch)
        {
            result = formattedObject.Deserialize();
        }

        return result;
    }
#endif

    private void ValidateRepeatedDeserialization(object first, object second)
    {
        ValidateResult(first);
        ValidateResult(second);

        if (!AreResultsIndependent(first, second))
        {
            throw new InvalidOperationException(
                $"Repeated deserialization for scenario '{Scenario}' reused mutable result state.");
        }
    }

    private object CreatePayload()
        => Scenario switch
        {
            "Int32Array_1K" => CreateInt32Array(PrimitiveArrayLength),
            "StringList_128" => CreateStringList(),
            "CustomObject" => CreateCustomObject(),
            "ObjectTree_127" => CreateObjectTree(),
            "SharedCycle_128" => CreateSharedCycle(),
            "SerializableCallback" => new BinaryFormattedObjectSerializablePayload(
                "serialization-info",
                CreateInt32Array(SerializableValueCount)),
            _ => throw new InvalidOperationException($"Unknown benchmark scenario '{Scenario}'.")
        };

    private RegisteredTypeResolver CreateResolver()
    {
        RegisteredTypeResolver resolver = new();

        switch (Scenario)
        {
            case "CustomObject":
                resolver.Register<BinaryFormattedObjectCustomPayload>();
                break;
            case "ObjectTree_127":
                resolver.Register<BinaryFormattedObjectTreeNode>();
                break;
            case "SharedCycle_128":
                resolver.Register<BinaryFormattedObjectGraph>();
                resolver.Register<BinaryFormattedObjectGraphNode>();
                resolver.Register<BinaryFormattedObjectGraphNode[]>();
                break;
            case "SerializableCallback":
                resolver.Register<BinaryFormattedObjectSerializablePayload>();
                break;
        }

        return resolver;
    }

    private static int[] CreateInt32Array(int length)
    {
        int[] values = new int[length];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = (index * 3) - 7;
        }

        return values;
    }

    private static List<string> CreateStringList()
    {
        List<string> values = new(StringListCount);
        for (int index = 0; index < StringListCount; index++)
        {
            values.Add($"NRBF benchmark value {index}");
        }

        return values;
    }

    private static BinaryFormattedObjectCustomPayload CreateCustomObject()
        => new()
        {
            Id = 42,
            Name = "representative custom payload",
            CreatedUtc = new DateTime(2026, 7, 11, 12, 34, 56, DateTimeKind.Utc),
            Values = CreateInt32Array(64)
        };

    private static BinaryFormattedObjectTreeNode CreateObjectTree()
    {
        int nextValue = 0;
        return CreateObjectTree(TreeDepth, ref nextValue)!;
    }

    private static BinaryFormattedObjectTreeNode? CreateObjectTree(int depth, ref int nextValue)
    {
        if (depth == 0)
        {
            return null;
        }

        int value = nextValue++;
        return new BinaryFormattedObjectTreeNode
        {
            Value = value,
            Name = $"node {value}",
            Left = CreateObjectTree(depth - 1, ref nextValue),
            Right = CreateObjectTree(depth - 1, ref nextValue)
        };
    }

    private static BinaryFormattedObjectGraph CreateSharedCycle()
    {
        BinaryFormattedObjectGraphNode[] nodes = new BinaryFormattedObjectGraphNode[GraphNodeCount];
        for (int index = 0; index < nodes.Length; index++)
        {
            nodes[index] = new BinaryFormattedObjectGraphNode { Value = index };
        }

        for (int index = 0; index < nodes.Length; index++)
        {
            nodes[index].Next = nodes[(index + 1) % nodes.Length];
            nodes[index].Shared = nodes[(index * 17) % nodes.Length];
        }

        return new BinaryFormattedObjectGraph
        {
            Entry = nodes[0],
            Nodes = nodes
        };
    }

    private void ValidateResult(object result)
    {
        bool isValid = Scenario switch
        {
            "Int32Array_1K" => IsValidInt32Array(result),
            "StringList_128" => IsValidStringList(result),
            "CustomObject" => IsValidCustomObject(result),
            "ObjectTree_127" => IsValidObjectTree(result),
            "SharedCycle_128" => IsValidSharedCycle(result),
            "SerializableCallback" => IsValidSerializableCallback(result),
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException($"Deserialized result for scenario '{Scenario}' is invalid.");
        }
    }

    private static bool IsValidInt32Array(object result)
    {
        if (result is not int[] values || values.Length != PrimitiveArrayLength)
        {
            return false;
        }

        for (int index = 0; index < values.Length; index++)
        {
            if (values[index] != (index * 3) - 7)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidStringList(object result)
    {
        if (result is not List<string> values || values.Count != StringListCount)
        {
            return false;
        }

        for (int index = 0; index < values.Count; index++)
        {
            if (values[index] != $"NRBF benchmark value {index}")
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidCustomObject(object result)
    {
        if (result is not BinaryFormattedObjectCustomPayload payload
            || payload.Id != 42
            || payload.Name != "representative custom payload"
            || payload.CreatedUtc != new DateTime(2026, 7, 11, 12, 34, 56, DateTimeKind.Utc)
            || payload.CreatedUtc.Kind != DateTimeKind.Utc
            || payload.Values.Length != 64)
        {
            return false;
        }

        for (int index = 0; index < payload.Values.Length; index++)
        {
            if (payload.Values[index] != (index * 3) - 7)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidObjectTree(object result)
    {
        if (result is not BinaryFormattedObjectTreeNode root)
        {
            return false;
        }

        int expectedValue = 0;
        return IsValidObjectTree(root, TreeDepth, ref expectedValue) && expectedValue == 127;
    }

    private static bool IsValidObjectTree(
        BinaryFormattedObjectTreeNode? node,
        int depth,
        ref int expectedValue)
    {
        if (depth == 0)
        {
            return node is null;
        }

        if (node is null || node.Value != expectedValue || node.Name != $"node {expectedValue}")
        {
            return false;
        }

        expectedValue++;
        return IsValidObjectTree(node.Left, depth - 1, ref expectedValue)
            && IsValidObjectTree(node.Right, depth - 1, ref expectedValue);
    }

    private static bool IsValidSharedCycle(object result)
    {
        if (result is not BinaryFormattedObjectGraph graph
            || graph.Nodes.Length != GraphNodeCount
            || !ReferenceEquals(graph.Entry, graph.Nodes[0])
            || !ReferenceEquals(graph.Nodes[^1].Next, graph.Entry))
        {
            return false;
        }

        for (int index = 0; index < graph.Nodes.Length; index++)
        {
            BinaryFormattedObjectGraphNode node = graph.Nodes[index];
            if (node.Value != index
                || !ReferenceEquals(node.Next, graph.Nodes[(index + 1) % graph.Nodes.Length])
                || !ReferenceEquals(node.Shared, graph.Nodes[(index * 17) % graph.Nodes.Length]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidSerializableCallback(object result)
    {
        if (result is not BinaryFormattedObjectSerializablePayload payload
            || payload.Name != "serialization-info"
            || payload.Values.Length != SerializableValueCount
            || payload.SerializationConstructorCallCount != 1
            || payload.CallbackCallCount != 1
            || payload.Checksum != 96128)
        {
            return false;
        }

        for (int index = 0; index < payload.Values.Length; index++)
        {
            if (payload.Values[index] != (index * 3) - 7)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreResultsIndependent(object first, object second)
        => Scenario switch
        {
            "Int32Array_1K" => !ReferenceEquals(first, second),
            "StringList_128" => first is List<string> firstValues
                && second is List<string> secondValues
                && AreStringListsIndependent(firstValues, secondValues),
            "CustomObject" => first is BinaryFormattedObjectCustomPayload firstPayload
                && second is BinaryFormattedObjectCustomPayload secondPayload
                && !ReferenceEquals(firstPayload, secondPayload)
                && !ReferenceEquals(firstPayload.Values, secondPayload.Values),
            "ObjectTree_127" => first is BinaryFormattedObjectTreeNode firstRoot
                && second is BinaryFormattedObjectTreeNode secondRoot
                && AreObjectTreesIndependent(firstRoot, secondRoot),
            "SharedCycle_128" => first is BinaryFormattedObjectGraph firstGraph
                && second is BinaryFormattedObjectGraph secondGraph
                && AreGraphsIndependent(firstGraph, secondGraph),
            "SerializableCallback" => first is BinaryFormattedObjectSerializablePayload firstPayload
                && second is BinaryFormattedObjectSerializablePayload secondPayload
                && !ReferenceEquals(firstPayload, secondPayload)
                && !ReferenceEquals(firstPayload.Values, secondPayload.Values),
            _ => false
        };

    private static bool AreStringListsIndependent(List<string> first, List<string> second)
    {
        if (ReferenceEquals(first, second))
        {
            return false;
        }

        string secondFirstValue = second[0];
        first[0] = "mutated validation value";
        return second[0] == secondFirstValue;
    }

    private static bool AreObjectTreesIndependent(
        BinaryFormattedObjectTreeNode? first,
        BinaryFormattedObjectTreeNode? second)
    {
        if (first is null || second is null)
        {
            return first is null && second is null;
        }

        return !ReferenceEquals(first, second)
            && AreObjectTreesIndependent(first.Left, second.Left)
            && AreObjectTreesIndependent(first.Right, second.Right);
    }

    private static bool AreGraphsIndependent(
        BinaryFormattedObjectGraph first,
        BinaryFormattedObjectGraph second)
    {
        if (ReferenceEquals(first, second)
            || ReferenceEquals(first.Entry, second.Entry)
            || ReferenceEquals(first.Nodes, second.Nodes)
            || first.Nodes.Length != second.Nodes.Length)
        {
            return false;
        }

        for (int index = 0; index < first.Nodes.Length; index++)
        {
            if (ReferenceEquals(first.Nodes[index], second.Nodes[index]))
            {
                return false;
            }
        }

        return true;
    }
}

[Serializable]
internal sealed class BinaryFormattedObjectCustomPayload
{
    public int Id;
    public string Name = string.Empty;
    public DateTime CreatedUtc;
    public int[] Values = [];
}

[Serializable]
internal sealed class BinaryFormattedObjectTreeNode
{
    public int Value;
    public string Name = string.Empty;
    public BinaryFormattedObjectTreeNode? Left;
    public BinaryFormattedObjectTreeNode? Right;
}

[Serializable]
internal sealed class BinaryFormattedObjectGraph
{
    public BinaryFormattedObjectGraphNode Entry = null!;
    public BinaryFormattedObjectGraphNode[] Nodes = [];
}

[Serializable]
internal sealed class BinaryFormattedObjectGraphNode
{
    public int Value;
    public BinaryFormattedObjectGraphNode? Next;
    public BinaryFormattedObjectGraphNode? Shared;
}

[Serializable]
internal sealed class BinaryFormattedObjectSerializablePayload : ISerializable
{
    internal BinaryFormattedObjectSerializablePayload(string name, int[] values)
    {
        Name = name;
        Values = values;
    }

    private BinaryFormattedObjectSerializablePayload(SerializationInfo info, StreamingContext context)
    {
        Name = info.GetString(nameof(Name)) ?? string.Empty;
        Values = (int[]?)info.GetValue(nameof(Values), typeof(int[])) ?? [];
        SerializationConstructorCallCount++;
    }

    public string Name;
    public int[] Values;

    [NonSerialized]
    public int SerializationConstructorCallCount;

    [NonSerialized]
    public int CallbackCallCount;

    [NonSerialized]
    public int Checksum;

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(nameof(Name), Name);
        info.AddValue(nameof(Values), Values, typeof(int[]));
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        int checksum = 0;
        foreach (int value in Values)
        {
            checksum += value;
        }

        Checksum = checksum;
        CallbackCallCount++;
    }
}

#pragma warning restore CA5362
#pragma warning restore CA2302
#pragma warning restore CA2301
#pragma warning restore CA2300
#pragma warning restore SYSLIB0050
#pragma warning restore SYSLIB0011