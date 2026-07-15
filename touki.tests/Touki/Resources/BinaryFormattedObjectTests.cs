// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Formats.Nrbf;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;

namespace Touki.Resources;

[TestClass]
[DoNotParallelize]
public class BinaryFormattedObjectTests
{
    [TestMethod]
    public void Constructor_NullStream_ThrowsArgumentNullException()
    {
        Action action = () => _ = new BinaryFormattedObject(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void Constructor_ValidPayload_LeavesStreamOpen()
    {
        using MemoryStream stream = new(Convert.FromBase64String(BinaryFormattedObjectFixtures.Int32));

        _ = new BinaryFormattedObject(stream);

        stream.CanRead.Should().BeTrue();
    }

    [TestMethod]
    public void Constructor_NonZeroLowerBoundArray_ThrowsNotSupportedException()
    {
        using MemoryStream stream = CreateNonZeroLowerBoundArrayPayload();

        Action action = () => _ = new BinaryFormattedObject(stream);

        action.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void Constructor_StreamThrowsKeyNotFoundException_PropagatesException()
    {
        KeyNotFoundException exception = new("user stream failure");
        using ThrowingReadStream stream = new(exception);

        Action action = () => _ = new BinaryFormattedObject(stream);

        action.Should().Throw<KeyNotFoundException>().Which.Should().BeSameAs(exception);
    }

    [TestMethod]
    public void Constructor_TargetInvocationExceptionWithoutInner_ThrowsSerializationException()
    {
        TargetInvocationException exception = new("user stream failure", inner: null);
        using ThrowingReadStream stream = new(exception);

        Action action = () => _ = new BinaryFormattedObject(stream);

        action.Should().Throw<SerializationException>()
            .Which.InnerException.Should().BeSameAs(exception);
    }

    [TestMethod]
    public void Indexer_RootRecordId_ReturnsRootRecord()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.Int32);

        formatted.RecordMap[formatted.RootRecord.Id].Should().BeSameAs(formatted.RootRecord);
        formatted[formatted.RootRecord.Id].Should().BeSameAs(formatted.RootRecord);
    }

    [TestMethod]
    public void Deserialize_FrameworkInt32Payload_ReturnsValue()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.Int32);

