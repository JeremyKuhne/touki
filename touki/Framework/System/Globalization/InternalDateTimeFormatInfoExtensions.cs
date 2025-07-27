// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Reflection;

namespace System;

internal static class InternalDateTimeFormatInfoExtensions
{
    private static readonly PropertyInfo s_dateTimeOffsetPattern =
        typeof(DateTimeFormatInfo).GetProperty("DateTimeOffsetPattern", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly PropertyInfo s_formatFlags =
        typeof(DateTimeFormatInfo).GetProperty("FormatFlags", BindingFlags.NonPublic | BindingFlags.Instance);

    // Also create MethodInfo for internalGetGenitiveMonthNames, internalGetLeapYearMonthNames, internalGetAbbreviatedMonthNames, and internalGetMonthNames

    private static readonly MethodInfo s_internalGetGenitiveMonthNames =
        typeof(DateTimeFormatInfo).GetMethod("internalGetGenitiveMonthNames", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo s_internalGetLeapYearMonthNames =
        typeof(DateTimeFormatInfo).GetMethod("internalGetLeapYearMonthNames", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo s_internalGetAbbreviatedMonthNames =
        typeof(DateTimeFormatInfo).GetMethod("internalGetAbbreviatedMonthNames", BindingFlags.NonPublic | BindingFlags.Instance);

    private static readonly MethodInfo s_internalGetMonthNames =
        typeof(DateTimeFormatInfo).GetMethod("internalGetMonthNames", BindingFlags.NonPublic | BindingFlags.Instance);

    internal static string DateTimeOffsetPattern(this DateTimeFormatInfo formatInfo) =>
        (string)s_dateTimeOffsetPattern.GetValue(formatInfo);

    internal static string GetMonthName(this DateTimeFormatInfo formatInfo, int month, MonthNameStyles style, bool abbreviated)
    {
        string[] monthNamesArray = style switch
        {
            MonthNameStyles.Genitive => (string[])s_internalGetGenitiveMonthNames.Invoke(formatInfo, [abbreviated]),
            MonthNameStyles.LeapYear => (string[])s_internalGetLeapYearMonthNames.Invoke(formatInfo, null),
            _ => abbreviated
                ? (string[])s_internalGetAbbreviatedMonthNames.Invoke(formatInfo, null)
                : (string[])s_internalGetMonthNames.Invoke(formatInfo, null),
        };

        // The month range is from 1 ~ this.m_monthNames.Length
        // (actually is 13 right now for all cases)
        return (month < 1) || (month > monthNamesArray.Length)
            ? throw new ArgumentOutOfRangeException("month")
            : monthNamesArray[month - 1];
    }

    internal static int FormatFlags(this DateTimeFormatInfo formatInfo)
    {
        // This is a bitmask of DateTimeFormatFlags
        return (int)s_formatFlags.GetValue(formatInfo);
    }
}
