﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Touki;
using Touki.Text;
using Touki.Framework.Resources;

namespace System;

/*
 Customized format patterns:
 P.S. Format in the table below is the internal number format used to display the pattern.

 Patterns   Format      Description                           Example
 =========  ==========  ===================================== ========
    "h"     "0"         hour (12-hour clock)w/o leading zero  3
    "hh"    "00"        hour (12-hour clock)with leading zero 03
    "hh*"   "00"        hour (12-hour clock)with leading zero 03

    "H"     "0"         hour (24-hour clock)w/o leading zero  8
    "HH"    "00"        hour (24-hour clock)with leading zero 08
    "HH*"   "00"        hour (24-hour clock)                  08

    "m"     "0"         minute w/o leading zero
    "mm"    "00"        minute with leading zero
    "mm*"   "00"        minute with leading zero

    "s"     "0"         second w/o leading zero
    "ss"    "00"        second with leading zero
    "ss*"   "00"        second with leading zero

    "f"     "0"         second fraction (1 digit)
    "ff"    "00"        second fraction (2 digit)
    "fff"   "000"       second fraction (3 digit)
    "ffff"  "0000"      second fraction (4 digit)
    "fffff" "00000"         second fraction (5 digit)
    "ffffff"    "000000"    second fraction (6 digit)
    "fffffff"   "0000000"   second fraction (7 digit)

    "F"     "0"         second fraction (up to 1 digit)
    "FF"    "00"        second fraction (up to 2 digit)
    "FFF"   "000"       second fraction (up to 3 digit)
    "FFFF"  "0000"      second fraction (up to 4 digit)
    "FFFFF" "00000"         second fraction (up to 5 digit)
    "FFFFFF"    "000000"    second fraction (up to 6 digit)
    "FFFFFFF"   "0000000"   second fraction (up to 7 digit)

    "t"                 first character of AM/PM designator   A
    "tt"                AM/PM designator                      AM
    "tt*"               AM/PM designator                      PM

    "d"     "0"         day w/o leading zero                  1
    "dd"    "00"        day with leading zero                 01
    "ddd"               short weekday name (abbreviation)     Mon
    "dddd"              full weekday name                     Monday
    "dddd*"             full weekday name                     Monday


    "M"     "0"         month w/o leading zero                2
    "MM"    "00"        month with leading zero               02
    "MMM"               short month name (abbreviation)       Feb
    "MMMM"              full month name                       February
    "MMMM*"             full month name                       February

    "y"     "0"         two digit year (year % 100) w/o leading zero           0
    "yy"    "00"        two digit year (year % 100) with leading zero          00
    "yyy"   "D3"        year                                  2000
    "yyyy"  "D4"        year                                  2000
    "yyyyy" "D5"        year                                  2000
    ...

    "z"     "+0;-0"     timezone offset w/o leading zero      -8
    "zz"    "+00;-00"   timezone offset with leading zero     -08
    "zzz"      "+00;-00" for hour offset, "00" for minute offset  full timezone offset   -07:30
    "zzz*"  "+00;-00" for hour offset, "00" for minute offset   full timezone offset   -08:00

    "K"    -Local       "zzz", e.g. -08:00
           -Utc         "'Z'", representing UTC
           -Unspecified ""
           -DateTimeOffset      "zzzzz" e.g -07:30:15

    "g*"                the current era name                  A.D.

    ":"                 time separator                        : -- DEPRECATED - Insert separator directly into pattern (eg: "H.mm.ss")
    "/"                 date separator                        /-- DEPRECATED - Insert separator directly into pattern (eg: "M-dd-yyyy")
    "'"                 quoted string                         'ABC' will insert ABC into the formatted string.
    '"'                 quoted string                         "ABC" will insert ABC into the formatted string.
    "%"                 used to quote a single pattern characters      E.g.The format character "%y" is to print two digit year.
    "\"                 escaped character                     E.g. '\d' insert the character 'd' into the format string.
    other characters    insert the character into the format string.

Pre-defined format characters:
    (U) to indicate Universal time is used.
    (G) to indicate Gregorian calendar is used.

    Format              Description                             Real format                             Example
    =========           =================================       ======================                  =======================
    "d"                 short date                              culture-specific                        10/31/1999
    "D"                 long data                               culture-specific                        Sunday, October 31, 1999
    "f"                 full date (long date + short time)      culture-specific                        Sunday, October 31, 1999 2:00 AM
    "F"                 full date (long date + long time)       culture-specific                        Sunday, October 31, 1999 2:00:00 AM
    "g"                 general date (short date + short time)  culture-specific                        10/31/1999 2:00 AM
    "G"                 general date (short date + long time)   culture-specific                        10/31/1999 2:00:00 AM
    "m"/"M"             Month/Day date                          culture-specific                        October 31
(G)     "o"/"O"             Round Trip XML                          "yyyy-MM-ddTHH:mm:ss.fffffffK"          1999-10-31 02:00:00.0000000Z
(G)     "r"/"R"             RFC 1123 date,                          "ddd, dd MMM yyyy HH':'mm':'ss 'GMT'"   Sun, 31 Oct 1999 10:00:00 GMT
(G)     "s"                 Sortable format, based on ISO 8601.     "yyyy-MM-dd'T'HH:mm:ss"                 1999-10-31T02:00:00
                                                                ('T' for local time)
    "t"                 short time                              culture-specific                        2:00 AM
    "T"                 long time                               culture-specific                        2:00:00 AM
(G)     "u"                 Universal time with sortable format,    "yyyy'-'MM'-'dd HH':'mm':'ss'Z'"        1999-10-31 10:00:00Z
                        based on ISO 8601.
(U)     "U"                 Universal time with full                culture-specific                        Sunday, October 31, 1999 10:00:00 AM
                        (long date + long time) format
                        "y"/"Y"             Year/Month day                          culture-specific                        October, 1999

*/

