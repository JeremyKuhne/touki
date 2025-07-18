﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringDecimal
{
    public static TheoryData<decimal> DecimalData => new()
    {
        { 42 },
        { decimal.MaxValue },
        { decimal.MinValue }
    };

    [Fact]
    public void DecimalImplicit()
    {
        Value value = (decimal)42.0;
        Assert.Equal((decimal)42.0, value.As<decimal>());
        Assert.Equal(typeof(decimal), value.Type);

        decimal? source = (decimal?)42.0;
        value = source;
        Assert.Equal(source, value.As<decimal?>());
        Assert.Equal(typeof(decimal), value.Type);
    }

    [Theory]
    [MemberData(nameof(DecimalData))]
    public void DecimalInOut(decimal @decimal)
    {
        Value value = @decimal;
        bool success = value.TryGetValue(out decimal result);
        Assert.True(success);
        Assert.Equal(@decimal, result);

        Assert.Equal(@decimal, value.As<decimal>());
        Assert.Equal(@decimal, (decimal)value);
    }

    [Theory]
    [MemberData(nameof(DecimalData))]
    public void NullableDecimalInDecimalOut(decimal @decimal)
    {
        decimal? source = @decimal;
        Value value = Value.Create(source);

        bool success = value.TryGetValue(out decimal result);
        Assert.True(success);
        Assert.Equal(@decimal, result);

        Assert.Equal(@decimal, value.As<decimal>());

        Assert.Equal(@decimal, (decimal)value);
    }

    [Theory]
    [MemberData(nameof(DecimalData))]
    public void DecimalInNullableDecimalOut(decimal @decimal)
    {
        decimal source = @decimal;
        Value value = Value.Create(source);
        bool success = value.TryGetValue(out decimal? result);
        Assert.True(success);
        Assert.Equal(@decimal, result);

        Assert.Equal(@decimal, (decimal?)value);
    }

    [Fact]
    public void NullDecimal()
    {
        decimal? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<decimal?>());
        Assert.False(value.As<decimal?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(DecimalData))]
    public void OutAsObject(decimal @decimal)
    {
        Value value = Value.Create(@decimal);
        object o = value.As<object>();
        Assert.Equal(typeof(decimal), o.GetType());
        Assert.Equal(@decimal, (decimal)o);

        decimal? n = @decimal;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(decimal), o.GetType());
        Assert.Equal(@decimal, (decimal)o);
    }
}
