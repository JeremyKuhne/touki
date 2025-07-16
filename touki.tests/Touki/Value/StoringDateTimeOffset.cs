// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringDateTimeOffset
{
    public static TheoryData<DateTimeOffset> DateTimeOffsetData => new()
    {
        { DateTimeOffset.Now },
        { DateTimeOffset.UtcNow },
        { DateTimeOffset.MaxValue },
        { DateTimeOffset.MinValue }
    };

    [Theory]
    [MemberData(nameof(DateTimeOffsetData))]
    public void DateTimeOffsetImplicit(DateTimeOffset dateTimeOffset)
    {
        Value value = dateTimeOffset;
        Assert.Equal(dateTimeOffset, value.As<DateTimeOffset>());
        Assert.Equal(typeof(DateTimeOffset), value.Type);

        DateTimeOffset? source = dateTimeOffset;
        value = source;
        Assert.Equal(source, value.As<DateTimeOffset?>());
        Assert.Equal(typeof(DateTimeOffset), value.Type);
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetData))]
    public void DateTimeOffsetInOut(DateTimeOffset dateTimeOffset)
    {
        Value value = dateTimeOffset;
        bool success = value.TryGetValue(out DateTimeOffset result);
        Assert.True(success);
        Assert.Equal(dateTimeOffset, result);

        Assert.Equal(dateTimeOffset, value.As<DateTimeOffset>());
        Assert.Equal(dateTimeOffset, (DateTimeOffset)value);
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetData))]
    public void NullableDateTimeOffsetInDateTimeOffsetOut(DateTimeOffset dateTimeOffset)
    {
        DateTimeOffset? source = dateTimeOffset;
        Value value = source;

        bool success = value.TryGetValue(out DateTimeOffset result);
        Assert.True(success);
        Assert.Equal(dateTimeOffset, result);

        Assert.Equal(dateTimeOffset, value.As<DateTimeOffset>());

        Assert.Equal(dateTimeOffset, (DateTimeOffset)value);
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetData))]
    public void DateTimeOffsetInNullableDateTimeOffsetOut(DateTimeOffset dateTimeOffset)
    {
        DateTimeOffset source = dateTimeOffset;
        Value value = source;
        bool success = value.TryGetValue(out DateTimeOffset? result);
        Assert.True(success);
        Assert.Equal(dateTimeOffset, result);

        Assert.Equal(dateTimeOffset, (DateTimeOffset?)value);
    }

    [Fact]
    public void NullDateTimeOffset()
    {
        DateTimeOffset? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<DateTimeOffset?>());
        Assert.False(value.As<DateTimeOffset?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(DateTimeOffsetData))]
    public void OutAsObject(DateTimeOffset dateTimeOffset)
    {
        Value value = dateTimeOffset;
        object o = value.As<object>();
        Assert.Equal(typeof(DateTimeOffset), o.GetType());
        Assert.Equal(dateTimeOffset, (DateTimeOffset)o);

        DateTimeOffset? n = dateTimeOffset;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(DateTimeOffset), o.GetType());
        Assert.Equal(dateTimeOffset, (DateTimeOffset)o);
    }
}
