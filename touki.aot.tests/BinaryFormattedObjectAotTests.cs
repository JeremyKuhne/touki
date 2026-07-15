// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Runtime.Serialization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Touki.Resources;

[TestClass]
public class BinaryFormattedObjectAotTests
{
    private const string RegisteredPayloadData = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAMAAAAETmFtZQZO
        dW1iZXIETmV4dAEABAghVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXlsb2FkAgAAAAIAAAAGAwAAAARyb290KgAAAAoL
        """;

    private const string ListInt32Data = """
        AAEAAAD/////AQAAAAAAAAAEAQAAAH5TeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5MaXN0YDFbW1N5c3RlbS5JbnQzMiwg
        bXNjb3JsaWIsIFZlcnNpb249NC4wLjAuMCwgQ3VsdHVyZT1uZXV0cmFsLCBQdWJsaWNLZXlUb2tlbj1iNzdhNWM1NjE5MzRl
        MDg5XV0DAAAABl9pdGVtcwVfc2l6ZQhfdmVyc2lvbgcAAAgICAkCAAAABQAAAAUAAAAPAgAAAAgAAAAIAQAAAAIAAAADAAAA
        BQAAAAgAAAAAAAAAAAAAAAAAAAAL
        """;

    private const string RegisteredPayloadArrayData = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAcBAAAAAAEAAAACAAAABCFUb3VraS5SZXNvdXJjZXMuUmVnaXN0ZXJlZFBheWxvYWQC
        AAAACQMAAAAJBAAAAAUDAAAAIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAMAAAAETmFtZQZOdW1iZXIETmV4
        dAEABAghVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXlsb2FkAgAAAAIAAAAGBQAAAAVmaXJzdAEAAAAKAQQAAAADAAAA
        BgYAAAAGc2Vjb25kAgAAAAoL
        """;

    private const string RegisteredPayloadMatrixData = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAcBAAAAAgIAAAABAAAAAgAAAAQhVG91a2kuUmVzb3VyY2VzLlJlZ2lzdGVyZWRQYXls
        b2FkAgAAAAkDAAAACQQAAAAFAwAAACFUb3VraS5SZXNvdXJjZXMuUmVnaXN0ZXJlZFBheWxvYWQDAAAABE5hbWUGTnVtYmVy
        BE5leHQBAAQIIVRvdWtpLlJlc291cmNlcy5SZWdpc3RlcmVkUGF5bG9hZAIAAAACAAAABgUAAAAEbGVmdAEAAAAKAQQAAAAD
        AAAABgYAAAAFcmlnaHQCAAAACgs=
        """;

    private const string CallbackPayloadData = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAH1RvdWtpLlJlc291cmNlcy5DYWxsYmFja1BheWxvYWQBAAAABVZhbHVlAQIA
        AAAGAwAAAAhjYWxsYmFjaws=
        """;

    private const string SerializablePayloadData = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAI1RvdWtpLlJlc291cmNlcy5TZXJpYWxpemFibGVQYXlsb2FkAQAAAAVWYWx1
        ZQECAAAABgMAAAASc2VyaWFsaXphdGlvbi1pbmZvCw==
        """;

    private const string ObjectReferenceSingletonData = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAKFRvdWtpLlJlc291cmNlcy5PYmplY3RSZWZlcmVuY2VTaW5nbGV0b24AAAAA
        AgAAAAs=
        """;

    private const string NodeWithNodeStructData = """
        AAEAAAD/////AQAAAAAAAAAMAgAAAEdGaXh0dXJlR2VuZXJhdG9yLCBWZXJzaW9uPTAuMC4wLjAsIEN1bHR1cmU9bmV1dHJh
        bCwgUHVibGljS2V5VG9rZW49bnVsbAUBAAAAIlRvdWtpLlJlc291cmNlcy5Ob2RlV2l0aE5vZGVTdHJ1Y3QCAAAABVZhbHVl
        Ck5vZGVTdHJ1Y3QBBBpUb3VraS5SZXNvdXJjZXMuTm9kZVN0cnVjdAIAAAACAAAABgMAAAAEcm9vdAX8////GlRvdWtpLlJl
        c291cmNlcy5Ob2RlU3RydWN0AQAAAAROb2RlBCJUb3VraS5SZXNvdXJjZXMuTm9kZVdpdGhOb2RlU3RydWN0AgAAAAIAAAAJ
        AQAAAAs=
        """;

    [TestMethod]
    public void BindToType_RegisteredType_RootsMembersForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        TypeName typeName = TypeName.Parse(typeof(RegisteredPayload).AssemblyQualifiedName!);

        Type type = resolver.BindToType(typeName);

