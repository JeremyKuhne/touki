// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringArrays
{
    [Test]
    public void ByteArray()
    {
        byte[] b = new byte[10];
        Value value;

        value = Value.Create(b);
        value.Type.Should().Be(typeof(byte[]));
        value.As<byte[]>().Should().BeSameAs(b);
        ((byte[])value.As<object>()).Should().Equal(b);

        Assert.Throws<InvalidCastException>(() => value.As<ArraySegment<byte>>());
    }

    [Test]
    public void CharArray()
    {
        char[] b = new char[10];
        Value value;

        value = Value.Create(b);
        value.Type.Should().Be(typeof(char[]));
        value.As<char[]>().Should().BeSameAs(b);
        ((char[])value.As<object>()).Should().Equal(b);

        Assert.Throws<InvalidCastException>(() => value.As<ArraySegment<char>>());
    }

    [Test]
    public void ByteSegment()
    {
        byte[] b = new byte[10];
        Value value;

        ArraySegment<byte> segment = new(b);
        value = Value.Create(segment);
        value.Type.Should().Be(typeof(ArraySegment<byte>));
        value.As<ArraySegment<byte>>().Should().Equal(segment);
        ((ArraySegment<byte>)value.As<object>()).Should().Equal(segment);
        Assert.Throws<InvalidCastException>(() => value.As<byte[]>());

        segment = new(b, 0, 0);
        value = Value.Create(segment);
        value.Type.Should().Be(typeof(ArraySegment<byte>));
        value.As<ArraySegment<byte>>().Should().Equal(segment);
        ((ArraySegment<byte>)value.As<object>()).Should().Equal(segment);
        Assert.Throws<InvalidCastException>(() => value.As<byte[]>());

        segment = new(b, 1, 1);
        value = Value.Create(segment);
        value.Type.Should().Be(typeof(ArraySegment<byte>));
        value.As<ArraySegment<byte>>().Should().Equal(segment);
        ((ArraySegment<byte>)value.As<object>()).Should().Equal(segment);
        Assert.Throws<InvalidCastException>(() => value.As<byte[]>());
    }

    [Test]
    public void CharSegment()
    {
        char[] b = new char[10];
        Value value;

        ArraySegment<char> segment = new(b);
        value = Value.Create(segment);
        value.Type.Should().Be(typeof(ArraySegment<char>));
        value.As<ArraySegment<char>>().Should().Equal(segment);
        ((ArraySegment<char>)value.As<object>()).Should().Equal(segment);
        Assert.Throws<InvalidCastException>(() => value.As<char[]>());

        segment = new(b, 0, 0);
        value = Value.Create(segment);
        value.Type.Should().Be(typeof(ArraySegment<char>));
        value.As<ArraySegment<char>>().Should().Equal(segment);
        ((ArraySegment<char>)value.As<object>()).Should().Equal(segment);
        Assert.Throws<InvalidCastException>(() => value.As<char[]>());

        segment = new(b, 1, 1);
        value = Value.Create(segment);
        value.Type.Should().Be(typeof(ArraySegment<char>));
        value.As<ArraySegment<char>>().Should().Equal(segment);
        ((ArraySegment<char>)value.As<object>()).Should().Equal(segment);
        Assert.Throws<InvalidCastException>(() => value.As<char[]>());
    }

    [Test]
    public void ArraySegment_NullByteArray_Throws()
    {
        ArraySegment<byte> segment = default;
        Assert.Throws<ArgumentNullException>(() => Value.Create(segment));
    }

    [Test]
    public void ArraySegment_NullCharArray_Throws()
    {
        ArraySegment<char> segment = default;
        Assert.Throws<ArgumentNullException>(() => Value.Create(segment));
    }
}