internal static class DateTimeFormat
{
    private const int MaxSecondsFractionDigits = 7;
    private const long NullOffset = long.MinValue;

    private const string RoundtripFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffffK";
    private const string RoundtripDateTimeUnfixed = "yyyy'-'MM'-'ddTHH':'mm':'ss zzz";

    private const int DEFAULT_ALL_DATETIMES_SIZE = 132;

    private const char CJKYearSuff = '\u5e74';
    private const char JapaneseEraStart = '\u5143';
    private const string Gmt = "GMT";

    // Number of 100ns (10E-7 second) ticks per time unit
    private const long TicksPerMillisecond = 10000;
    private const long TicksPerSecond = TicksPerMillisecond * 1000;
    private const long TicksPerMinute = TicksPerSecond * 60;
    private const long TicksPerHour = TicksPerMinute * 60;
    private const long TicksPerDay = TicksPerHour * 24;


    private static DateTimeFormatInfo InvariantFormatInfo { get; } = CultureInfo.InvariantCulture.DateTimeFormat;
    private static string[] InvariantAbbreviatedMonthNames { get; } = InvariantFormatInfo.AbbreviatedMonthNames;
    private static string[] InvariantAbbreviatedDayNames { get; } = InvariantFormatInfo.AbbreviatedDayNames;

    private static readonly string s_switchFormatJapaneseFirstYearAsANumber
        = "Switch.System.Globalization.FormatJapaneseFirstYearAsANumber";

    private static readonly string[] s_fixedNumberFormats =
    [
        "0",
        "00",
        "000",
        "0000",
        "00000",
        "000000",
        "0000000",
    ];

    private static readonly GregorianCalendar s_gregorianCalendar = new();


    ////////////////////////////////////////////////////////////////////////////
    //
    // Format the positive integer value to a string and prefix with assigned
    // length of leading zero.
    //
    // Parameters:
    //  value: The value to format
    //  len: The maximum length for leading zero.
    //  If the digits of the value is greater than len, no leading zero is added.
    //
    // Notes:
    //  The function can format to int.MaxValue.
    //
    ////////////////////////////////////////////////////////////////////////////
    private static void FormatDigits(ref ValueStringBuilder outputBuffer, int value, int len)
    {
        Debug.Assert(value >= 0, "DateTimeFormat.FormatDigits(): value >= 0");
        FormatDigits(ref outputBuffer, value, len, overrideLengthLimit: false);
    }

    private static unsafe void FormatDigits(ref ValueStringBuilder outputBuffer, int value, int len, bool overrideLengthLimit)
    {
        Debug.Assert(value >= 0, "DateTimeFormat.FormatDigits(): value >= 0");

        // Limit the use of this function to be two-digits, so that we have the same behavior
        // as RTM bits.
        if (!overrideLengthLimit && len > 2)
        {
            len = 2;
        }

        char* buffer = stackalloc char[16];
        char* p = buffer + 16;
        int n = value;
        do
        {
            *--p = (char)(n % 10 + '0');
            n /= 10;
        } while ((n != 0) && (p > buffer));

        int digits = (int)(buffer + 16 - p);

        // If the repeat count is greater than 0, we're trying
        // to emulate the "00" format, so we have to prepend
        // a zero if the string only has one character.
        while ((digits < len) && (p > buffer))
        {
            *--p = '0';
            digits++;
        }

        outputBuffer.Append(p, digits);
    }

    private static void HebrewFormatDigits(ref ValueStringBuilder builder, int digits)
    {
        HebrewNumber.Append(builder, digits);
    }

    private static int ParseRepeatPattern(ReadOnlySpan<char> format, int pos, char patternChar)
    {
        int len = format.Length;
        int index = pos + 1;
        while ((index < len) && (format[index] == patternChar))
        {
            index++;
        }

        return index - pos;
    }

    private static string FormatDayOfWeek(int dayOfWeek, int repeat, DateTimeFormatInfo dtfi)
    {
        Debug.Assert(dayOfWeek is >= 0 and <= 6, "dayOfWeek >= 0 && dayOfWeek <= 6");
        if (repeat == 3)
        {
            return dtfi.GetAbbreviatedDayName((DayOfWeek)dayOfWeek);
        }
        // Call dtfi.GetDayName() here, instead of accessing DayNames property, because we don't
        // want a clone of DayNames, which will hurt perf.
        return dtfi.GetDayName((DayOfWeek)dayOfWeek);
    }

    private static string FormatMonth(int month, int repeatCount, DateTimeFormatInfo dtfi)
    {
        Debug.Assert(month is >= 1 and <= 12, "month >=1 && month <= 12");
        if (repeatCount == 3)
        {
            return dtfi.GetAbbreviatedMonthName(month);
        }
        // Call GetMonthName() here, instead of accessing MonthNames property, because we don't
        // want a clone of MonthNames, which will hurt perf.
        return dtfi.GetMonthName(month);
    }

    //
    //  FormatHebrewMonthName
    //
    //  Action: Return the Hebrew month name for the specified DateTime.
    //  Returns: The month name string for the specified DateTime.
    //  Arguments:
    //        time   the time to format
    //        month  The month is the value of HebrewCalendar.GetMonth(time).
    //        repeat Return abbreviated month name if repeat=3, or full month name if repeat=4
    //        dtfi    The DateTimeFormatInfo which uses the Hebrew calendars as its calendar.
    //  Exceptions: None.
    //

