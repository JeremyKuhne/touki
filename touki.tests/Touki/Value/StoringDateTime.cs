// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.ValueTests;

public class StoringDateTime
{
    public static TheoryData<DateTime> DateTimeData => new()
    {
        { DateTime.Now },
        { DateTime.UtcNow },
        { DateTime.MaxValue },
        { DateTime.MinValue }
    };

    [Theory]
    [MemberData(nameof(DateTimeData))]
    public void DateTimeImplicit(DateTime dateTime)
    {
        Value value = dateTime;
        Assert.Equal(dateTime, value.As<DateTime>());
        Assert.Equal(typeof(DateTime), value.Type);

        DateTime? source = dateTime;
        value = source;
        Assert.Equal(source, value.As<DateTime?>());
        Assert.Equal(typeof(DateTime), value.Type);
    }

    [Theory]
    [MemberData(nameof(DateTimeData))]
    public void DateTimeInOut(DateTime dateTime)
    {
        Value value = dateTime;
        bool success = value.TryGetValue(out DateTime result);
        Assert.True(success);
        Assert.Equal(dateTime, result);

        Assert.Equal(dateTime, value.As<DateTime>());
        Assert.Equal(dateTime, (DateTime)value);
    }

    [Theory]
    [MemberData(nameof(DateTimeData))]
    public void NullableDateTimeInDateTimeOut(DateTime dateTime)
    {
        DateTime? source = dateTime;
        Value value = source;

        bool success = value.TryGetValue(out DateTime result);
        Assert.True(success);
        Assert.Equal(dateTime, result);

        Assert.Equal(dateTime, value.As<DateTime>());

        Assert.Equal(dateTime, (DateTime)value);
    }

    [Theory]
    [MemberData(nameof(DateTimeData))]
    public void DateTimeInNullableDateTimeOut(DateTime dateTime)
    {
        DateTime source = dateTime;
        Value value = source;
        bool success = value.TryGetValue(out DateTime? result);
        Assert.True(success);
        Assert.Equal(dateTime, result);

        Assert.Equal(dateTime, (DateTime?)value);
    }

    [Fact]
    public void NullDateTime()
    {
        DateTime? source = null;
        Value value = source;
        Assert.Null(value.Type);
        Assert.Equal(source, value.As<DateTime?>());
        Assert.False(value.As<DateTime?>().HasValue);
    }

    [Theory]
    [MemberData(nameof(DateTimeData))]
    public void OutAsObject(DateTime dateTime)
    {
        Value value = dateTime;
        object o = value.As<object>();
        Assert.Equal(typeof(DateTime), o.GetType());
        Assert.Equal(dateTime, (DateTime)o);

        DateTime? n = dateTime;
        value = n;
        o = value.As<object>();
        Assert.Equal(typeof(DateTime), o.GetType());
        Assert.Equal(dateTime, (DateTime)o);
    }
}
