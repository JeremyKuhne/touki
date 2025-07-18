﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringDouble
{
    public static TheoryData<double> DoubleData => new()
    {
        { 0d },
        { 42d },
        { double.MaxValue },
        { double.MinValue },
        { double.NaN },
        { double.NegativeInfinity },
        { double.PositiveInfinity }
    };

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void DoubleImplicit(double @double)
    {
        Value value = @double;
        Assert.Equal(@double, value.As<double>());
        Assert.Equal(typeof(double), value.Type);

        double? source = @double;
        value = source;
        Assert.Equal(source, value.As<double?>());
        Assert.Equal(typeof(double), value.Type);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void DoubleCreate(double @double)
    {
        Value value;
        using (MemoryWatch.Create)
        {
            value = Value.Create(@double);
        }

        Assert.Equal(@double, value.As<double>());
        Assert.Equal(typeof(double), value.Type);

        double? source = @double;

        using (MemoryWatch.Create)
        {
            value = Value.Create(source);
        }

        Assert.Equal(source, value.As<double?>());
        Assert.Equal(typeof(double), value.Type);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void DoubleInOut(double @double)
    {
        Value value = @double;
        bool success = value.TryGetValue(out double result);
        Assert.True(success);
        Assert.Equal(@double, result);

        Assert.Equal(@double, value.As<double>());
        Assert.Equal(@double, (double)value);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void NullableDoubleInDoubleOut(double @double)
    {
        double? source = @double;
        Value value = source;

        bool success = value.TryGetValue(out double result);
        Assert.True(success);
        Assert.Equal(@double, result);

        Assert.Equal(@double, value.As<double>());

        Assert.Equal(@double, (double)value);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void DoubleInNullableDoubleOut(double @double)
    {
        double source = @double;
        Value value = source;
        bool success = value.TryGetValue(out double? result);
        Assert.True(success);
        Assert.Equal(@double, result);

        Assert.Equal(@double, (double)value);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void BoxedDouble(double @double)
    {
        double i = @double;
        object o = i;
        Value value = Value.Create(o);

        Assert.Equal(typeof(double), value.Type);
        Assert.True(value.TryGetValue(out double result));
        Assert.Equal(@double, result);
        Assert.True(value.TryGetValue(out double? nullableResult));
        Assert.Equal(@double, nullableResult!.Value);


        double? n = @double;
        o = n;
        value = Value.Create(o);

        Assert.Equal(typeof(double), value.Type);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@double, result);
        Assert.True(value.TryGetValue(out nullableResult));
        Assert.Equal(@double, nullableResult!.Value);
    }

    [Fact]
    public void NullDouble()
    {
        double? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<double?>());
        Assert.False(value.As<double?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(DoubleData))]
    public void OutAsObject(double @double)
    {
        Value value = @double;
        object o = value.As<object>();
        Assert.Equal(typeof(double), o.GetType());
        Assert.Equal(@double, (double)o);

        double? n = @double;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(double), o.GetType());
        Assert.Equal(@double, (double)o);
    }
}
