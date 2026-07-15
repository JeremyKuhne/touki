// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;
using System.Runtime.Serialization;

namespace touki.perf;

#pragma warning disable SYSLIB0050 // Serialization infrastructure is intentionally benchmarked.

/// <summary>
///  Compares portable reflection-based ways to assign all serializable fields of an object.
/// </summary>
[MemoryDiagnoser]
public class ClassFieldAssignmentPerf
{
    private MemberInfo[] _members = null!;
    private object?[] _values = null!;

    [GlobalSetup]
    public void Setup()
    {
        BinaryFormattedObjectWidePayload payload = BinaryFormattedObjectWidePayload.Create();
        _members = FormatterServices.GetSerializableMembers(typeof(BinaryFormattedObjectWidePayload));
        _values = FormatterServices.GetObjectData(payload, _members);

        if (FieldInfo_SetValue() is not BinaryFormattedObjectWidePayload direct || !direct.IsValid()
            || FormatterServices_PopulateObjectMembers() is not BinaryFormattedObjectWidePayload batch
            || !batch.IsValid()
            || FormatterServices_WithPerObjectValuesArray() is not BinaryFormattedObjectWidePayload realisticBatch
            || !realisticBatch.IsValid())
        {
            throw new InvalidOperationException("Field-assignment benchmark validation failed.");
        }
    }

    [Benchmark(Baseline = true)]
    public object FieldInfo_SetValue()
    {
        object instance = CreateUninitializedObject();
        for (int index = 0; index < _members.Length; index++)
        {
            ((FieldInfo)_members[index]).SetValue(instance, _values[index]);
        }

        return instance;
    }

    [Benchmark]
    public object FormatterServices_PopulateObjectMembers()
    {
        object instance = CreateUninitializedObject();
        return FormatterServices.PopulateObjectMembers(instance, _members, _values);
    }

    [Benchmark]
    public object FormatterServices_WithPerObjectValuesArray()
    {
        object?[] values = new object?[_values.Length];
        Array.Copy(_values, values, values.Length);

        object instance = CreateUninitializedObject();
        return FormatterServices.PopulateObjectMembers(instance, _members, values);
    }

    private static object CreateUninitializedObject()
        =>
#if NET
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(BinaryFormattedObjectWidePayload));
#else
            FormatterServices.GetUninitializedObject(typeof(BinaryFormattedObjectWidePayload));
#endif
}

#pragma warning restore SYSLIB0050