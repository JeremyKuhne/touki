﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringByte
{
    public static TheoryData<byte> ByteData => new()
    {
        { 42 },
        { byte.MaxValue },
        { byte.MinValue }
    };

    [Theory]
    [MemberData(nameof(ByteData))]
    public void ByteImplicit(byte @byte)
    {
        Value value = @byte;
        Assert.Equal(@byte, value.As<byte>());
        Assert.Equal(typeof(byte), value.Type);

        byte? source = @byte;
        value = source;
        Assert.Equal(source, value.As<byte?>());
        Assert.Equal(typeof(byte), value.Type);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void ByteCreate(byte @byte)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@byte);
        }

        Assert.Equal(@byte, value.As<byte>());
        Assert.Equal(typeof(byte), value.Type);

        byte? source = @byte;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        Assert.Equal(source, value.As<byte?>());
        Assert.Equal(typeof(byte), value.Type);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void ByteInOut(byte @byte)
    {
        Value value = @byte;
        bool success = value.TryGetValue(out byte result);
        Assert.True(success);
        Assert.Equal(@byte, result);

        Assert.Equal(@byte, value.As<byte>());
        Assert.Equal(@byte, (byte)value);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void NullableByteInByteOut(byte @byte)
    {
        byte? source = @byte;
        Value value = source;

        bool success = value.TryGetValue(out byte result);
        Assert.True(success);
        Assert.Equal(@byte, result);

        Assert.Equal(@byte, value.As<byte>());

        Assert.Equal(@byte, (byte)value);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void ByteInNullableByteOut(byte @byte)
    {
        byte source = @byte;
        Value value = source;
        bool success = value.TryGetValue(out byte? result);
        Assert.True(success);
        Assert.Equal(@byte, result);

        Assert.Equal(@byte, (byte?)value);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void BoxedByte(byte @byte)
    {
        byte i = @byte;
        object o = i;
        Value value = Value.Create(o);

        Assert.Equal(typeof(byte), value.Type);
        Assert.True(value.TryGetValue(out byte result));
        Assert.Equal(@byte, result);
        Assert.True(value.TryGetValue(out byte? nullableResult));
        Assert.Equal(@byte, nullableResult!.Value);


        byte? n = @byte;
        o = n;
        value = Value.Create(o);

        Assert.Equal(typeof(byte), value.Type);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@byte, result);
        Assert.True(value.TryGetValue(out nullableResult));
        Assert.Equal(@byte, nullableResult!.Value);
    }

    [Fact]
    public void NullByte()
    {
        byte? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<byte?>());
        Assert.False(value.As<byte?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(ByteData))]
    public void OutAsObject(byte @byte)
    {
        Value value = @byte;
        object o = value.As<object>();
        Assert.Equal(typeof(byte), o.GetType());
        Assert.Equal(@byte, (byte)o);

        byte? n = @byte;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(byte), o.GetType());
        Assert.Equal(@byte, (byte)o);
    }
}
