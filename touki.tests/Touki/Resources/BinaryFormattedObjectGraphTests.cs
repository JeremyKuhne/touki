// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Resources;

[TestClass]
public class BinaryFormattedObjectGraphTests
{
    [TestMethod]
    public void Deserialize_SharedReference_PreservesIdentity()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<SharedReferencePayload>();
        resolver.Register<RegisteredPayload>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.SharedReferencePayload,
            resolver);

        SharedReferencePayload payload = (SharedReferencePayload)formatted.Deserialize();

        payload.First.Should().BeSameAs(payload.Second);
        payload.First.Name.Should().Be("shared");
        payload.First.Number.Should().Be(17);
    }

    [TestMethod]
    public void Deserialize_ISerializableSelfCycle_PreservesReference()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<SerializableCycle>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.SerializableCycle,
            resolver);

        SerializableCycle payload = (SerializableCycle)formatted.Deserialize();

        payload.Value.Should().Be(42);
        payload.Next.Should().BeSameAs(payload);
    }

    [TestMethod]
    public void Deserialize_ISerializableStructBackPointer_AppliesFieldFixup()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<NodeWithNodeStruct>();
        resolver.Register<NodeStruct>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.NodeWithNodeStruct,
            resolver);

        NodeWithNodeStruct payload = (NodeWithNodeStruct)formatted.Deserialize();

        payload.Value.Should().Be("root");
        payload.NodeStruct.Node.Should().BeSameAs(payload);
    }

    [TestMethod]
    public void Deserialize_ISerializableStructArrayBackPointer_AppliesArrayFixups()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<ArrayBackReference>();
        resolver.Register<ArrayBackReference[]>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.ArrayBackReference,
            resolver);

        ArrayBackReference[] payload = (ArrayBackReference[])formatted.Deserialize();

        payload.Select(static item => item.Value).Should().Equal(11, 22);
        payload[0].Array.Should().BeSameAs(payload);
        payload[1].Array.Should().BeSameAs(payload);
    }

    [TestMethod]
    public void Deserialize_IObjectReferenceRoot_ReturnsReplacement()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<ObjectReferenceSingleton>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.ObjectReferenceSingleton,
            resolver);

        object payload = formatted.Deserialize();

        payload.Should().BeSameAs(ObjectReferenceSingleton.Value);
    }

    [TestMethod]
    public void Deserialize_NullNullableSerializationValue_ReturnsNull()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<NullableSerializablePayload>();
        resolver.Register<int?>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.NullableSerializablePayloadNull,
            resolver);

        NullableSerializablePayload payload = (NullableSerializablePayload)formatted.Deserialize();

        payload.Value.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_NonNullNullableSerializationValue_ReturnsValue()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<NullableSerializablePayload>();
        resolver.Register<int?>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.NullableSerializablePayloadValue,
            resolver);

        NullableSerializablePayload payload = (NullableSerializablePayload)formatted.Deserialize();

        payload.Value.Should().Be(42);
    }

    [TestMethod]
    public void Deserialize_RegisteredRectangularArray_ReturnsArray()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        resolver.Register<RegisteredPayload[,]>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.RegisteredPayloadMatrix,
            resolver);

        RegisteredPayload[,] payload = (RegisteredPayload[,])formatted.Deserialize();

        payload.Rank.Should().Be(2);
        payload.GetLength(0).Should().Be(1);
        payload.GetLength(1).Should().Be(2);
        payload[0, 0].Name.Should().Be("left");
        payload[0, 1].Name.Should().Be("right");
    }

    [TestMethod]
    public void Deserialize_CallbackStructField_ReappliesMutatedValue()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<CallbackStructContainer>();
        resolver.Register<CallbackStruct>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.CallbackStructField,
            resolver);

        CallbackStructContainer payload = (CallbackStructContainer)formatted.Deserialize();

        payload.Value.Value.Should().Be(142);
        payload.Value.CallbackCalled.Should().BeTrue();
    }

    [TestMethod]
    public void Deserialize_CallbackStructArray_ReappliesMutatedValues()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<CallbackStruct>();
        resolver.Register<CallbackStruct[]>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.CallbackStructArray,
            resolver);

        CallbackStruct[] payload = (CallbackStruct[])formatted.Deserialize();

        payload.Select(static item => item.Value).Should().Equal(142, 143);
        payload.Select(static item => item.CallbackCalled).Should().OnlyContain(static called => called);
    }

    [TestMethod]
    public void Deserialize_SharedDelayedValue_CompletesAllDependentCallbacks()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<FanOutPayload>();
        resolver.Register<FanOutOwner>();
        resolver.Register<FanOutValue>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.FanOutPayload,
            resolver);

        FanOutPayload payload = (FanOutPayload)formatted.Deserialize();

        payload.First.CallbackCalled.Should().BeTrue();
        payload.Second.CallbackCalled.Should().BeTrue();
        payload.First.Value.Should().BeSameAs(payload.Second.Value);
        ((FanOutValue)payload.First.Value).Value.Should().Be(42);
    }

    [TestMethod]
    public void Deserialize_SharedCallbackStruct_PropagatesAllConvergingCopies()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<ConvergingCallbackContainer>();
        resolver.Register<ConvergingCallbackStruct>();
        resolver.Register<CallbackStruct>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.ConvergingCallbackStruct,
            resolver);

        ConvergingCallbackContainer payload = (ConvergingCallbackContainer)formatted.Deserialize();

        CallbackStruct first = (CallbackStruct)payload.Value.First;
        CallbackStruct second = (CallbackStruct)payload.Value.Second;
        first.Value.Should().Be(142);
        first.CallbackCalled.Should().BeTrue();
        second.Value.Should().Be(142);
        second.CallbackCalled.Should().BeTrue();
    }
}