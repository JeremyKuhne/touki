// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace System.Globalization;

/// <summary>
///  Tests for the <see cref="DateTimeFormat"/> polyfill on .NET Framework. Many
///  cases drive the polyfill side-by-side with the BCL's
///  <see cref="DateTime.ToString(string, IFormatProvider)"/> as an oracle and
///  assert byte-identical output. Where the polyfill is intentionally narrower
///  (e.g. it does not currently handle every full standard expansion the BCL
///  does), tests assert the documented behavior directly.
/// </summary>
public class DateTimeFormatTests
{
    private static readonly DateTime s_sample = new(2026, 5, 6, 14, 23, 45, 678, DateTimeKind.Utc);
    private static readonly DateTime s_morning = new(2026, 1, 7, 3, 4, 5, DateTimeKind.Local);
    private static readonly DateTime s_unspec = new(2026, 12, 31, 23, 59, 59, 999, DateTimeKind.Unspecified);

    private static string Format(DateTime dateTime, string format, IFormatProvider? provider)
    {
        ValueStringBuilder builder = new(stackalloc char[128]);
        try
        {
            DateTimeFormat.Format(dateTime, format.AsSpan(), provider, ref builder);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static string Format(DateTime dateTime, string format, TimeSpan offset, IFormatProvider? provider)
    {
        ValueStringBuilder builder = new(stackalloc char[128]);
        try
        {
            DateTimeFormat.Format(dateTime, format.AsSpan(), provider, offset, ref builder);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    public static IEnumerable<object[]> CustomFormat_TestData() =>
        new List<object[]>
        {
            new object[] { "yyyy-MM-dd" },
            new object[] { "yyyy/MM/dd HH:mm:ss" },
            new object[] { "yy-M-d" },
            new object[] { "MMM dd, yyyy" },
            new object[] { "MMMM dd, yyyy" },
            new object[] { "ddd, dd MMM yyyy HH:mm:ss" },
            new object[] { "dddd" },
            new object[] { "h:mm tt" },
            new object[] { "hh:mm:ss.fff" },
            new object[] { "HH:mm:ss.fffffff" },
            new object[] { "yyyyMMddHHmmss" },
            new object[] { "yyyy" },
            new object[] { "MM" },
            new object[] { "dd" },
            new object[] { "ff" },
            new object[] { "fff" },
            new object[] { "ffff" },
            new object[] { "fffff" },
            new object[] { "ffffff" },
            new object[] { "fffffff" },
            new object[] { "FF" },
            new object[] { "FFF" },
            new object[] { "FFFF" },
            new object[] { "g yyyy" },      // era token in a custom format
            new object[] { "gg yyyy" },     // era token, double-letter form
            new object[] { @"yyyy\-MM\-dd" },
            new object[] { "'literal' yyyy" },
            new object[] { "\"quoted\" yyyy" },
            new object[] { "%h" },          // single-char custom format
            new object[] { "%H" },
            new object[] { "%t" },
            new object[] { "%y" },
        };

    [Test]
    [MethodDataSource(nameof(CustomFormat_TestData))]
    public void Format_Custom_MatchesBcl_InvariantCulture(string format)
    {
        // Polyfill output must equal BCL output for invariant culture across the common token set.
        Format(s_sample, format, CultureInfo.InvariantCulture)
            .Should().Be(s_sample.ToString(format, CultureInfo.InvariantCulture));
    }

    [Test]
    [MethodDataSource(nameof(CustomFormat_TestData))]
    public void Format_Custom_MatchesBcl_EnUs(string format)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo("en-US");
        Format(s_sample, format, culture).Should().Be(s_sample.ToString(format, culture));
    }

    [Test]
    [Arguments("uk-UA")]   // genitive month names
    [Arguments("ru-RU")]   // genitive month names
    [Arguments("fr-FR")]   // accented month names
    [Arguments("de-DE")]
    [Arguments("ja-JP")]
    public void Format_Custom_MatchesBcl_AcrossCultures(string cultureName)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        // Use a date that exercises a genitive form (day before month).
        const string FormatString = "d MMMM yyyy";
        Format(s_sample, FormatString, culture).Should().Be(s_sample.ToString(FormatString, culture));
    }

    public static IEnumerable<object[]> StandardFormat_TestData() =>
        new List<object[]>
        {
            new object[] { "d" },
            new object[] { "D" },
            new object[] { "f" },
            new object[] { "F" },
            new object[] { "g" },
            new object[] { "G" },
            new object[] { "m" },
            new object[] { "M" },
            new object[] { "o" },
            new object[] { "O" },
            new object[] { "r" },
            new object[] { "R" },
            new object[] { "s" },
            new object[] { "t" },
            new object[] { "T" },
            new object[] { "u" },
            // "U" not included: invariant culture's UniversalSortableDateTimePattern adjusts the
            // displayed time to UTC, which the polyfill handles only when the input is UTC.
            new object[] { "y" },
            new object[] { "Y" },
        };

    [Test]
    [MethodDataSource(nameof(StandardFormat_TestData))]
    public void Format_Standard_MatchesBcl_InvariantCulture(string format)
    {
        Format(s_sample, format, CultureInfo.InvariantCulture)
            .Should().Be(s_sample.ToString(format, CultureInfo.InvariantCulture));
    }

    // Spot checks ported from dotnet/runtime DateTimeTests.ToString_MatchesExpected_MemberData.
    // These are explicit (DateTime, format, expected) tuples so the polyfill is validated against
    // values the runtime team chose to lock in, not just whatever the BCL happens to produce on
    // this machine.
    [Test]
    [Arguments(2714985378271158548L, DateTimeKind.Utc, "D", "Wednesday, 13 June 8604")]
    [Arguments(388901633623941264L, DateTimeKind.Unspecified, "O", "1233-05-19T15:09:22.3941264")]
    [Arguments(319688581620784322L, DateTimeKind.Utc, "d", "01/20/1014")]
    [Arguments(1633263998564961778L, DateTimeKind.Unspecified, "G", "08/10/5176 20:24:16")]
    [Arguments(1850421988142570769L, DateTimeKind.Utc, "U", "Monday, 03 October 5864 02:46:54")]
    [Arguments(2161519739750829933L, DateTimeKind.Utc, "g", "08/01/6850 22:59")]
    [Arguments(94926719545582445L, DateTimeKind.Unspecified, "g", "10/24/0301 21:19")]
    [Arguments(1345442651123205077L, DateTimeKind.Utc, "U", "Saturday, 16 July 4264 06:58:32")]
    [Arguments(1683269053145633504L, DateTimeKind.Unspecified, "f", "Wednesday, 26 January 5335 01:41")]
    [Arguments(261818716531476839L, DateTimeKind.Utc, "G", "09/02/0830 22:07:33")]
    [Arguments(149735664893692740L, DateTimeKind.Unspecified, "m", "June 30")]
    [Arguments(2552572811382202194L, DateTimeKind.Unspecified, "D", "Wednesday, 12 October 8089")]
    [Arguments(794982031942527306L, DateTimeKind.Utc, "o", "2520-03-14T02:13:14.2527306Z")]
    [Arguments(2146466025818766443L, DateTimeKind.Utc, "o", "6802-11-18T16:16:21.8766443Z")]
    [Arguments(806709007011133014L, DateTimeKind.Unspecified, "t", "23:31")]
    [Arguments(2916204299343097820L, DateTimeKind.Unspecified, "F", "Friday, 31 January 9242 10:58:54")]
    [Arguments(2540972632026940446L, DateTimeKind.Utc, "U", "Wednesday, 08 January 8053 13:06:42")]
    [Arguments(316446896574206081L, DateTimeKind.Unspecified, "R", "Thu, 13 Oct 1003 23:34:17 GMT")]
    [Arguments(1352087970149786791L, DateTimeKind.Unspecified, "s", "4285-08-06T15:10:14")]
    [Arguments(975348914587607928L, DateTimeKind.Unspecified, "R", "Mon, 05 Oct 3091 01:24:18 GMT")]
    [Arguments(806691560290860158L, DateTimeKind.Utc, "T", "18:53:49")]
    [Arguments(2329057094873169055L, DateTimeKind.Unspecified, "t", "22:24")]
    [Arguments(40244582424527696L, DateTimeKind.Utc, "m", "July 13")]
    [Arguments(1502152713607918360L, DateTimeKind.Utc, "d", "02/18/4761")]
    [Arguments(230701341483195296L, DateTimeKind.Unspecified, "T", "10:35:48")]
    [Arguments(2946266365850485700L, DateTimeKind.Utc, "D", "Tuesday, 07 May 9337")]
    [Arguments(322635236878311096L, DateTimeKind.Utc, "u", "1023-05-24 09:54:47Z")]
    [Arguments(381748720453740183L, DateTimeKind.Unspecified, "D", "Saturday, 18 September 1210")]
    [Arguments(42694710897975892L, DateTimeKind.Unspecified, "g", "04/18/0136 04:11")]
    [Arguments(2889335867722033047L, DateTimeKind.Unspecified, "t", "17:39")]
    [Arguments(1108255955917223459L, DateTimeKind.Unspecified, "u", "3512-12-04 15:39:51Z")]
    [Arguments(102597329933554815L, DateTimeKind.Utc, "s", "0326-02-13T21:49:53")]
    [Arguments(1316597307220179904L, DateTimeKind.Utc, "y", "4173 February")]
    [Arguments(79516486664227528L, DateTimeKind.Unspecified, "y", "0252 December")]
    public void Format_KnownExpected_MatchesRuntime(long ticks, DateTimeKind kind, string format, string expected)
    {
        DateTime dateTime = new(ticks, kind);
        Format(dateTime, format, CultureInfo.InvariantCulture).Should().Be(expected);
    }

    [Test]
    public void Format_NonAsciiInFormatString_PreservedLiteral()
    {
        // From runtime test data: U+202D (LEFT-TO-RIGHT OVERRIDE) between HH and mm must be
        // emitted as a literal, not interpreted.
        DateTime dt = new(2023, 04, 17, 10, 46, 12, DateTimeKind.Utc);
        Format(dt, "HH\u202dmm", CultureInfo.InvariantCulture).Should().Be("10\u202d46");
    }

    [Test]
    public void Format_MinValueAndMaxValue_RoundTripPattern_MatchesBcl()
    {
        DateTime min = DateTime.MinValue;
        DateTime max = DateTime.MaxValue;
        Format(min, "o", CultureInfo.InvariantCulture).Should().Be(min.ToString("o", CultureInfo.InvariantCulture));
        Format(max, "o", CultureInfo.InvariantCulture).Should().Be(max.ToString("o", CultureInfo.InvariantCulture));
    }

    [Test]
    [Arguments("yyy")]
    [Arguments("yyyyy")]
    [Arguments("yyyyyy")]
    [Arguments("yyyyyyy")]
    public void Format_YearPaddingWidths_MatchBcl(string format)
    {
        // Custom yN format pads/truncates the era year to N digits. Touki's polyfill must match.
        DateTime dt = new(1234, 5, 6);
        Format(dt, format, CultureInfo.InvariantCulture)
            .Should().Be(dt.ToString(format, CultureInfo.InvariantCulture));
    }

    [Test]
    public void Format_TimeZoneTokenZ_ContainsOffsetSign()
    {
        // Custom format with z/zz/zzz produces a signed offset.
        DateTime utc = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        string formatted = Format(utc, "HH:mm zzz", CultureInfo.InvariantCulture);
        formatted.Should().Match(s => s.Contains('+') || s.Contains('-'));
    }

    [Test]
    public void Format_RoundtripK_OnUtcDateTime_AppendsZ()
    {
        DateTime utc = new(2026, 5, 6, 14, 0, 0, DateTimeKind.Utc);
        Format(utc, "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture)
            .Should().Be(utc.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
    }

    [Test]
    public void Format_RoundtripK_OnUnspecifiedDateTime_OmitsZone()
    {
        Format(s_unspec, "yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture)
            .Should().Be(s_unspec.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture));
    }

    [Test]
    public void Format_RoundtripK_WithExplicitOffset_AppendsOffset()
    {
        DateTime local = new(2026, 5, 6, 14, 0, 0, DateTimeKind.Unspecified);
        TimeSpan offset = TimeSpan.FromHours(5.5);
        string formatted = Format(local, "yyyy-MM-ddTHH:mm:ssK", offset, CultureInfo.InvariantCulture);
        formatted.Should().EndWith("+05:30");
    }

    [Test]
    public void Format_TimeFractionWithFCapital_TrimsTrailingZeros()
    {
        // The capital 'F' specifier omits trailing zero fractional digits.
        DateTime dt = new(2026, 1, 1, 0, 0, 0, 100, DateTimeKind.Unspecified); // .100 ms
        Format(dt, "ss.FFF", CultureInfo.InvariantCulture)
            .Should().Be(dt.ToString("ss.FFF", CultureInfo.InvariantCulture));
    }

    [Test]
    public void Format_HourSpecifierH_TwentyFourHourClockNoLeadingZero()
    {
        // s_morning has hour=3; %H must produce "3" not "03".
        Format(s_morning, "%H", CultureInfo.InvariantCulture).Should().Be("3");
    }

    [Test]
    public void Format_HourSpecifierLowerH_TwelveHourClock()
    {
        // 14:23 in 12-hour form is 2.
        Format(s_sample, "%h", CultureInfo.InvariantCulture).Should().Be("2");
    }

    [Test]
    public void Format_Tt_Designator_PmAm()
    {
        DateTime pm = new(2026, 1, 1, 14, 0, 0);
        DateTime am = new(2026, 1, 1, 9, 0, 0);
        Format(pm, "tt", CultureInfo.InvariantCulture).Should().Be("PM");
        Format(am, "tt", CultureInfo.InvariantCulture).Should().Be("AM");
    }

    [Test]
    public void Format_DayName_FullAndAbbreviated()
    {
        Format(s_sample, "ddd", CultureInfo.InvariantCulture)
            .Should().Be(s_sample.ToString("ddd", CultureInfo.InvariantCulture));
        Format(s_sample, "dddd", CultureInfo.InvariantCulture)
            .Should().Be(s_sample.ToString("dddd", CultureInfo.InvariantCulture));
    }

    [Test]
    public void Format_QuotedLiteral_PreservedInOutput()
    {
        Format(s_sample, "'today is' yyyy-MM-dd", CultureInfo.InvariantCulture)
            .Should().Be("today is 2026-05-06");
    }

    [Test]
    public void Format_DoubleQuotedLiteral_PreservedInOutput()
    {
        Format(s_sample, "\"yr\" yyyy", CultureInfo.InvariantCulture).Should().Be("yr 2026");
    }

    [Test]
    public void Format_BackslashEscape_PreservesNextChar()
    {
        Format(s_sample, @"yyyy\Tdd", CultureInfo.InvariantCulture).Should().Be("2026T06");
    }

    [Test]
    public void Format_HebrewCalendar_MonthName_EmitsHebrewLetters()
    {
        // Drives FormatHebrewMonthName, which is currently 0% covered.
        CultureInfo culture = new("he-IL");
        culture.DateTimeFormat.Calendar = new HebrewCalendar();
        DateTime date = new(2026, 5, 1);
        string formatted = Format(date, "MMMM", culture);
        formatted.Should().NotBeEmpty();
        formatted.Any(c => c is >= '\x05d0' and <= '\x05ea').Should().BeTrue(
            "Hebrew calendar month name must contain Hebrew letters");
    }

    [Test]
    public void Format_HebrewCalendar_FullDate_EmitsGematria()
    {
        CultureInfo culture = new("he-IL");
        culture.DateTimeFormat.Calendar = new HebrewCalendar();
        DateTime date = new(2026, 5, 1);
        string formatted = Format(date, "dd MM yyyy", culture);
        formatted.Should().NotBeEmpty();
        formatted.Any(c => c is >= '\x05d0' and <= '\x05ea').Should().BeTrue();
    }

    [Test]
    public void Format_UnterminatedSingleQuote_ThrowsFormatException()
    {
        // ParseQuoteString reaches end-of-format without finding the matching quote.
        Action act = () => Format(s_sample, "'unterminated", CultureInfo.InvariantCulture);
        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Format_UnterminatedDoubleQuote_ThrowsFormatException()
    {
        Action act = () => Format(s_sample, "\"unterminated", CultureInfo.InvariantCulture);
        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Format_BackslashAtEndOfQuotedString_ThrowsFormatException()
    {
        // The format ends with the escape character inside a quoted literal:
        // ParseQuoteString consumes the escape, then has no following character.
        Action act = () => Format(s_sample, "'abc\\", CultureInfo.InvariantCulture);
        act.Should().Throw<FormatException>();
    }

    [Test]
    public void Format_EscapedQuoteInsideQuote_PreservesQuoteCharacter()
    {
        // The runtime comment block on ParseQuoteString documents this exact case:
        //   "'minute:' mm\""  =>  minute: 45"
        Format(s_sample, "'minute:' mm\\\"", CultureInfo.InvariantCulture).Should().Be("minute: 23\"");
    }
}
