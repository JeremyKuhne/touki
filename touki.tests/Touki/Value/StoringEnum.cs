﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringEnum
{
    [Fact]
    public void BasicFunctionality()
    {
        InitType();
        DayOfWeek day = DayOfWeek.Monday;

        MemoryWatch watch = MemoryWatch.Create;
        Value value = Value.Create(day);
        DayOfWeek outDay = value.As<DayOfWeek>();
        watch.Validate();

        Assert.Equal(day, outDay);
        Assert.Equal(typeof(DayOfWeek), value.Type);
    }

    [Fact]
    public void NullableEnum()
    {
        DayOfWeek? day = DayOfWeek.Monday;

        Value value = Value.Create(day);
        DayOfWeek outDay = value.As<DayOfWeek>();

        Assert.Equal(day.Value, outDay);
        Assert.Equal(typeof(DayOfWeek), value.Type);
    }

    [Fact]
    public void ToFromNullableEnum()
    {
        DayOfWeek day = DayOfWeek.Monday;
        Value value = Value.Create(day);
        Assert.True(value.TryGetValue(out DayOfWeek? nullDay));
        Assert.Equal(day, nullDay);

        value = Value.Create((DayOfWeek?)day);
        Assert.True(value.TryGetValue(out DayOfWeek outDay));
        Assert.Equal(day, outDay);
    }

    [Fact]
    public void BoxedEnum()
    {
        DayOfWeek day = DayOfWeek.Monday;
        Value value = Value.Create((object)day);
        Assert.True(value.TryGetValue(out DayOfWeek? nullDay));
        Assert.Equal(day, nullDay);

        value = Value.Create((object)(DayOfWeek?)day);
        Assert.True(value.TryGetValue(out DayOfWeek outDay));
        Assert.Equal(day, outDay);
    }

    [Theory]
    [InlineData(ByteEnum.MinValue)]
    [InlineData(ByteEnum.MaxValue)]
    public void ByteSize(ByteEnum @enum)
    {
        Value value = Value.Create(@enum);
        Assert.True(value.TryGetValue(out ByteEnum result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out ByteEnum? nullResult));
        Assert.Equal(@enum, nullResult!.Value);
        value = Value.Create((ByteEnum?)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);

        // Create boxed
        value = Value.Create((object)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);
        value = Value.Create((object)(ByteEnum?)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);
    }

    [Theory]
    [InlineData(ShortEnum.MinValue)]
    [InlineData(ShortEnum.MaxValue)]
    public void ShortSize(ShortEnum @enum)
    {
        Value value = Value.Create(@enum);
        Assert.True(value.TryGetValue(out ShortEnum result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out ShortEnum? nullResult));
        Assert.Equal(@enum, nullResult!.Value);
        value = Value.Create((ShortEnum?)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);

        // Create boxed
        value = Value.Create((object)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);
        value = Value.Create((object)(ShortEnum?)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);
    }

    [Theory]
    [InlineData(LongEnum.MinValue)]
    [InlineData(LongEnum.MaxValue)]
    public void LongSize(LongEnum @enum)
    {
        Value value = Value.Create(@enum);
        Assert.True(value.TryGetValue(out LongEnum result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out LongEnum? nullResult));
        Assert.Equal(@enum, nullResult!.Value);
        value = Value.Create((LongEnum?)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);

        // Create boxed
        value = Value.Create((object)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);
        value = Value.Create((object)(LongEnum?)@enum);
        Assert.True(value.TryGetValue(out result));
        Assert.Equal(@enum, result);
        Assert.True(value.TryGetValue(out nullResult));
        Assert.Equal(@enum, nullResult!.Value);
    }

    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    internal static DayOfWeek InitType()
    {
        DayOfWeek day = DayOfWeek.Monday;
        return Value.Create(day).As<DayOfWeek>();
    }

    public enum ByteEnum : byte
    {
        MinValue = byte.MinValue,
        MaxValue = byte.MaxValue
    }

    public enum ShortEnum : short
    {
        MinValue = short.MinValue,
        MaxValue = short.MaxValue
    }

    public enum LongEnum : long
    {
        MinValue = long.MinValue,
        MaxValue = long.MaxValue
    }
}
