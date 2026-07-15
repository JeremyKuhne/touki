// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.Serialization.Formatters.Binary;
using Touki.Resources;

namespace touki.perf;

#pragma warning disable SYSLIB0011 // BinaryFormatter is used only to create trusted benchmark input.
#pragma warning disable CA2300 // Only trusted payloads generated during GlobalSetup are deserialized.
#pragma warning disable CA2301 // Only trusted payloads generated during GlobalSetup are deserialized.
#pragma warning disable CA2302 // Only trusted payloads generated during GlobalSetup are deserialized.

/// <summary>
///  Measures materialization of a class with enough fields to isolate reflection field-assignment cost.
/// </summary>
[MemoryDiagnoser]
public class BinaryFormattedObjectFieldAssignmentPerf
{
    private const int MaterializationBatchSize = 512;

    private System.IO.MemoryStream _stream = null!;
    private RegisteredTypeResolver _resolver = null!;
    private BinaryFormattedObject[] _materializationBatch = null!;

    [GlobalSetup]
    public void Setup()
    {
#if NET
        AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
#endif

        BinaryFormattedObjectWidePayload payload = BinaryFormattedObjectWidePayload.Create();
        byte[] payloadBytes;

        using (System.IO.MemoryStream serializationStream = new())
        {
            BinaryFormatter serializer = new();
            serializer.Serialize(serializationStream, payload);
            payloadBytes = serializationStream.ToArray();
        }

        _stream = new(payloadBytes, writable: false);
        _resolver = new RegisteredTypeResolver().Register<BinaryFormattedObjectWidePayload>();

        BinaryFormattedObjectWidePayload first = DeserializeForValidation();
        BinaryFormattedObjectWidePayload second = DeserializeForValidation();
        if (!first.IsValid() || !second.IsValid() || ReferenceEquals(first, second))
        {
            throw new InvalidOperationException("Wide-object deserialization validation failed.");
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _stream?.Dispose();

    [IterationSetup]
    public void SetupMaterializationBatch()
    {
        BinaryFormattedObject[] batch = new BinaryFormattedObject[MaterializationBatchSize];
        for (int index = 0; index < batch.Length; index++)
        {
            _stream.Position = 0;
            batch[index] = new BinaryFormattedObject(_stream, _resolver);
        }

        _materializationBatch = batch;
    }

    [IterationCleanup]
    public void CleanupMaterializationBatch() => _materializationBatch = null!;

    [Benchmark(Baseline = true, OperationsPerInvoke = MaterializationBatchSize)]
    public object DeserializeRecords()
    {
        object result = null!;
        foreach (BinaryFormattedObject formattedObject in _materializationBatch)
        {
            result = formattedObject.Deserialize();
        }

        return result;
    }

    private BinaryFormattedObjectWidePayload DeserializeForValidation()
    {
        _stream.Position = 0;
        BinaryFormattedObject formattedObject = new(_stream, _resolver);
        return (BinaryFormattedObjectWidePayload)formattedObject.Deserialize();
    }
}

[Serializable]
internal sealed class BinaryFormattedObjectWidePayload
{
    public int Field00;
    public int Field01;
    public int Field02;
    public int Field03;
    public int Field04;
    public int Field05;
    public int Field06;
    public int Field07;
    public int Field08;
    public int Field09;
    public int Field10;
    public int Field11;
    public int Field12;
    public int Field13;
    public int Field14;
    public int Field15;
    public int Field16;
    public int Field17;
    public int Field18;
    public int Field19;
    public int Field20;
    public int Field21;
    public int Field22;
    public int Field23;
    public int Field24;
    public int Field25;
    public int Field26;
    public int Field27;
    public int Field28;
    public int Field29;
    public int Field30;
    public int Field31;

    internal static BinaryFormattedObjectWidePayload Create()
        => new()
        {
            Field00 = 100,
            Field01 = 101,
            Field02 = 102,
            Field03 = 103,
            Field04 = 104,
            Field05 = 105,
            Field06 = 106,
            Field07 = 107,
            Field08 = 108,
            Field09 = 109,
            Field10 = 110,
            Field11 = 111,
            Field12 = 112,
            Field13 = 113,
            Field14 = 114,
            Field15 = 115,
            Field16 = 116,
            Field17 = 117,
            Field18 = 118,
            Field19 = 119,
            Field20 = 120,
            Field21 = 121,
            Field22 = 122,
            Field23 = 123,
            Field24 = 124,
            Field25 = 125,
            Field26 = 126,
            Field27 = 127,
            Field28 = 128,
            Field29 = 129,
            Field30 = 130,
            Field31 = 131
        };

    internal bool IsValid()
        => Field00 == 100
            && Field01 == 101
            && Field02 == 102
            && Field03 == 103
            && Field04 == 104
            && Field05 == 105
            && Field06 == 106
            && Field07 == 107
            && Field08 == 108
            && Field09 == 109
            && Field10 == 110
            && Field11 == 111
            && Field12 == 112
            && Field13 == 113
            && Field14 == 114
            && Field15 == 115
            && Field16 == 116
            && Field17 == 117
            && Field18 == 118
            && Field19 == 119
            && Field20 == 120
            && Field21 == 121
            && Field22 == 122
            && Field23 == 123
            && Field24 == 124
            && Field25 == 125
            && Field26 == 126
            && Field27 == 127
            && Field28 == 128
            && Field29 == 129
            && Field30 == 130
            && Field31 == 131;
}

#pragma warning restore CA2302
#pragma warning restore CA2301
#pragma warning restore CA2300
#pragma warning restore SYSLIB0011