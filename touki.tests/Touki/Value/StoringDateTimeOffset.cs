// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class StoringDateTimeOffset
{
    public static IEnumerable<DateTimeOffset> DateTimeOffsetData()
    {
        yield return DateTimeOffset.Now;
        yield return DateTimeOffset.UtcNow;
        yield return DateTimeOffset.MaxValue;
        yield return DateTimeOffset.MinValue;
    }

    [Test]
    [MethodDataSource(nameof(DateTimeOffsetData))]
    public void DateTimeOffsetImplicit(DateTimeOffset dateTimeOffset)
    {
        Value value = dateTimeOffset;
        value.As<DateTimeOffset>().Should().Be(dateTimeOffset);
        value.Type.Should().Be(typeof(DateTimeOffset));

        DateTimeOffset? source = dateTimeOffset;
        value = source;
        value.As<DateTimeOffset?>().Should().Be(source);
        value.Type.Should().Be(typeof(DateTimeOffset));
    }

    [Test]
    [MethodDataSource(nameof(DateTimeOffsetData))]
    public void DateTimeOffsetInOut(DateTimeOffset dateTimeOffset)
    {
        Value value = dateTimeOffset;
        bool success = value.TryGetValue(out DateTimeOffset result);
        success.Should().BeTrue();
        result.Should().Be(dateTimeOffset);

        value.As<DateTimeOffset>().Should().Be(dateTimeOffset);
        ((DateTimeOffset)value).Should().Be(dateTimeOffset);
    }

    [Test]
    [MethodDataSource(nameof(DateTimeOffsetData))]
    public void NullableDateTimeOffsetInDateTimeOffsetOut(DateTimeOffset dateTimeOffset)
    {
        DateTimeOffset? source = dateTimeOffset;
        Value value = source;

        bool success = value.TryGetValue(out DateTimeOffset result);
        success.Should().BeTrue();
        result.Should().Be(dateTimeOffset);

        value.As<DateTimeOffset>().Should().Be(dateTimeOffset);

        ((DateTimeOffset)value).Should().Be(dateTimeOffset);
    }

    [Test]
    [MethodDataSource(nameof(DateTimeOffsetData))]
    public void DateTimeOffsetInNullableDateTimeOffsetOut(DateTimeOffset dateTimeOffset)
    {
        DateTimeOffset source = dateTimeOffset;
        Value value = source;
        bool success = value.TryGetValue(out DateTimeOffset? result);
        success.Should().BeTrue();
        result.Should().Be(dateTimeOffset);

        ((DateTimeOffset?)value).Should().Be(dateTimeOffset);
    }

    [Test]
    public void NullDateTimeOffset()
    {
        DateTimeOffset? source = null;
        Value value = source;
        value.Type.Should().BeNull();
        value.As<DateTimeOffset?>().Should().Be(source);
        value.As<DateTimeOffset?>().HasValue.Should().BeFalse();
    }

    [Test]
    [MethodDataSource(nameof(DateTimeOffsetData))]
    public void OutAsObject(DateTimeOffset dateTimeOffset)
    {
        Value value = dateTimeOffset;
        object o = value.As<object>();
        o.GetType().Should().Be(typeof(DateTimeOffset));
        ((DateTimeOffset)o).Should().Be(dateTimeOffset);

        DateTimeOffset? n = dateTimeOffset;
        value = n;
        o = value.As<object>();
        o.GetType().Should().Be(typeof(DateTimeOffset));
        ((DateTimeOffset)o).Should().Be(dateTimeOffset);
    }
}
