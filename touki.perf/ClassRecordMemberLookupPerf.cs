// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Formats.Nrbf;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Touki.Resources;

namespace touki.perf;

#pragma warning disable SYSLIB0011 // BinaryFormatter is used only to create trusted benchmark input.
#pragma warning disable SYSLIB0050 // Serialization infrastructure is intentionally benchmarked.
#pragma warning disable CA2300 // Only trusted payloads generated during GlobalSetup are deserialized.
#pragma warning disable CA2301 // Only trusted payloads generated during GlobalSetup are deserialized.
#pragma warning disable CA2302 // Only trusted payloads generated during GlobalSetup are deserialized.

/// <summary>
///  Measures the duplicate member-name lookup required by the public <see cref="ClassRecord"/> API.
/// </summary>
[MemoryDiagnoser]
public class ClassRecordMemberLookupPerf
{
    private const int ExpectedChecksum = 3696;

    private ClassRecord _classRecord = null!;
    private string[] _memberNames = null!;

    [GlobalSetup]
    public void Setup()
    {
#if NET
        AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", true);
#endif

        byte[] payloadBytes;
        using (System.IO.MemoryStream serializationStream = new())
        {
            BinaryFormatter serializer = new();
            serializer.Serialize(serializationStream, BinaryFormattedObjectWidePayload.Create());
            payloadBytes = serializationStream.ToArray();
        }

        using System.IO.MemoryStream stream = new(payloadBytes, writable: false);
        BinaryFormattedObject formattedObject = new(stream);
        _classRecord = (ClassRecord)formattedObject.RootRecord;

        MemberInfo[] members = FormatterServices.GetSerializableMembers(typeof(BinaryFormattedObjectWidePayload));
        _memberNames = new string[members.Length];
        for (int index = 0; index < members.Length; index++)
        {
            _memberNames[index] = members[index].Name;
        }

        if (HasMember_ThenGetRawValue() != ExpectedChecksum
            || GetRawValue_KnownPresent() != ExpectedChecksum)
        {
            throw new InvalidOperationException("Class-record member lookup validation failed.");
        }
    }

    [Benchmark(Baseline = true)]
    public int HasMember_ThenGetRawValue()
    {
        int checksum = 0;
        foreach (string memberName in _memberNames)
        {
            if (_classRecord.HasMember(memberName))
            {
                checksum += (int)_classRecord.GetRawValue(memberName)!;
            }
        }

        return checksum;
    }

    [Benchmark]
    public int GetRawValue_KnownPresent()
    {
        int checksum = 0;
        foreach (string memberName in _memberNames)
        {
            checksum += (int)_classRecord.GetRawValue(memberName)!;
        }

        return checksum;
    }
}

#pragma warning restore CA2302
#pragma warning restore CA2301
#pragma warning restore CA2300
#pragma warning restore SYSLIB0050
#pragma warning restore SYSLIB0011