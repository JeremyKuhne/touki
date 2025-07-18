﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringUShort
{
    public static TheoryData<ushort> UShortData => new()
    {
        { 42 },
        { ushort.MaxValue },
        { ushort.MinValue }
    };

    [Theory]
    [MemberData(nameof(UShortData))]
    public void UShortImplicit(ushort @ushort)
    {
        Value value = @ushort;
        Assert.Equal(@ushort, value.As<ushort>());
        Assert.Equal(typeof(ushort), value.Type);

        ushort? source = @ushort;
        value = source;
        Assert.Equal(source, value.As<ushort?>());
        Assert.Equal(typeof(ushort), value.Type);
    }

    [Theory]
    [MemberData(nameof(UShortData))]
    public void UShortCreate(ushort @ushort)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@ushort);
        }

        Assert.Equal(@ushort, value.As<ushort>());
        Assert.Equal(typeof(ushort), value.Type);

        ushort? source = @ushort;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        Assert.Equal(source, value.As<ushort?>());
        Assert.Equal(typeof(ushort), value.Type);
    }

    [Theory]
    [MemberData(nameof(UShortData))]
    public void UShortInOut(ushort @ushort)
    {
        Value value = @ushort;
        bool success = value.TryGetValue(out ushort result);
        Assert.True(success);
        Assert.Equal(@ushort, result);

        Assert.Equal(@ushort, value.As<ushort>());
        Assert.Equal(@ushort, (ushort)value);
    }

    [Theory]
    [MemberData(nameof(UShortData))]
    public void NullableUShortInUShortOut(ushort @ushort)
    {
        ushort? source = @ushort;
        Value value = source;

        bool success = value.TryGetValue(out ushort result);
        Assert.True(success);
        Assert.Equal(@ushort, result);

        Assert.Equal(@ushort, value.As<ushort>());

        Assert.Equal(@ushort, (ushort)value);
    }

    [Theory]
    [MemberData(nameof(UShortData))]
    public void UShortInNullableUShortOut(ushort @ushort)
    {
        ushort source = @ushort;
        Value value = source;
        bool success = value.TryGetValue(out ushort? result);
        Assert.True(success);
        Assert.Equal(@ushort, result);

        Assert.Equal(@ushort, (ushort?)value);
    }

    [Theory]
    [MemberData(nameof(UShortData))]
    public void BoxedUShort(ushort @ushort)
    {
        ushort i = @ushort;
        object o = i;
        Value value = Value.Create(o);

        Assert.Equal(typeof(ushort), value.Type);
        Assert.True(value.TryGetValue(out ushort result));
        Assert.Equal(@ushort, result);
        Assert.True(value.TryGetValue(out ushort? nullableResult));
        Assert.Equal(@ushort, nullableResult!.Value);


        ushort? n = @ushort;
        o = n;
        value = Value.Create(o);

        Assert.Equal(typeof(ushort), value.Type);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@ushort, result);
        Assert.True(value.TryGetValue(out nullableResult));
        Assert.Equal(@ushort, nullableResult!.Value);
    }

    [Fact]
    public void NullUShort()
    {
        ushort? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<ushort?>());
        Assert.False(value.As<ushort?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(UShortData))]
    public void OutAsObject(ushort @ushort)
    {
        Value value = @ushort;
        object o = value.As<object>();
        Assert.Equal(typeof(ushort), o.GetType());
        Assert.Equal(@ushort, (ushort)o);

        ushort? n = @ushort;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(ushort), o.GetType());
        Assert.Equal(@ushort, (ushort)o);
    }
}
