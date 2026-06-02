// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringEnum
{
    [Test]
    public void BasicFunctionality()
    {
        InitType();
        DayOfWeek day = DayOfWeek.Monday;

        MemoryWatch watch = MemoryWatch.Create;
        Value value = Value.Create(day);
        DayOfWeek outDay = value.As<DayOfWeek>();
        watch.Validate();

        outDay.Should().Be(day);
        value.Type.Should().Be(typeof(DayOfWeek));
    }

    [Test]
    public void NullableEnum()
    {
        DayOfWeek? day = DayOfWeek.Monday;

        Value value = Value.Create(day);
        DayOfWeek outDay = value.As<DayOfWeek>();

        outDay.Should().Be(day.Value);
        value.Type.Should().Be(typeof(DayOfWeek));
    }

    [Test]
    public void ToFromNullableEnum()
    {
        DayOfWeek day = DayOfWeek.Monday;
        Value value = Value.Create(day);
        value.TryGetValue(out DayOfWeek? nullDay).Should().BeTrue();
        nullDay.Should().Be(day);

        value = Value.Create((DayOfWeek?)day);
        value.TryGetValue(out DayOfWeek outDay).Should().BeTrue();
        outDay.Should().Be(day);
    }

    [Test]
    public void BoxedEnum()
    {
        DayOfWeek day = DayOfWeek.Monday;
        Value value = Value.Create((object)day);
        value.TryGetValue(out DayOfWeek? nullDay).Should().BeTrue();
        nullDay.Should().Be(day);

        value = Value.Create((object)(DayOfWeek?)day);
        value.TryGetValue(out DayOfWeek outDay).Should().BeTrue();
        outDay.Should().Be(day);
    }

    [Test]
    [Arguments(ByteEnum.MinValue)]
    [Arguments(ByteEnum.MaxValue)]
    public void ByteSize(ByteEnum @enum)
    {
        Value value = Value.Create(@enum);
        value.TryGetValue(out ByteEnum result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out ByteEnum? nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
        value = Value.Create((ByteEnum?)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);

        // Create boxed
        value = Value.Create((object)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
        value = Value.Create((object)(ByteEnum?)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
    }

    [Test]
    [Arguments(ShortEnum.MinValue)]
    [Arguments(ShortEnum.MaxValue)]
    public void ShortSize(ShortEnum @enum)
    {
        Value value = Value.Create(@enum);
        value.TryGetValue(out ShortEnum result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out ShortEnum? nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
        value = Value.Create((ShortEnum?)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);

        // Create boxed
        value = Value.Create((object)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
        value = Value.Create((object)(ShortEnum?)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
    }

    [Test]
    [Arguments(LongEnum.MinValue)]
    [Arguments(LongEnum.MaxValue)]
    public void LongSize(LongEnum @enum)
    {
        Value value = Value.Create(@enum);
        value.TryGetValue(out LongEnum result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out LongEnum? nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
        value = Value.Create((LongEnum?)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);

        // Create boxed
        value = Value.Create((object)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
        value = Value.Create((object)(LongEnum?)@enum);
        value.TryGetValue(out result).Should().BeTrue();
        result.Should().Be(@enum);
        value.TryGetValue(out nullResult).Should().BeTrue();
        nullResult!.Value.Should().Be(@enum);
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
