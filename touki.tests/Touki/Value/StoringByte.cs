// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

[TestClass]
public class StoringByte
{
    public static IEnumerable<object[]> ByteData()
    {
        yield return [(byte)42];
        yield return [byte.MaxValue];
        yield return [byte.MinValue];
    }

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public void ByteImplicit(byte @byte)
    {
        Value value = @byte;
        value.As<byte>().Should().Be(@byte);
        value.Type.Should().Be(typeof(byte));

        byte? source = @byte;
        value = source;
        value.As<byte?>().Should().Be(source);
        value.Type.Should().Be(typeof(byte));
    }

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public void ByteCreate(byte @byte)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@byte);
        }

        value.As<byte>().Should().Be(@byte);
        value.Type.Should().Be(typeof(byte));

        byte? source = @byte;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        value.As<byte?>().Should().Be(source);
        value.Type.Should().Be(typeof(byte));
    }

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public void ByteInOut(byte @byte)
    {
        Value value = @byte;
        bool success = value.TryGetValue(out byte result);
        success.Should().BeTrue();
        result.Should().Be(@byte);

        value.As<byte>().Should().Be(@byte);
        ((byte)value).Should().Be(@byte);
    }

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public void NullableByteInByteOut(byte @byte)
    {
        byte? source = @byte;
        Value value = source;

        bool success = value.TryGetValue(out byte result);
        success.Should().BeTrue();
        result.Should().Be(@byte);

        value.As<byte>().Should().Be(@byte);

        ((byte)value).Should().Be(@byte);
    }

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public void ByteInNullableByteOut(byte @byte)
    {
        byte source = @byte;
        Value value = source;
        bool success = value.TryGetValue(out byte? result);
        success.Should().BeTrue();
        result.Should().Be(@byte);

        ((byte?)value).Should().Be(@byte);
    }

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public void BoxedByte(byte @byte)
    {
        byte i = @byte;
        object o = i;
        Value value = Value.Create(o);

        value.Type.Should().Be(typeof(byte));
        value.TryGetValue(out byte result).Should().BeTrue();
        result.Should().Be(@byte);
        value.TryGetValue(out byte? nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@byte);


        byte? n = @byte;
        o = n;
        value = Value.Create(o);

        value.Type.Should().Be(typeof(byte));
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@byte);
        value.TryGetValue(out nullableResult).Should().BeTrue();
        nullableResult!.Value.Should().Be(@byte);
    }

    [TestMethod]
    public void NullByte()
    {
        byte? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<byte?>().Should().Be(source);
        value.As<byte?>().HasValue.Should().BeFalse();
    }

    [TestMethod]
    [DynamicData(nameof(ByteData))]
    public void OutAsObject(byte @byte)
    {
        Value value = @byte;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(byte));
        ((byte)o).Should().Be(@byte);

        byte? n = @byte;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(byte));
        ((byte)o).Should().Be(@byte);
    }
}
