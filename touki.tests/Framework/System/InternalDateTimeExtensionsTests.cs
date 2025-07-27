// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

public class InternalDateTimeExtensionsTests
{
    // GetDate tests
    [Fact]
    public void GetDate_StandardDate_ReturnsCorrectComponents()
    {
        DateTime date = new(2023, 5, 15);

        date.GetDate(out int year, out int month, out int day);

        year.Should().Be(2023);
        month.Should().Be(5);
        day.Should().Be(15);
    }

    [Fact]
    public void GetDate_MinValue_ReturnsCorrectComponents()
    {
        DateTime date = DateTime.MinValue;

        date.GetDate(out int year, out int month, out int day);

        year.Should().Be(1);
        month.Should().Be(1);
        day.Should().Be(1);
    }

    [Fact]
    public void GetDate_MaxValue_ReturnsCorrectComponents()
    {
        DateTime date = DateTime.MaxValue;

        date.GetDate(out int year, out int month, out int day);

        year.Should().Be(9999);
        month.Should().Be(12);
        day.Should().Be(31);
    }

    [Fact]
    public void GetDate_LeapYear_ReturnsCorrectComponents()
    {
        DateTime date = new(2024, 2, 29);

        date.GetDate(out int year, out int month, out int day);

        year.Should().Be(2024);
        month.Should().Be(2);
        day.Should().Be(29);
    }

    // GetTime tests (hour, minute, second)
    [Fact]
    public void GetTime_StandardTime_ReturnsCorrectComponents()
    {
        DateTime time = new(2023, 1, 1, 14, 30, 45);

        time.GetTime(out int hour, out int minute, out int second);

        hour.Should().Be(14);
        minute.Should().Be(30);
        second.Should().Be(45);
    }

    [Fact]
    public void GetTime_Midnight_ReturnsZeroComponents()
    {
        DateTime time = new(2023, 1, 1, 0, 0, 0);

        time.GetTime(out int hour, out int minute, out int second);

        hour.Should().Be(0);
        minute.Should().Be(0);
        second.Should().Be(0);
    }

    [Fact]
    public void GetTime_BeforeMidnight_ReturnsCorrectComponents()
    {
        DateTime time = new(2023, 1, 1, 23, 59, 59);

        time.GetTime(out int hour, out int minute, out int second);

        hour.Should().Be(23);
        minute.Should().Be(59);
        second.Should().Be(59);
    }

    // GetTime tests (hour, minute, second, millisecond)
    [Fact]
    public void GetTimeWithMillisecond_StandardTime_ReturnsCorrectComponents()
    {
        DateTime time = new(2023, 1, 1, 14, 30, 45, 500);

        time.GetTime(out int hour, out int minute, out int second, out int millisecond);

        hour.Should().Be(14);
        minute.Should().Be(30);
        second.Should().Be(45);
        millisecond.Should().Be(500);
    }

    [Fact]
    public void GetTimeWithMillisecond_Midnight_ReturnsZeroComponents()
    {
        DateTime time = new(2023, 1, 1, 0, 0, 0, 0);

        time.GetTime(out int hour, out int minute, out int second, out int millisecond);

        hour.Should().Be(0);
        minute.Should().Be(0);
        second.Should().Be(0);
        millisecond.Should().Be(0);
    }

    [Fact]
    public void GetTimeWithMillisecond_MaxMillisecond_ReturnsCorrectComponents()
    {
        DateTime time = new(2023, 1, 1, 12, 30, 45, 999);

        time.GetTime(out int hour, out int minute, out int second, out int millisecond);

        hour.Should().Be(12);
        minute.Should().Be(30);
        second.Should().Be(45);
        millisecond.Should().Be(999);
    }

    // GetTimePrecise tests
    [Fact]
    public void GetTimePrecise_Midnight_ReturnsZeroComponents()
    {
        DateTime time = new(2023, 1, 1, 0, 0, 0);

        time.GetTimePrecise(out int hour, out int minute, out int second, out int tick);

        hour.Should().Be(0);
        minute.Should().Be(0);
        second.Should().Be(0);
        tick.Should().Be(0);
    }
}