        formatted.Deserialize().Should().Be(42);
    }

    [TestMethod]
    public void Deserialize_CalledTwice_ThrowsInvalidOperationException()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.Int32);
        _ = formatted.Deserialize();

        Action action = () => formatted.Deserialize();

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been deserialized*");
    }

    [TestMethod]
    public void Deserialize_FirstCallFails_SecondCallThrowsInvalidOperationException()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.RegisteredPayloadArray,
            resolver);
        Action firstCall = () => formatted.Deserialize();
        firstCall.Should().Throw<SerializationException>();

        Action secondCall = () => formatted.Deserialize();

        secondCall.Should().Throw<InvalidOperationException>()
            .WithMessage("*already been deserialized*");
    }

    [TestMethod]
    public async Task Deserialize_ConcurrentCalls_OnlyOneSucceeds()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(BinaryFormattedObjectFixtures.Int32);
        using Barrier barrier = new(3);
        Task<(object? Result, Exception? Exception)> first = Task.Run(Deserialize);
        Task<(object? Result, Exception? Exception)> second = Task.Run(Deserialize);
        barrier.SignalAndWait();

        (object? Result, Exception? Exception)[] outcomes = await Task.WhenAll(first, second).ConfigureAwait(false);

        outcomes.Count(static outcome => Equals(outcome.Result, 42) && outcome.Exception is null).Should().Be(1);
        outcomes.Count(static outcome => outcome.Result is null
            && outcome.Exception is InvalidOperationException).Should().Be(1);

        (object? Result, Exception? Exception) Deserialize()
        {
            barrier.SignalAndWait();
            try
            {
                return (formatted.Deserialize(), null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }
    }

    [TestMethod]
    public void Deserialize_FrameworkListPayload_ReturnsValues()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.ListInt32);

        object result = formatted.Deserialize();

        result.Should().BeOfType<List<int>>();
        ((List<int>)result).Should().Equal(1, 2, 3, 5, 8);
    }

    [TestMethod]
    public void Deserialize_FrameworkDateTimePayload_ReturnsValue()
    {
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.DateTime);

        formatted.Deserialize().Should().Be(new DateTime(2025, 6, 15, 12, 34, 56, DateTimeKind.Utc));
    }

    [TestMethod]
    public void Deserialize_RegisteredType_ReturnsObject()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.RegisteredPayload,
            resolver);

        object result = formatted.Deserialize();

        result.Should().BeOfType<RegisteredPayload>();
        RegisteredPayload payload = (RegisteredPayload)result;
        payload.Name.Should().Be("root");
        payload.Number.Should().Be(42);
        payload.Next.Should().BeNull();
    }

    [TestMethod]
    public void Deserialize_CyclicRegisteredType_PreservesReference()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.RegisteredPayloadCycle,
            resolver);

        RegisteredPayload payload = (RegisteredPayload)formatted.Deserialize();

        payload.Name.Should().Be("cycle");
        payload.Next.Should().BeSameAs(payload);
    }

    [TestMethod]
    public void Deserialize_RegisteredArrayAndElement_ReturnsArray()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        resolver.Register<RegisteredPayload[]>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.RegisteredPayloadArray,
            resolver);

        object result = formatted.Deserialize();

        result.Should().BeOfType<RegisteredPayload[]>();
        RegisteredPayload[] payloads = (RegisteredPayload[])result;
        payloads.Select(static payload => payload.Name).Should().Equal("first", "second");
        payloads.Select(static payload => payload.Number).Should().Equal(1, 2);
    }

    [TestMethod]
    public void Deserialize_RegisteredCallbackType_InvokesCallbacks()
    {
        CallbackPayload.InvocationCount = 0;
        RegisteredTypeResolver resolver = new();
        resolver.Register<CallbackPayload>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.CallbackPayload,
            resolver);

        CallbackPayload payload = (CallbackPayload)formatted.Deserialize();

        payload.Value.Should().Be("callback");
        payload.OnDeserializingCalled.Should().BeTrue();
        payload.OnDeserializedCalled.Should().BeTrue();
        payload.CallbackCalled.Should().BeTrue();
        CallbackPayload.InvocationCount.Should().Be(3);
    }

    [TestMethod]
    public void Deserialize_CustomResolverCallbackType_InvokesCallbacks()
    {
        CallbackPayload.InvocationCount = 0;
        using MemoryStream stream = new(Convert.FromBase64String(BinaryFormattedObjectFixtures.CallbackPayload));
        BinaryFormattedObject formatted = new(stream, new CallbackTypeResolver());

        CallbackPayload payload = (CallbackPayload)formatted.Deserialize();

        payload.OnDeserializingCalled.Should().BeTrue();
        payload.OnDeserializedCalled.Should().BeTrue();
        payload.CallbackCalled.Should().BeTrue();
        CallbackPayload.InvocationCount.Should().Be(3);
    }

    [TestMethod]
    public void Deserialize_ResolverThrowsKeyNotFoundException_PropagatesException()
    {
        KeyNotFoundException exception = new("user resolver failure");
        using MemoryStream stream = new(Convert.FromBase64String(BinaryFormattedObjectFixtures.RegisteredPayload));
        BinaryFormattedObject formatted = new(stream, new ThrowingTypeResolver(exception));

        Action action = () => formatted.Deserialize();

        action.Should().Throw<KeyNotFoundException>().Which.Should().BeSameAs(exception);
    }

    [TestMethod]
    public void Deserialize_TargetInvocationExceptionWithoutInner_ThrowsSerializationException()
    {
        TargetInvocationException exception = new("user resolver failure", inner: null);
        using MemoryStream stream = new(Convert.FromBase64String(BinaryFormattedObjectFixtures.RegisteredPayload));
        BinaryFormattedObject formatted = new(stream, new ThrowingTypeResolver(exception));

        Action action = () => formatted.Deserialize();

        action.Should().Throw<SerializationException>()
            .Which.InnerException.Should().BeSameAs(exception);
    }

    [TestMethod]
    public void Deserialize_RegisteredISerializableType_InvokesSerializationConstructor()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<SerializablePayload>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.SerializablePayload,
            resolver);

        SerializablePayload payload = (SerializablePayload)formatted.Deserialize();

        payload.Value.Should().Be("serialization-info");
        payload.ConstructorCalled.Should().BeTrue();
    }

    [TestMethod]
    public void Deserialize_ArrayTypeNotRegistered_ThrowsSerializationException()
    {
        RegisteredTypeResolver resolver = new();
        resolver.Register<RegisteredPayload>();
        BinaryFormattedObject formatted = BinaryFormattedObjectFixtures.Parse(
            BinaryFormattedObjectFixtures.RegisteredPayloadArray,
            resolver);

        Action action = () => formatted.Deserialize();

        action.Should().Throw<SerializationException>().WithMessage("*is not registered*");
    }

    private static MemoryStream CreateNonZeroLowerBoundArrayPayload()
    {
        MemoryStream stream = new();
        using (System.IO.BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((byte)SerializationRecordType.SerializedStreamHeader);
            writer.Write(1);
            writer.Write(-1);
            writer.Write(1);
            writer.Write(0);

            writer.Write((byte)SerializationRecordType.BinaryArray);
            writer.Write(1);
            writer.Write((byte)3); // BinaryArrayType.SingleOffset ([MS-NRBF] 2.4.3.2)
            writer.Write(1);
            writer.Write(1);
            writer.Write(1);
            writer.Write((byte)0); // BinaryType.Primitive ([MS-NRBF] 2.1.2.2)
            writer.Write((byte)8); // PrimitiveType.Int32 ([MS-NRBF] 2.1.1.6)
            writer.Write(42);
            writer.Write((byte)SerializationRecordType.MessageEnd);
        }

        stream.Position = 0;
        return stream;
    }

    private sealed class CallbackTypeResolver : ITypeResolver
    {
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public Type BindToType(System.Reflection.Metadata.TypeName typeName)
            => typeName.FullName == typeof(CallbackPayload).FullName
                ? typeof(CallbackPayload)
                : throw new SerializationException($"Type '{typeName.AssemblyQualifiedName}' is not registered.");

        public bool TryBindToType(
            System.Reflection.Metadata.TypeName typeName,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All), NotNullWhen(true)] out Type? type)
        {
            type = typeName.FullName == typeof(CallbackPayload).FullName
                ? typeof(CallbackPayload)
                : null;
            return type is not null;
        }
    }

    private sealed class ThrowingTypeResolver(Exception exception) : ITypeResolver
    {
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public Type BindToType(System.Reflection.Metadata.TypeName typeName) => throw exception;

        public bool TryBindToType(
            System.Reflection.Metadata.TypeName typeName,
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All), NotNullWhen(true)] out Type? type)
            => throw exception;
    }

    private sealed class ThrowingReadStream(Exception exception) : System.IO.Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw exception;

        public override int ReadByte() => throw exception;

        public override long Seek(long offset, System.IO.SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}