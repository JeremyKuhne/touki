﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringChar
{
    public static TheoryData<char> CharData => new()
    {
        { '!' },
        { char.MaxValue },
        { char.MinValue }
    };

    [Theory]
    [MemberData(nameof(CharData))]
    public void CharImplicit(char @char)
    {
        Value value = @char;
        Assert.Equal(@char, value.As<char>());
        Assert.Equal(typeof(char), value.Type);

        char? source = @char;
        value = source;
        Assert.Equal(source, value.As<char?>());
        Assert.Equal(typeof(char), value.Type);
    }

    [Theory]
    [MemberData(nameof(CharData))]
    public void CharCreate(char @char)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@char);
        }

        Assert.Equal(@char, value.As<char>());
        Assert.Equal(typeof(char), value.Type);

        char? source = @char;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        Assert.Equal(source, value.As<char?>());
        Assert.Equal(typeof(char), value.Type);
    }

    [Theory]
    [MemberData(nameof(CharData))]
    public void CharInOut(char @char)
    {
        Value value = @char;
        bool success = value.TryGetValue(out char result);
        Assert.True(success);
        Assert.Equal(@char, result);

        Assert.Equal(@char, value.As<char>());
        Assert.Equal(@char, (char)value);
    }

    [Theory]
    [MemberData(nameof(CharData))]
    public void NullableCharInCharOut(char @char)
    {
        char? source = @char;
        Value value = source;

        bool success = value.TryGetValue(out char result);
        Assert.True(success);
        Assert.Equal(@char, result);

        Assert.Equal(@char, value.As<char>());

        Assert.Equal(@char, (char)value);
    }

    [Theory]
    [MemberData(nameof(CharData))]
    public void CharInNullableCharOut(char @char)
    {
        char source = @char;
        Value value = source;
        bool success = value.TryGetValue(out char? result);
        Assert.True(success);
        Assert.Equal(@char, result);

        Assert.Equal(@char, (char?)value);
    }

    [Theory]
    [MemberData(nameof(CharData))]
    public void BoxedChar(char @char)
    {
        char i = @char;
        object o = i;
        Value value = Value.Create(o);

        Assert.Equal(typeof(char), value.Type);
        Assert.True(value.TryGetValue(out char result));
        Assert.Equal(@char, result);
        Assert.True(value.TryGetValue(out char? nullableResult));
        Assert.Equal(@char, nullableResult!.Value);


        char? n = @char;
        o = n;
        value = Value.Create(o);

        Assert.Equal(typeof(char), value.Type);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@char, result);
        Assert.True(value.TryGetValue(out nullableResult));
        Assert.Equal(@char, nullableResult!.Value);
    }

    [Fact]
    public void NullChar()
    {
        char? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<char?>());
        Assert.False(value.As<char?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(CharData))]
    public void OutAsObject(char @char)
    {
        Value value = @char;
        object o = value.As<object>();
        Assert.Equal(typeof(char), o.GetType());
        Assert.Equal(@char, (char)o);

        char? n = @char;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(char), o.GetType());
        Assert.Equal(@char, (char)o);
    }
}
