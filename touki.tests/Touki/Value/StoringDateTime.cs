// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringDateTime
{
    public static IEnumerable<DateTime> DateTimeData()
    {
        yield return DateTime.Now;
        yield return DateTime.UtcNow;
        yield return DateTime.MaxValue;
        yield return DateTime.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(DateTimeData))]
    public void DateTimeImplicit(DateTime dateTime)
    {
        Value value = dateTime;
        value.As<DateTime>().Should().Be(dateTime);
        value.Type.Should().Be(typeof(DateTime));

        DateTime? source = dateTime;
        value = source;
        value.As<DateTime?>().Should().Be(source);
        value.Type.Should().Be(typeof(DateTime));
    }

    [Test]
    [MethodDataSource(nameof(DateTimeData))]
    public void DateTimeInOut(DateTime dateTime)
    {
        Value value = dateTime;
        bool success = value.TryGetValue(out DateTime result);
        success.Should().BeTrue();
        result.Should().Be(dateTime);

        value.As<DateTime>().Should().Be(dateTime);
        ((DateTime)value).Should().Be(dateTime);
    }

    [Test]
    [MethodDataSource(nameof(DateTimeData))]
    public void NullableDateTimeInDateTimeOut(DateTime dateTime)
    {
        DateTime? source = dateTime;
        Value value = source;

        bool success = value.TryGetValue(out DateTime result);
        success.Should().BeTrue();
        result.Should().Be(dateTime);

        value.As<DateTime>().Should().Be(dateTime);

        ((DateTime)value).Should().Be(dateTime);
    }

    [Test]
    [MethodDataSource(nameof(DateTimeData))]
    public void DateTimeInNullableDateTimeOut(DateTime dateTime)
    {
        DateTime source = dateTime;
        Value value = source;
        bool success = value.TryGetValue(out DateTime? result);
        success.Should().BeTrue();
        result.Should().Be(dateTime);

        ((DateTime?)value).Should().Be(dateTime);
    }

    [Test]
    public void NullDateTime()
    {
        DateTime? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<DateTime?>().Should().Be(source);
        value.As<DateTime?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(DateTimeData))]
    public void OutAsObject(DateTime dateTime)
    {
        Value value = dateTime;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(DateTime));
        ((DateTime)o).Should().Be(dateTime);

        DateTime? n = dateTime;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(DateTime));
        ((DateTime)o).Should().Be(dateTime);
    }
}
