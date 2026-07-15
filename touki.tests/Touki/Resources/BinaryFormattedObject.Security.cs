// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Formats.Nrbf;
using System.Runtime.Serialization;
using System.Text;
using Touki.Resources.BinaryFormat;

namespace Touki.Resources;

[TestClass]
[DoNotParallelize]
public class BinaryFormattedObjectSecurityTests
{
    [TestMethod]
    public void GetCapacityHint_AtAndAboveLimit_CapsCapacity()
    {
        BinaryFormatDeserializer.GetCapacityHint(256).Should().Be(256);
        BinaryFormatDeserializer.GetCapacityHint(257).Should().Be(256);
    }

    [TestMethod]
    public void Constructor_MalformedPayload_ThrowsSerializationException()
    {
        using MemoryStream stream = new([0, 1, 2, 3]);

        Action action = () => _ = new BinaryFormattedObject(stream);

        action.Should().Throw<SerializationException>();
    }

    [TestMethod]
    public void Deserialize_DecodableValueTypeSelfCycle_ThrowsSerializationException()
    {
        using MemoryStream stream = CreateValueTypeSelfCyclePayload(typeof(SelfReferencingStruct));
        RegisteredTypeResolver resolver = new();
        resolver.Register<SelfReferencingStruct>();
        BinaryFormattedObject formatted = new(stream, resolver);

        Action action = () => formatted.Deserialize();

        action.Should().Throw<SerializationException>();
    }

    [TestMethod]
    public void Deserialize_ISerializableValueTypeSelfCycle_DoesNotInvokeSerializationConstructor()
    {
        SelfReferencingSerializableStruct.InvocationCount = 0;
        using MemoryStream stream = CreateValueTypeSelfCyclePayload(typeof(SelfReferencingSerializableStruct));
        RegisteredTypeResolver resolver = new();
        resolver.Register<SelfReferencingSerializableStruct>();
        BinaryFormattedObject formatted = new(stream, resolver);

        Action action = () => formatted.Deserialize();

        action.Should().Throw<SerializationException>();
        SelfReferencingSerializableStruct.InvocationCount.Should().Be(0);
    }

    [TestMethod]
    public void Deserialize_UnregisteredCallbackType_DoesNotInvokeUserCode()
    {
        CallbackPayload.InvocationCount = 0;
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.CallbackPayload);

        Action action = () => formatted.Deserialize();

        action.Should().Throw<SerializationException>().WithMessage("*is not registered*");
        CallbackPayload.InvocationCount.Should().Be(0);
    }

    private static MemoryStream CreateValueTypeSelfCyclePayload(Type type)
    {
        const int classId = 1;
        const int libraryId = 2;

        MemoryStream stream = new();
        using (System.IO.BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)SerializationRecordType.SerializedStreamHeader);
            writer.Write(classId);
            writer.Write(classId);
            writer.Write(1);
            writer.Write(0);

            writer.Write((byte)SerializationRecordType.BinaryLibrary);
            writer.Write(libraryId);
            writer.Write(type.Assembly.FullName!);

            writer.Write((byte)SerializationRecordType.ClassWithMembersAndTypes);
            writer.Write(classId);
            writer.Write(type.FullName!);
            writer.Write(1);
            writer.Write(nameof(SelfReferencingStruct.Value));
            writer.Write((byte)4); // BinaryType.Class ([MS-NRBF] 2.1.2.2)
            writer.Write(type.FullName!);
            writer.Write(libraryId);
            writer.Write(libraryId);

            writer.Write((byte)SerializationRecordType.MemberReference);
            writer.Write(classId);
            writer.Write((byte)SerializationRecordType.MessageEnd);
        }

        stream.Position = 0;
        return stream;
    }
}