    /* Note:
        If DTFI is using Hebrew calendar, GetMonthName()/GetAbbreviatedMonthName() will return month names like this:
        1   Hebrew 1st Month
        2   Hebrew 2nd Month
        ..  ...
        6   Hebrew 6th Month
        7   Hebrew 6th Month II (used only in a leap year)
        8   Hebrew 7th Month
        9   Hebrew 8th Month
        10  Hebrew 9th Month
        11  Hebrew 10th Month
        12  Hebrew 11th Month
        13  Hebrew 12th Month

        Therefore, if we are in a regular year, we have to increment the month name if month is greater or equal to 7.
    */
    private static string FormatHebrewMonthName(DateTime time, int month, int repeatCount, DateTimeFormatInfo dtfi)
    {
        Debug.Assert(repeatCount is 3 or 4, "repeatCount should be 3 or 4");
        if (dtfi.Calendar.IsLeapYear(dtfi.Calendar.GetYear(time)))
        {
            // This month is in a leap year
            return dtfi.GetMonthName(month, MonthNameStyles.LeapYear, repeatCount == 3);
        }

        // This is in a regular year.
        if (month >= 7)
        {
            month++;
        }

        return repeatCount == 3 ? dtfi.GetAbbreviatedMonthName(month) : dtfi.GetMonthName(month);
    }

    //
    // The pos should point to a quote character. This method will
    // append to the result StringBuilder the string enclosed by the quote character.
    //
    private static int ParseQuoteString(scoped ReadOnlySpan<char> format, int pos, ref ValueStringBuilder builder)
    {
        //
        // NOTE : pos will be the index of the quote character in the 'format' string.
        //
        int formatLen = format.Length;
        int beginPos = pos;
        char quoteChar = format[pos++]; // Get the character used to quote the following string.

        bool foundQuote = false;
        while (pos < formatLen)
        {
            char ch = format[pos++];
            if (ch == quoteChar)
            {
                foundQuote = true;
                break;
            }
            else if (ch == '\\')
            {
                // The following are used to support escaped character.
                // Escaped character is also supported in the quoted string.
                // Therefore, someone can use a format like "'minute:' mm\"" to display:
                //  minute: 45"
                // because the second double quote is escaped.
                if (pos < formatLen)
                {
                    builder.Append(format[pos++]);
                }
                else
                {
                    //
                    // This means that '\' is at the end of the formatting string.
                    //
                    throw new FormatException(SRF.Format_InvalidString);
                }
            }
            else
            {
                builder.Append(ch);
            }
        }

        if (!foundQuote)
        {
            // Here we can't find the matching quote.
            throw new FormatException(Strings.Format(SRF.Format_BadQuote, quoteChar));
        }

        //
        // Return the character count including the begin/end quote characters and enclosed string.
        //
        return pos - beginPos;
    }

    //
    // Get the next character at the index of 'pos' in the 'format' string.
    // Return value of -1 means 'pos' is already at the end of the 'format' string.
    // Otherwise, return value is the int value of the next character.
    //
    private static int ParseNextChar(ReadOnlySpan<char> format, int pos)
    {
        return pos >= format.Length - 1 ? -1 : format[pos + 1];
    }

    //
    //  IsUseGenitiveForm
    //
    //  Actions: Check the format to see if we should use genitive month in the formatting.
    //      Starting at the position (index) in the (format) string, look back and look ahead to
    //      see if there is "d" or "dd".  In the case like "d MMMM" or "MMMM dd", we can use
    //      genitive form.  Genitive form is not used if there is more than two "d".
    //  Arguments:
    //      format      The format string to be scanned.
    //      index       Where we should start the scanning.  This is generally where "M" starts.
    //      tokenLen    The len of the current pattern character.  This indicates how many "M" that we have.
    //      patternToMatch  The pattern that we want to search. This generally uses "d"
    //
    private static bool IsUseGenitiveForm(ReadOnlySpan<char> format, int index, int tokenLen, char patternToMatch)
    {
        int i;
        int repeat = 0;

        //
        // Look back to see if we can find "d" or "ddd"
        //

        // Find first "d".
        for (i = index - 1; i >= 0 && format[i] != patternToMatch; i--)
        {  /*Do nothing here */ }

        if (i >= 0)
        {
            // Find a "d", so look back to see how many "d" that we can find.
            while (--i >= 0 && format[i] == patternToMatch)
            {
                repeat++;
            }
            //
            // repeat == 0 means that we have one (patternToMatch)
            // repeat == 1 means that we have two (patternToMatch)
            //
            if (repeat <= 1)
            {
                return true;
            }
            // Note that we can't just stop here.  We may find "ddd" while looking back, and we have to look
            // ahead to see if there is "d" or "dd".
        }

        //
        // If we can't find "d" or "dd" by looking back, try look ahead.
        //

        // Find first "d"
        for (i = index + tokenLen; i < format.Length && format[i] != patternToMatch; i++)
        { /* Do nothing here */ }

        if (i < format.Length)
        {
            repeat = 0;
            // Find a "d", so contine the walk to see how may "d" that we can find.
            while (++i < format.Length && format[i] == patternToMatch)
            {
                repeat++;
            }
            //
            // repeat == 0 means that we have one (patternToMatch)
            // repeat == 1 means that we have two (patternToMatch)
            //
            if (repeat <= 1)
            {
                return true;
            }
        }
        return false;
    }

