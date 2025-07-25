﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringInt
{
    public static TheoryData<int> IntData => new()
    {
        { 0 },
        { 42 },
        { int.MaxValue },
        { int.MinValue }
    };

    [Theory]
    [MemberData(nameof(IntData))]
    public void IntImplicit(int @int)
    {
        Value value = @int;
        Assert.Equal(@int, value.As<int>());
        Assert.Equal(typeof(int), value.Type);

        int? source = @int;
        value = source;
        Assert.Equal(source, value.As<int?>());
        Assert.Equal(typeof(int), value.Type);
    }

    [Theory]
    [MemberData(nameof(IntData))]
    public void IntCreate(int @int)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@int);
        }

        Assert.Equal(@int, value.As<int>());
        Assert.Equal(typeof(int), value.Type);

        int? source = @int;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        Assert.Equal(source, value.As<int?>());
        Assert.Equal(typeof(int), value.Type);
    }

    [Theory]
    [MemberData(nameof(IntData))]
    public void IntInOut(int @int)
    {
        Value value = @int;
        bool success = value.TryGetValue(out int result);
        Assert.True(success);
        Assert.Equal(@int, result);

        Assert.Equal(@int, value.As<int>());
        Assert.Equal(@int, (int)value);
    }

    [Theory]
    [MemberData(nameof(IntData))]
    public void NullableIntInIntOut(int @int)
    {
        int? source = @int;
        Value value = source;

        bool success = value.TryGetValue(out int result);
        Assert.True(success);
        Assert.Equal(@int, result);

        Assert.Equal(@int, value.As<int>());

        Assert.Equal(@int, (int)value);
    }

    [Theory]
    [MemberData(nameof(IntData))]
    public void IntInNullableIntOut(int @int)
    {
        int source = @int;
        Value value = source;
        Assert.True(value.TryGetValue(out int? result));
        Assert.Equal(@int, result);

        Assert.Equal(@int, (int?)value);
    }

    [Theory]
    [MemberData(nameof(IntData))]
    public void BoxedInt(int @int)
    {
        int i = @int;
        object o = i;
        Value value = Value.Create(o);

        Assert.Equal(typeof(int), value.Type);
        Assert.True(value.TryGetValue(out int result));
        Assert.Equal(@int, result);
        Assert.True(value.TryGetValue(out int? nullableResult));
        Assert.Equal(@int, nullableResult!.Value);


        int? n = @int;
        o = n;
        value = Value.Create(o);

        Assert.Equal(typeof(int), value.Type);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@int, result);
        Assert.True(value.TryGetValue(out nullableResult));
        Assert.Equal(@int, nullableResult!.Value);
    }

    [Fact]
    public void NullInt()
    {
        int? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<int?>());
        Assert.False(value.As<int?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(IntData))]
    public void OutAsObject(int @int)
    {
        Value value = @int;
        object o = value.As<object>();
        Assert.Equal(typeof(int), o.GetType());
        Assert.Equal(@int, (int)o);

        int? n = @int;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(int), o.GetType());
        Assert.Equal(@int, (int)o);
    }
}