        Assert.AreSame(typeof(RegisteredPayload), type);
    }

    [TestMethod]
    public void Deserialize_RegisteredType_RootsMembersForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        using MemoryStream stream = new(Convert.FromBase64String(RegisteredPayloadData));
        BinaryFormattedObject formatted = new(stream, resolver);

        RegisteredPayload payload = (RegisteredPayload)formatted.Deserialize();

        Assert.AreEqual("root", payload.Name);
        Assert.AreEqual(42, payload.Number);
        Assert.IsNull(payload.Next);
    }

    [TestMethod]
    public void Deserialize_DefaultFrameworkType_RootsMembersForAot()
    {
        using MemoryStream stream = new(Convert.FromBase64String(ListInt32Data));
        BinaryFormattedObject formatted = new(stream);

        List<int> values = (List<int>)formatted.Deserialize();

        CollectionAssert.AreEqual(new[] { 1, 2, 3, 5, 8 }, values);
    }

    [TestMethod]
    public void Deserialize_RegisteredArray_RootsArrayForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        resolver.Register<RegisteredPayload[]>();
        using MemoryStream stream = new(Convert.FromBase64String(RegisteredPayloadArrayData));
        BinaryFormattedObject formatted = new(stream, resolver);

        RegisteredPayload[] payload = (RegisteredPayload[])formatted.Deserialize();

        Assert.AreEqual(2, payload.Length);
        Assert.AreEqual("first", payload[0].Name);
        Assert.AreEqual("second", payload[1].Name);
    }

    [TestMethod]
    public void Deserialize_RegisteredRectangularArray_RootsArrayForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        resolver.Register<RegisteredPayload[,]>();
        using MemoryStream stream = new(Convert.FromBase64String(RegisteredPayloadMatrixData));
        BinaryFormattedObject formatted = new(stream, resolver);

        RegisteredPayload[,] payload = (RegisteredPayload[,])formatted.Deserialize();

        Assert.AreEqual(1, payload.GetLength(0));
        Assert.AreEqual(2, payload.GetLength(1));
        Assert.AreEqual("left", payload[0, 0].Name);
        Assert.AreEqual("right", payload[0, 1].Name);
    }

    [TestMethod]
    public void Deserialize_RegisteredCallbacks_RootsPrivateMethodsForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<CallbackPayload>();
        using MemoryStream stream = new(Convert.FromBase64String(CallbackPayloadData));
        BinaryFormattedObject formatted = new(stream, resolver);

        CallbackPayload payload = (CallbackPayload)formatted.Deserialize();

        Assert.AreEqual("callback", payload.Value);
        Assert.IsTrue(payload.OnDeserializingCalled);
        Assert.IsTrue(payload.OnDeserializedCalled);
        Assert.IsTrue(payload.CallbackCalled);
    }

    [TestMethod]
    public void Deserialize_RegisteredISerializable_RootsPrivateConstructorForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<SerializablePayload>();
        using MemoryStream stream = new(Convert.FromBase64String(SerializablePayloadData));
        BinaryFormattedObject formatted = new(stream, resolver);

        SerializablePayload payload = (SerializablePayload)formatted.Deserialize();

        Assert.AreEqual("serialization-info", payload.Value);
        Assert.IsTrue(payload.ConstructorCalled);
    }

    [TestMethod]
    public void Deserialize_RegisteredObjectReference_RootsReplacementForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<ObjectReferenceSingleton>();
        using MemoryStream stream = new(Convert.FromBase64String(ObjectReferenceSingletonData));
        BinaryFormattedObject formatted = new(stream, resolver);

        object payload = formatted.Deserialize();

        Assert.AreSame(ObjectReferenceSingleton.Value, payload);
    }

    [TestMethod]
    public void Deserialize_RegisteredSerializationInfoFixup_RootsUpdateForAot()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<NodeWithNodeStruct>();
        resolver.Register<NodeStruct>();
        using MemoryStream stream = new(Convert.FromBase64String(NodeWithNodeStructData));
        BinaryFormattedObject formatted = new(stream, resolver);

        NodeWithNodeStruct payload = (NodeWithNodeStruct)formatted.Deserialize();

        Assert.AreSame(payload, payload.NodeStruct.Node);
    }
}

#pragma warning disable CA5362 // The self-reference is part of the serialized test contract.

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

    [OnDeserializing]
    private void OnDeserializing(StreamingContext context) => OnDeserializingCalled = true;

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) => OnDeserializedCalled = true;

    public void OnDeserialization(object? sender) => CallbackCalled = true;
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
internal sealed class ObjectReferenceSingleton : IObjectReference
{
    private ObjectReferenceSingleton()
    {
    }

    internal static ObjectReferenceSingleton Value { get; } = new();

    public object GetRealObject(StreamingContext context) => Value;
}

#pragma warning disable CA5362 // The back-pointer is intentional test data for graph deserialization.

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

#pragma warning restore CA5362

#pragma warning restore SYSLIB0050 // Type or member is obsolete.