    //
    //  FormatCustomized
    //
    //  Actions: Format the DateTime instance using the specified format.
    //
    private static void FormatCustomized(
        DateTime dateTime,
        scoped ReadOnlySpan<char> format,
        DateTimeFormatInfo dtfi,
        TimeSpan offset,
        ref ValueStringBuilder builder)
    {
        Calendar calendar = dtfi.Calendar;
        Span<char> singleChar = stackalloc char[1];

        // This is a flag to indicate if we are formatting the dates using Hebrew calendar.
        bool isHebrewCalendar = calendar is HebrewCalendar;
        bool isJapaneseCalendar = calendar is JapaneseCalendar;

        // This is a flag to indicate if we are formatting hour/minute/second only.
        bool isTimeOnly = true;

        int i = 0;
        int tokenLen, hour12;

        while (i < format.Length)
        {
            char ch = format[i];
            int nextChar;
            switch (ch)
            {
                case 'g':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    builder.Append(dtfi.GetEraName(calendar.GetEra(dateTime)));
                    break;
                case 'h':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    hour12 = dateTime.Hour % 12;
                    if (hour12 == 0)
                    {
                        hour12 = 12;
                    }
                    FormatDigits(ref builder, hour12, tokenLen);
                    break;
                case 'H':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    FormatDigits(ref builder, dateTime.Hour, tokenLen);
                    break;
                case 'm':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    FormatDigits(ref builder, dateTime.Minute, tokenLen);
                    break;
                case 's':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    FormatDigits(ref builder, dateTime.Second, tokenLen);
                    break;
                case 'f':
                case 'F':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    if (tokenLen <= MaxSecondsFractionDigits)
                    {
                        long fraction = dateTime.Ticks % TicksPerSecond;
                        fraction /= (long)Math.Pow(10, 7 - tokenLen);
                        if (ch == 'f')
                        {
                            // CultureInfo.InvariantCulture
                            builder.AppendFormatted((int)fraction, s_fixedNumberFormats[tokenLen - 1]);
                        }
                        else
                        {
                            int effectiveDigits = tokenLen;
                            while (effectiveDigits > 0)
                            {
                                if (fraction % 10 == 0)
                                {
                                    fraction /= 10;
                                    effectiveDigits--;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            if (effectiveDigits > 0)
                            {
                                // CultureInfo.InvariantCulture
                                builder.AppendFormatted((int)fraction, s_fixedNumberFormats[effectiveDigits - 1]);
                            }
                            else
                            {
                                // No fraction to emit, so see if we should remove decimal also.
                                if (builder.Length > 0 && builder[^1] == '.')
                                {
                                    builder.Length--;
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new FormatException(SRF.Format_InvalidString);
                    }

                    break;
                case 't':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    if (tokenLen == 1)
                    {
                        if (dateTime.Hour < 12)
                        {
                            if (dtfi.AMDesignator.Length >= 1)
                            {
                                builder.Append(dtfi.AMDesignator[0]);
                            }
                        }
                        else
                        {
                            if (dtfi.PMDesignator.Length >= 1)
                            {
                                builder.Append(dtfi.PMDesignator[0]);
                            }
                        }
                    }
                    else
                    {
                        builder.Append(dateTime.Hour < 12 ? dtfi.AMDesignator : dtfi.PMDesignator);
                    }
                    break;
                case 'd':
                    //
                    // tokenLen == 1 : Day of month as digits with no leading zero.
                    // tokenLen == 2 : Day of month as digits with leading zero for single-digit months.
                    // tokenLen == 3 : Day of week as a three-letter abbreviation.
                    // tokenLen >= 4 : Day of week as its full name.
                    //
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    if (tokenLen <= 2)
                    {
                        int day = calendar.GetDayOfMonth(dateTime);
                        if (isHebrewCalendar)
                        {
                            // For Hebrew calendar, we need to convert numbers to Hebrew text for yyyy, MM, and dd values.
                            HebrewFormatDigits(ref builder, day);
                        }
                        else
                        {
                            FormatDigits(ref builder, day, tokenLen);
                        }
                    }
                    else
                    {
                        int dayOfWeek = (int)calendar.GetDayOfWeek(dateTime);
                        builder.Append(FormatDayOfWeek(dayOfWeek, tokenLen, dtfi));
                    }

                    isTimeOnly = false;
                    break;
                case 'M':
                    //
                    // tokenLen == 1 : Month as digits with no leading zero.
                    // tokenLen == 2 : Month as digits with leading zero for single-digit months.
                    // tokenLen == 3 : Month as a three-letter abbreviation.
                    // tokenLen >= 4 : Month as its full name.
                    //
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    int month = calendar.GetMonth(dateTime);
                    if (tokenLen <= 2)
                    {
                        if (isHebrewCalendar)
                        {
                            // For Hebrew calendar, we need to convert numbers to Hebrew text for yyyy, MM, and dd values.
                            HebrewFormatDigits(ref builder, month);
                        }
                        else
                        {
                            FormatDigits(ref builder, month, tokenLen);
                        }
                    }
                    else
                    {
                        if (isHebrewCalendar)
                        {
                            builder.Append(FormatHebrewMonthName(dateTime, month, tokenLen, dtfi));
                        }
                        else
                        {
                            if ((dtfi.FormatFlags() & 1 /* UseGenitiveMonth */) != 0)
                            {
                                builder.Append(
                                    dtfi.GetMonthName(
                                        month,
                                        IsUseGenitiveForm(format, i, tokenLen, 'd') ? MonthNameStyles.Genitive : MonthNameStyles.Regular,
                                        tokenLen == 3));
                            }
                            else
                            {
                                builder.Append(FormatMonth(month, tokenLen, dtfi));
                            }
                        }
                    }
                    isTimeOnly = false;
                    break;
                case 'y':
                    // Notes about OS behavior:
                    // y: Always print (year % 100). No leading zero.
                    // yy: Always print (year % 100) with leading zero.
                    // yyy/yyyy/yyyyy/... : Print year value.  No leading zero.

                    int year = calendar.GetYear(dateTime);
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    if (isJapaneseCalendar
                        && (!AppContext.TryGetSwitch(s_switchFormatJapaneseFirstYearAsANumber, out bool enabled) || !enabled)
                        && year == 1
                        && ((i + tokenLen < format.Length && format[i + tokenLen] == CJKYearSuff)
                        || (i + tokenLen < format.Length - 1
                            && format[i + tokenLen] == '\''
                            && format[i + tokenLen + 1] == CJKYearSuff)))
                    {
                        // We are formatting a Japanese date with year equals 1 and the year number is followed by the year sign \u5e74
                        // In Japanese dates, the first year in the era is not formatted as a number 1 instead it is formatted as \u5143 which means
                        // first or beginning of the era.
                        builder.Append(JapaneseEraStart);
                    }
                    else if (calendar is JapaneseCalendar or TaiwanCalendar)
                    {
                        // dtfi.HasForceTwoDigitYears
                        FormatDigits(ref builder, year, tokenLen <= 2 ? tokenLen : 2);
                    }
                    else if (isHebrewCalendar)
                    {
                        HebrewFormatDigits(ref builder, year);
                    }
                    else
                    {
                        if (tokenLen <= 2)
                        {
                            FormatDigits(ref builder, year % 100, tokenLen);
                        }
                        else if (tokenLen <= 16) // FormatDigits has an implicit 16-digit limit
                        {
                            FormatDigits(ref builder, year, tokenLen, overrideLengthLimit: true);
                        }
                        else
                        {
                            builder.Append(year.ToString("D" + tokenLen.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
                        }
                    }

                    isTimeOnly = false;
                    break;
                case 'z':
                    tokenLen = ParseRepeatPattern(format, i, ch);
                    FormatCustomizedTimeZone(dateTime, offset, tokenLen, isTimeOnly, ref builder);
                    break;
                case 'K':
                    tokenLen = 1;
                    FormatCustomizedRoundripTimeZone(dateTime, offset, ref builder);
                    break;
                case ':':
                    builder.Append(dtfi.TimeSeparator);
                    tokenLen = 1;
                    break;
                case '/':
                    builder.Append(dtfi.DateSeparator);
                    tokenLen = 1;
                    break;
                case '\'':
                case '\"':
                    tokenLen = ParseQuoteString(format, i, ref builder);
                    break;
                case '%':
                    // Optional format character.
                    // For example, format string "%d" will print day of month
                    // without leading zero.  Most of the cases, "%" can be ignored.
                    nextChar = ParseNextChar(format, i);

                    // nextChar will be -1 if we have already reached the end of the format string.
                    // Besides, we will not allow "%%" to appear in the pattern.
                    if (nextChar is >= 0 and not '%')
                    {
                        singleChar[0] = (char)nextChar;
                        FormatCustomized(dateTime, singleChar, dtfi, offset, ref builder);
                        tokenLen = 2;
                    }
                    else
                    {
                        // This means that '%' is at the end of the format string or
                        // "%%" appears in the format string.
                        throw new FormatException(SRF.Format_InvalidString);
                    }

                    break;
                case '\\':
                    // Escaped character.  Can be used to insert a character into the format string.
                    // For example, "\d" will insert the character 'd' into the string.
                    //
                    // NOTENOTE : we can remove this format character if we enforce the enforced quote
                    // character rule.
                    // That is, we ask everyone to use single quote or double quote to insert characters,
                    // then we can remove this character.
                    //
                    nextChar = ParseNextChar(format, i);
                    if (nextChar >= 0)
                    {
                        builder.Append((char)nextChar);
                        tokenLen = 2;
                    }
                    else
                    {
                        //
                        // This means that '\' is at the end of the formatting string.
                        //
                        throw new FormatException(SRF.Format_InvalidString);
                    }
                    break;
                default:
                    // NOTENOTE : we can remove this rule if we enforce the enforced quote
                    // character rule.
                    // That is, if we ask everyone to use single quote or double quote to insert characters,
                    // then we can remove this default block.
                    builder.Append(ch);
                    tokenLen = 1;
                    break;
            }

            i += tokenLen;
        }
    }

    // output the 'z' family of formats, which output a the offset from UTC, e.g. "-07:30"
    private static void FormatCustomizedTimeZone(
        DateTime dateTime,
        TimeSpan offset,
        int tokenLen,
        bool timeOnly,
        ref ValueStringBuilder builder)
    {
        // See if the instance already has an offset
        bool dateTimeFormat = offset.Ticks == NullOffset;
        if (dateTimeFormat)
        {
            // No offset. The instance is a DateTime and the output should be the local time zone
            if (timeOnly && dateTime.Ticks < TicksPerDay)
            {
                // For time only format and a time only input, the time offset on 0001/01/01 is less
                // accurate than the system's current offset because of daylight saving time.
                offset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
            }
            else if (dateTime.Kind == DateTimeKind.Utc)
            {
                offset = default; // TimeSpan.Zero
            }
            else
            {
                offset = TimeZoneInfo.Local.GetUtcOffset(dateTime);
            }
        }
        if (offset.Ticks >= 0)
        {
            builder.Append('+');
        }
        else
        {
            builder.Append('-');
            // get a positive offset, so that you don't need a separate code path for the negative numbers.
            offset = offset.Negate();
        }

        if (tokenLen <= 1)
        {
            // 'z' format e.g "-7"
            builder.AppendFormatted(offset.Hours, "0");
        }
        else
        {
            // 'zz' or longer format e.g "-07"
            builder.AppendFormatted(offset.Hours, "00");
            if (tokenLen >= 3)
            {
                // 'zzz*' or longer format e.g "-07:30"
                builder.Append(':');
                builder.AppendFormatted(offset.Minutes, "00");
            }
        }
    }

    // output the 'K' format, which is for round-tripping the data
    private static void FormatCustomizedRoundripTimeZone(DateTime dateTime, TimeSpan offset, ref ValueStringBuilder builder)
    {
        // The objective of this format is to round trip the data in the type
        // For DateTime it should round-trip the Kind value and preserve the time zone.
        // DateTimeOffset instance, it should do so by using the internal time zone.

        if (offset.Ticks == NullOffset)
        {
            // source is a date time, so behavior depends on the kind.
            switch (dateTime.Kind)
            {
                case DateTimeKind.Local:
                    // This should output the local offset, e.g. "-07:30"
                    offset = TimeZoneInfo.Local.GetUtcOffset(dateTime);
                    // fall through to shared time zone output code
                    break;
                case DateTimeKind.Utc:
                    // The 'Z' constant is a marker for a UTC date
                    builder.Append('Z');
                    return;
                default:
                    // If the kind is unspecified, we output nothing here
                    return;
            }
        }
        if (offset.Ticks >= 0)
        {
            builder.Append('+');
        }
        else
        {
            builder.Append('-');
            // get a positive offset, so that you don't need a separate code path for the negative numbers.
            offset = offset.Negate();
        }

        Append2DigitNumber(ref builder, offset.Hours);
        builder.Append(':');
        Append2DigitNumber(ref builder, offset.Minutes);
    }

    private static void Append2DigitNumber(ref ValueStringBuilder builder, int val)
    {
        builder.Append((char)('0' + (val / 10)));
        builder.Append((char)('0' + (val % 10)));
    }

    internal static void Format(
        DateTime dateTime,
        ReadOnlySpan<char> format,
        IFormatProvider? provider,
        ref ValueStringBuilder builder) =>
        Format(dateTime, format, provider, new TimeSpan(NullOffset), ref builder);

    internal static void Format(
        DateTime dateTime,
        ReadOnlySpan<char> format,
        IFormatProvider? provider,
        TimeSpan offset,
        ref ValueStringBuilder builder)
    {
        if (format.Length == 1)
        {
            const int ReservedSpace = 33;

            // Optimize for these standard formats that are not affected by culture.
            switch (format[0])
            {
                // Round trip format
                case 'o':
                case 'O':
                    {
                        Span<char> destination = builder.AppendSpan(ReservedSpace);
                        bool success = TryFormatO(dateTime, offset, destination, out int charsWritten);
                        Debug.Assert(success);
                        if (charsWritten < ReservedSpace)
                        {
                            builder.Length -= ReservedSpace - charsWritten;
                        }

                        return;
                    }

                // RFC1123
                case 'r':
                case 'R':
                    {
                        Span<char> destination = builder.AppendSpan(ReservedSpace);
                        bool success = TryFormatR(dateTime, offset, destination, out int charsWritten);
                        Debug.Assert(success);

                        if (charsWritten < ReservedSpace)
                        {
                            builder.Length -= ReservedSpace - charsWritten;
                        }

                        return;
                    }
            }
        }

        DateTimeFormatInfo formatInfo = DateTimeFormatInfo.GetInstance(provider);
        FormatStringBuilder(dateTime, format, formatInfo, offset, ref builder);
    }

    private static void FormatStringBuilder(
        DateTime dateTime,
        scoped ReadOnlySpan<char> format,
        DateTimeFormatInfo formatInfo,
        TimeSpan offset,
        ref ValueStringBuilder builder)
    {
        if (format.Length == 0)
        {
            bool timeOnlySpecialCase = false;
            if (dateTime.Ticks < TicksPerDay)
            {
                // If the time is less than 1 day, consider it as time of day.
                // Just print out the short time format.
                //
                // This is a workaround for VB, since they use ticks less then one day to be
                // time of day.  In cultures which use calendar other than Gregorian calendar, these
                // alternative calendar may not support ticks less than a day.
                // For example, Japanese calendar only supports date after 1868/9/8.
                // This will pose a problem when people in VB get the time of day, and use it
                // to call ToString(), which will use the general format (short date + long time).
                // Since Japanese calendar does not support Gregorian year 0001, an exception will be
                // thrown when we try to get the Japanese year for Gregorian year 0001.
                // Therefore, the workaround allows them to call ToString() for time of day from a DateTime by
                // formatting as ISO 8601 format.
                if (formatInfo.Calendar is JapaneseCalendar
                    or TaiwanCalendar
                    or HijriCalendar
                    or HebrewCalendar
                    or JulianCalendar
                    or UmAlQuraCalendar
                    or PersianCalendar)
                {
                    timeOnlySpecialCase = true;
                    formatInfo = DateTimeFormatInfo.InvariantInfo;
                }
            }

            if (offset.Ticks == NullOffset)
            {
                // Default DateTime.ToString case.
                format = timeOnlySpecialCase ? "s".AsSpan() : "G".AsSpan();
            }
            else
            {
                // Default DateTimeOffset.ToString case.
                format = timeOnlySpecialCase ? RoundtripDateTimeUnfixed.AsSpan() : formatInfo.DateTimeOffsetPattern().AsSpan();
            }
        }

        // Some cases need a buffer to expand the format string, longest known one is 48 characters long.
        Span<char> buffer = stackalloc char[64];

        if (format.Length == 1)
        {
            // Expand the single character format to the full format.

            switch (format[0])
            {
                case 'o':
                case 'O':       // Round trip format
                    formatInfo = DateTimeFormatInfo.InvariantInfo;
                    break;
                case 'r':
                case 'R':       // RFC 1123 Standard
                case 'u':       // Universal time in sortable format.
                    if (offset.Ticks != NullOffset)
                    {
                        // Convert to UTC invariants mean this will be in range
                        dateTime -= offset;
                    }

                    formatInfo = DateTimeFormatInfo.InvariantInfo;
                    break;
                case 's':       // Sortable without Time Zone Info
                    formatInfo = DateTimeFormatInfo.InvariantInfo;
                    break;
                case 'U':       // Universal time in culture dependent format.
                    if (offset.Ticks != NullOffset)
                    {
                        // This format is not supported by DateTimeOffset
                        throw new FormatException(SRF.Format_InvalidString);
                    }

                    // Universal time is always in Gregorian calendar.
                    formatInfo = (DateTimeFormatInfo)formatInfo.Clone();
                    if (formatInfo.Calendar.GetType() != typeof(GregorianCalendar))
                    {
                        formatInfo.Calendar = s_gregorianCalendar;
                    }

                    dateTime = dateTime.ToUniversalTime();
                    break;
            }

            format = format[0] switch
            {
                'd' => formatInfo.ShortDatePattern.AsSpan(),                    // Short Date
                'D' => formatInfo.LongDatePattern.AsSpan(),                     // Long Date
                'F' => formatInfo.FullDateTimePattern.AsSpan(),                 // Full (long date + long time)
                'm' or 'M' => formatInfo.MonthDayPattern.AsSpan(),              // Month/Day Date
                'o' or 'O' => RoundtripFormat.AsSpan(),
                'r' or 'R' => formatInfo.RFC1123Pattern.AsSpan(),               // RFC 1123 Standard
                's' => formatInfo.SortableDateTimePattern.AsSpan(),             // Sortable without Time Zone Info
                't' => formatInfo.ShortTimePattern.AsSpan(),                    // Short Time
                'T' => formatInfo.LongTimePattern.AsSpan(),                     // Long Time
                'u' => formatInfo.UniversalSortableDateTimePattern.AsSpan(),    // Universal with Sortable format
                'U' => formatInfo.FullDateTimePattern.AsSpan(),                 // Universal with Full (long date + long time) format
                'y' or 'Y' => formatInfo.YearMonthPattern.AsSpan(),             // Year/Month Date
                _ => SpecialCases(formatInfo, format[0], buffer),
            };
        }

        FormatCustomized(dateTime, format, formatInfo, offset, ref builder);

        static ReadOnlySpan<char> SpecialCases(DateTimeFormatInfo formatInfo, char format, Span<char> buffer)
        {
            scoped SpanWriter<char> writer = new(buffer);
            switch (format)
            {
                case 'f':
                    // Full (long date + short time)
                    writer.TryWrite(formatInfo.LongDatePattern.AsSpan());
                    writer.TryWrite(' ');
                    writer.TryWrite(formatInfo.ShortTimePattern.AsSpan());
                    return buffer[..writer.Position];
                case 'g':
                    // General (short date + short time)
                    writer.TryWrite(formatInfo.ShortDatePattern.AsSpan());
                    writer.TryWrite(' ');
                    writer.TryWrite(formatInfo.ShortTimePattern.AsSpan());
                    return buffer[..writer.Position];
                case 'G':
                    // General (short date + long time)
                    writer.TryWrite(formatInfo.ShortDatePattern.AsSpan());
                    writer.TryWrite(' ');
                    writer.TryWrite(formatInfo.LongTimePattern.AsSpan());
                    return buffer[..writer.Position];
                default:
                    throw new FormatException(SRF.Format_InvalidString);
            }
        }
    }

    // Roundtrippable format. One of
    //   012345678901234567890123456789012
    //   ---------------------------------
    //   2017-06-12T05:30:45.7680000-07:00
    //   2017-06-12T05:30:45.7680000Z           (Z is short for "+00:00" but also distinguishes DateTimeKind.Utc from DateTimeKind.Local)
    //   2017-06-12T05:30:45.7680000            (interpreted as local time wrt to current time zone)
    private static bool TryFormatO(DateTime dateTime, TimeSpan offset, Span<char> destination, out int charsWritten)
    {
        const int MinimumBytesNeeded = 27;

        int charsRequired = MinimumBytesNeeded;
        DateTimeKind kind = DateTimeKind.Local;

        if (offset.Ticks == NullOffset)
        {
            kind = dateTime.Kind;
            if (kind == DateTimeKind.Local)
            {
                offset = TimeZoneInfo.Local.GetUtcOffset(dateTime);
                charsRequired += 6;
            }
            else if (kind == DateTimeKind.Utc)
            {
                charsRequired++;
            }
        }
        else
        {
            charsRequired += 6;
        }

        if (destination.Length < charsRequired)
        {
            charsWritten = 0;
            return false;
        }
        charsWritten = charsRequired;

        // Hoist most of the bounds checks on destination.
        { _ = destination[MinimumBytesNeeded - 1]; }

        dateTime.GetDate(out int year, out int month, out int day);
        dateTime.GetTimePrecise(out int hour, out int minute, out int second, out int tick);

        WriteFourDecimalDigits((uint)year, destination, 0);
        destination[4] = '-';
        WriteTwoDecimalDigits((uint)month, destination, 5);
        destination[7] = '-';
        WriteTwoDecimalDigits((uint)day, destination, 8);
        destination[10] = 'T';
        WriteTwoDecimalDigits((uint)hour, destination, 11);
        destination[13] = ':';
        WriteTwoDecimalDigits((uint)minute, destination, 14);
        destination[16] = ':';
        WriteTwoDecimalDigits((uint)second, destination, 17);
        destination[19] = '.';
        WriteDigits((uint)tick, destination.Slice(20, 7));

        if (kind == DateTimeKind.Local)
        {
            int offsetTotalMinutes = (int)(offset.Ticks / TimeSpan.TicksPerMinute);

            char sign;
            if (offsetTotalMinutes < 0)
            {
                sign = '-';
                offsetTotalMinutes = -offsetTotalMinutes;
            }
            else
            {
                sign = '+';
            }

            int offsetHours = Math.DivRem(offsetTotalMinutes, 60, out int offsetMinutes);

            // Writing the value backward allows the JIT to optimize by
            // performing a single bounds check against buffer.
            WriteTwoDecimalDigits((uint)offsetMinutes, destination, 31);
            destination[30] = ':';
            WriteTwoDecimalDigits((uint)offsetHours, destination, 28);
            destination[27] = sign;
        }
        else if (kind == DateTimeKind.Utc)
        {
            destination[27] = 'Z';
        }

        return true;
    }

    // Rfc1123
    //   01234567890123456789012345678
    //   -----------------------------
    //   Tue, 03 Jan 2017 08:08:05 GMT
    private static bool TryFormatR(DateTime dateTime, TimeSpan offset, Span<char> destination, out int charsWritten)
    {
        if (destination.Length <= 28)
        {
            charsWritten = 0;
            return false;
        }

        if (offset.Ticks != NullOffset)
        {
            // Convert to UTC invariants.
            dateTime -= offset;
        }

        dateTime.GetDate(out int year, out int month, out int day);
        dateTime.GetTime(out int hour, out int minute, out int second);

        string dayAbbrev = InvariantAbbreviatedDayNames[(int)dateTime.DayOfWeek];
        Debug.Assert(dayAbbrev.Length == 3);

        string monthAbbrev = InvariantAbbreviatedMonthNames[month - 1];
        Debug.Assert(monthAbbrev.Length == 3);

        destination[0] = dayAbbrev[0];
        destination[1] = dayAbbrev[1];
        destination[2] = dayAbbrev[2];
        destination[3] = ',';
        destination[4] = ' ';
        WriteTwoDecimalDigits((uint)day, destination, 5);
        destination[7] = ' ';
        destination[8] = monthAbbrev[0];
        destination[9] = monthAbbrev[1];
        destination[10] = monthAbbrev[2];
        destination[11] = ' ';
        WriteFourDecimalDigits((uint)year, destination, 12);
        destination[16] = ' ';
        WriteTwoDecimalDigits((uint)hour, destination, 17);
        destination[19] = ':';
        WriteTwoDecimalDigits((uint)minute, destination, 20);
        destination[22] = ':';
        WriteTwoDecimalDigits((uint)second, destination, 23);
        destination[25] = ' ';
        destination[26] = 'G';
        destination[27] = 'M';
        destination[28] = 'T';

        charsWritten = 29;
        return true;
    }

    /// <summary>
    /// Writes a value [ 00 .. 99 ] to the buffer starting at the specified offset.
    /// This method performs best when the starting index is a constant literal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteTwoDecimalDigits(uint value, Span<char> destination, int offset)
    {
        Debug.Assert(value <= 99);

        uint temp = '0' + value;
        value /= 10;
        destination[offset + 1] = (char)(temp - (value * 10));
        destination[offset] = (char)('0' + value);
    }

    /// <summary>
    /// Writes a value [ 0000 .. 9999 ] to the buffer starting at the specified offset.
    /// This method performs best when the starting index is a constant literal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteFourDecimalDigits(uint value, Span<char> buffer, int startingIndex = 0)
    {
        Debug.Assert(value <= 9999);

        uint temp = '0' + value;
        value /= 10;
        buffer[startingIndex + 3] = (char)(temp - (value * 10));

        temp = '0' + value;
        value /= 10;
        buffer[startingIndex + 2] = (char)(temp - (value * 10));

        temp = '0' + value;
        value /= 10;
        buffer[startingIndex + 1] = (char)(temp - (value * 10));

        buffer[startingIndex] = (char)('0' + value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteDigits(ulong value, Span<char> buffer)
    {
        // We can mutate the 'value' parameter since it's a copy-by-value local.
        // It'll be used to represent the value left over after each division by 10.

        for (int i = buffer.Length - 1; i >= 1; i--)
        {
            ulong temp = '0' + value;
            value /= 10;
            buffer[i] = (char)(temp - (value * 10));
        }

        Debug.Assert(value < 10);
        buffer[0] = (char)('0' + value);
    }
}
