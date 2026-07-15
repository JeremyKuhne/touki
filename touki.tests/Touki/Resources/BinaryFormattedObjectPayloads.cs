// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Runtime.Serialization;

namespace Touki.Resources;

#pragma warning disable CA5362 // The cycle is intentional test data for graph deserialization.

[Serializable]
internal sealed class RegisteredPayload
{
#pragma warning disable CS0649 // Fields are populated by BinaryFormattedObject.
    public string Name = string.Empty;
    public int Number;
    public RegisteredPayload? Next;
#pragma warning restore CS0649
}

#pragma warning restore CA5362

[Serializable]
internal sealed class CallbackPayload : IDeserializationCallback
{
    public string Value = string.Empty;

    [NonSerialized]
    public bool OnDeserializingCalled;

    [NonSerialized]
    public bool OnDeserializedCalled;

    [NonSerialized]
    public bool CallbackCalled;

    internal static int InvocationCount { get; set; }

    [OnDeserializing]
    private void OnDeserializing(StreamingContext context)
    {
        OnDeserializingCalled = true;
        InvocationCount++;
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        OnDeserializedCalled = true;
        InvocationCount++;
    }

    public void OnDeserialization(object? sender)
    {
        CallbackCalled = true;
        InvocationCount++;
    }
}

#pragma warning disable SYSLIB0050 // Type or member is obsolete.

[Serializable]
internal sealed class SerializablePayload : ISerializable
{
    private SerializablePayload(SerializationInfo info, StreamingContext context)
    {
        Value = info.GetString("Value") ?? string.Empty;
        ConstructorCalled = true;
    }

    public string Value = string.Empty;

    [NonSerialized]
    public bool ConstructorCalled;

    public void GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue("Value", Value);
}

[Serializable]
internal sealed class SharedReferencePayload
{
#pragma warning disable CS0649 // Fields are populated by BinaryFormattedObject.
    public RegisteredPayload First = null!;
    public RegisteredPayload Second = null!;
#pragma warning restore CS0649
}

#pragma warning disable CA5362 // Cycles are intentional test data for graph deserialization.

[Serializable]
internal sealed class SerializableCycle : ISerializable
{
    private SerializableCycle(SerializationInfo info, StreamingContext context)
    {
        Value = info.GetInt32("Value");
        Next = (SerializableCycle?)info.GetValue("Next", typeof(SerializableCycle));
    }

    public int Value;
    public SerializableCycle? Next;

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("Value", Value);
        info.AddValue("Next", Next);
    }
}

[Serializable]
internal sealed class NodeWithNodeStruct
{
    public string Value = string.Empty;
#pragma warning disable CS0649 // Field is populated by BinaryFormattedObject.
    public NodeStruct NodeStruct;
#pragma warning restore CS0649
}

[Serializable]
internal struct NodeStruct : ISerializable
{
    private NodeStruct(SerializationInfo info, StreamingContext context)
    {
        Node = (NodeWithNodeStruct?)info.GetValue("Node", typeof(NodeWithNodeStruct));
    }

    public NodeWithNodeStruct? Node;

    public readonly void GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue("Node", Node, typeof(NodeWithNodeStruct));
}

[Serializable]
internal struct ArrayBackReference : ISerializable
{
    private ArrayBackReference(SerializationInfo info, StreamingContext context)
    {
        Value = info.GetInt32("Value");
        Array = (ArrayBackReference[]?)info.GetValue("Array", typeof(ArrayBackReference[]));
    }

    public int Value;
    public ArrayBackReference[]? Array;

    public readonly void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("Value", Value);
        info.AddValue("Array", Array);
    }
}

[Serializable]
internal sealed class ObjectReferenceSingleton : IObjectReference
{
    private ObjectReferenceSingleton()
    {
    }

    internal static ObjectReferenceSingleton Value { get; } = new();

    public object GetRealObject(StreamingContext context) => Value;
}

#pragma warning restore CA5362

[Serializable]
internal sealed class NullableSerializablePayload : ISerializable
{
    private NullableSerializablePayload(SerializationInfo info, StreamingContext context)
    {
        Value = (int?)info.GetValue("Value", typeof(int?));
    }

    public int? Value;

    public void GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue("Value", Value, typeof(int?));
}

[Serializable]
internal sealed class CallbackStructContainer
{
#pragma warning disable CS0649 // Field is populated by BinaryFormattedObject.
    public CallbackStruct Value;
#pragma warning restore CS0649
}

[Serializable]
internal struct CallbackStruct
{
    public int Value;

    [NonSerialized]
    public bool CallbackCalled;

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        Value += 100;
        CallbackCalled = true;
    }
}

[Serializable]
internal sealed class FanOutPayload
{
    public FanOutOwner First = null!;
    public FanOutOwner Second = null!;
}

[Serializable]
internal sealed class FanOutOwner
{
    public object Value = null!;

    [NonSerialized]
    public bool CallbackCalled;

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
        => CallbackCalled = true;
}

[Serializable]
internal struct FanOutValue : ISerializable
{
    private FanOutValue(SerializationInfo info, StreamingContext context)
    {
        Value = info.GetInt32("Value");
    }

    public int Value;

    public readonly void GetObjectData(SerializationInfo info, StreamingContext context)
        => info.AddValue("Value", Value);
}

[Serializable]
internal sealed class ConvergingCallbackContainer
{
#pragma warning disable CS0649 // Field is populated by BinaryFormattedObject.
    public ConvergingCallbackStruct Value;
#pragma warning restore CS0649
}

[Serializable]
internal struct ConvergingCallbackStruct
{
#pragma warning disable CS0649 // Fields are populated by BinaryFormattedObject.
    public object First;
    public object Second;
#pragma warning restore CS0649
}

#pragma warning restore SYSLIB0050 // Type or member is obsolete.

[Serializable]
internal struct SelfReferencingStruct
{
#pragma warning disable CS0649 // Field is populated by BinaryFormattedObject.
    public object? Value;
#pragma warning restore CS0649
}