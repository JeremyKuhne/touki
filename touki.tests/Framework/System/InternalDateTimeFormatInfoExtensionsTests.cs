// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Globalization;

public class InternalDateTimeFormatInfoExtensionsTests
{
    [Fact]
    public void DateTimeOffsetPattern_ReturnNonEmptyString()
    {
        // Get DateTimeFormatInfo from current culture
        DateTimeFormatInfo formatInfo = CultureInfo.CurrentCulture.DateTimeFormat;

        string pattern = formatInfo.DateTimeOffsetPattern();

        pattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DateTimeOffsetPattern_DifferentCultures_ReturnsDifferentPatterns()
    {
        // Test with English and Japanese cultures
        DateTimeFormatInfo enFormatInfo = CultureInfo.GetCultureInfo("en-US").DateTimeFormat;
        DateTimeFormatInfo jpFormatInfo = CultureInfo.GetCultureInfo("ja-JP").DateTimeFormat;

        string enPattern = enFormatInfo.DateTimeOffsetPattern();
        string jpPattern = jpFormatInfo.DateTimeOffsetPattern();

        enPattern.Should().NotBeNullOrEmpty();
        jpPattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetMonthName_RegularStyle_ReturnsCorrectName()
    {
        DateTimeFormatInfo formatInfo = CultureInfo.GetCultureInfo("en-US").DateTimeFormat;

        string january = formatInfo.GetMonthName(1, MonthNameStyles.Regular, false);
        string december = formatInfo.GetMonthName(12, MonthNameStyles.Regular, false);

        january.Should().Be("January");
        december.Should().Be("December");
    }

    [Fact]
    public void GetMonthName_AbbreviatedRegularStyle_ReturnsCorrectName()
    {
        DateTimeFormatInfo formatInfo = CultureInfo.GetCultureInfo("en-US").DateTimeFormat;

        string january = formatInfo.GetMonthName(1, MonthNameStyles.Regular, true);
        string december = formatInfo.GetMonthName(12, MonthNameStyles.Regular, true);

        january.Should().Be("Jan");
        december.Should().Be("Dec");
    }

    [Fact]
    public void GetMonthName_GenitiveStyle_ReturnsName()
    {
        // Ukranian has distinct genitive forms
        DateTimeFormatInfo formatInfo = CultureInfo.GetCultureInfo("uk-UA").DateTimeFormat;
        string january = formatInfo.GetMonthName(1, MonthNameStyles.Genitive, false);
        january.Should().Be("січня");
    }

    [Fact]
    public void GetMonthName_AbbreviatedGenitiveStyle_ReturnsName()
    {
        DateTimeFormatInfo formatInfo = CultureInfo.GetCultureInfo("uk-UA").DateTimeFormat;
        string january = formatInfo.GetMonthName(1, MonthNameStyles.Genitive, true);
        january.Should().Be("січ");
    }

    [Fact]
    public void GetMonthName_LeapYearStyle_InvariantCulture_ReturnsName()
    {
        DateTimeFormatInfo formatInfo = CultureInfo.InvariantCulture.DateTimeFormat;
        string february = formatInfo.GetMonthName(2, MonthNameStyles.LeapYear, false);
        february.Should().Be("February");
    }

    [Fact]
    public void GetMonthName_InvalidMonth_ThrowsArgumentOutOfRangeException()
    {
        DateTimeFormatInfo formatInfo = CultureInfo.InvariantCulture.DateTimeFormat;

        Action action1 = () => formatInfo.GetMonthName(0, MonthNameStyles.Regular, false);
        Action action2 = () => formatInfo.GetMonthName(14, MonthNameStyles.Regular, false);

        action1.Should().Throw<ArgumentOutOfRangeException>();
        action2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void FormatFlags_Ukrainian_ReturnsGenitive()
    {
        DateTimeFormatInfo formatInfo = CultureInfo.GetCultureInfo("uk-UA").DateTimeFormat;

        // Ukrainian culture should have the Genitive month format flag
        int flags = formatInfo.FormatFlags();
        flags.Should().Be(1);
    }

    [Fact]
    public void FormatFlags_UnitedStates_ReturnsNonGenitive()
    {
        DateTimeFormatInfo usFormatInfo = CultureInfo.GetCultureInfo("en-US").DateTimeFormat;

        int usFlags = usFormatInfo.FormatFlags();
        usFlags.Should().Be(0);
    }
}
