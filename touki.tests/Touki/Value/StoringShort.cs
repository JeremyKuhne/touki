// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringShort
{
    public static TheoryData<short> ShortData => new()
    {
        { 0 },
        { 42 },
        { short.MaxValue },
        { short.MinValue }
    };

    [Theory]
    [MemberData(nameof(ShortData))]
    public void ShortImplicit(short @short)
    {
        Value value = @short;
        Assert.Equal(@short, value.As<short>());
        Assert.Equal(typeof(short), value.Type);

        short? source = @short;
        value = source;
        Assert.Equal(source, value.As<short?>());
        Assert.Equal(typeof(short), value.Type);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void ShortCreate(short @short)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@short);
        }

        Assert.Equal(@short, value.As<short>());
        Assert.Equal(typeof(short), value.Type);

        short? source = @short;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        Assert.Equal(source, value.As<short?>());
        Assert.Equal(typeof(short), value.Type);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void ShortInOut(short @short)
    {
        Value value = @short;
        bool success = value.TryGetValue(out short result);
        Assert.True(success);
        Assert.Equal(@short, result);

        Assert.Equal(@short, value.As<short>());
        Assert.Equal(@short, (short)value);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void NullableShortInShortOut(short @short)
    {
        short? source = @short;
        Value value = source;

        bool success = value.TryGetValue(out short result);
        Assert.True(success);
        Assert.Equal(@short, result);

        Assert.Equal(@short, value.As<short>());

        Assert.Equal(@short, (short)value);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void ShortInNullableShortOut(short @short)
    {
        short source = @short;
        Value value = source;
        bool success = value.TryGetValue(out short? result);
        Assert.True(success);
        Assert.Equal(@short, result);

        Assert.Equal(@short, (short?)value);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void BoxedShort(short @short)
    {
        short i = @short;
        object o = i;
        Value value = Value.Create(o);

        Assert.Equal(typeof(short), value.Type);
        Assert.True(value.TryGetValue(out short result));
        Assert.Equal(@short, result);
        Assert.True(value.TryGetValue(out short? nullableResult));
        Assert.Equal(@short, nullableResult!.Value);


        short? n = @short;
        o = n;
        value = Value.Create(o);

        Assert.Equal(typeof(short), value.Type);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@short, result);
        Assert.True(value.TryGetValue(out nullableResult));
        Assert.Equal(@short, nullableResult!.Value);
    }

    [Fact]
    public void NullShort()
    {
        short? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<short?>());
        Assert.False(value.As<short?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(ShortData))]
    public void OutAsObject(short @short)
    {
        Value value = @short;
        object o = value.As<object>();
        Assert.Equal(typeof(short), o.GetType());
        Assert.Equal(@short, (short)o);

        short? n = @short;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(short), o.GetType());
        Assert.Equal(@short, (short)o);
    }
}
