// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

internal static class InternalDateTimeExtensions
{
    // Exactly the same as GetDatePart, except computing all of
    // year/month/day rather than just one of them. Used when all three
    // are needed rather than redoing the computations for each.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void GetDate(this ref readonly DateTime dateTime, out int year, out int month, out int day) =>
        Unsafe.As<DateTime, InternalDateTime>(ref Unsafe.AsRef(in dateTime)).GetDate(out year, out month, out day);

    internal static void GetTime(this ref readonly DateTime dateTime, out int hour, out int minute, out int second) =>
        Unsafe.As<DateTime, InternalDateTime>(ref Unsafe.AsRef(in dateTime)).GetTime(out hour, out minute, out second);

    internal static void GetTime(this ref readonly DateTime dateTime, out int hour, out int minute, out int second, out int millisecond) =>
        Unsafe.As<DateTime, InternalDateTime>(ref Unsafe.AsRef(in dateTime)).GetTime(out hour, out minute, out second, out millisecond);

    internal static void GetTimePrecise(this ref readonly DateTime dateTime, out int hour, out int minute, out int second, out int tick) =>
        Unsafe.As<DateTime, InternalDateTime>(ref Unsafe.AsRef(in dateTime)).GetTimePrecise(out hour, out minute, out second, out tick);

    internal readonly struct InternalDateTime
    {
        // Number of 100ns ticks per time unit
        private const long TicksPerMillisecond = 10000;
        private const long TicksPerSecond = TicksPerMillisecond * 1000;
        private const long TicksPerMinute = TicksPerSecond * 60;
        private const long TicksPerHour = TicksPerMinute * 60;
        private const long TicksPerDay = TicksPerHour * 24;

        // Number of days in a non-leap year
        private const int DaysPerYear = 365;
        // Number of days in 4 years
        private const int DaysPer4Years = DaysPerYear * 4 + 1;       // 1461
                                                                     // Number of days in 100 years
        private const int DaysPer100Years = DaysPer4Years * 25 - 1;  // 36524
                                                                     // Number of days in 400 years
        private const int DaysPer400Years = DaysPer100Years * 4 + 1; // 146097


        private const ulong TicksMask = 0x3FFFFFFFFFFFFFFF;

        private static readonly uint[] s_daysToMonth365 = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334, 365];
        private static readonly uint[] s_daysToMonth366 = [0, 31, 60, 91, 121, 152, 182, 213, 244, 274, 305, 335, 366];

        // The data is stored as an unsigned 64-bit integer
        //   Bits 01-62: The value of 100-nanosecond ticks where 0 represents 1/1/0001 12:00am, up until the value
        //               12/31/9999 23:59:59.9999999
        //   Bits 63-64: A four-state value that describes the DateTimeKind value of the date time, with a 2nd
        //               value for the rare case where the date time is local, but is in an overlapped daylight
        //               savings time hour and it is in daylight savings time. This allows distinction of these
        //               otherwise ambiguous local times and prevents data loss when round tripping from Local to
        //               UTC time.
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        private readonly ulong _dateData;
#pragma warning restore CS0649

        private ulong UTicks => _dateData & TicksMask;

        // Exactly the same as GetDatePart, except computing all of
        // year/month/day rather than just one of them. Used when all three
        // are needed rather than redoing the computations for each.
        internal void GetDate(out int year, out int month, out int day)
        {
            // n = number of days since 1/1/0001
            uint n = (uint)(UTicks / TicksPerDay);
            // y400 = number of whole 400-year periods since 1/1/0001
            uint y400 = n / DaysPer400Years;
            // n = day number within 400-year period
            n -= y400 * DaysPer400Years;
            // y100 = number of whole 100-year periods within 400-year period
            uint y100 = n / DaysPer100Years;
            // Last 100-year period has an extra day, so decrement result if 4
            if (y100 == 4)
                y100 = 3;
            // n = day number within 100-year period
            n -= y100 * DaysPer100Years;
            // y4 = number of whole 4-year periods within 100-year period
            uint y4 = n / DaysPer4Years;
            // n = day number within 4-year period
            n -= y4 * DaysPer4Years;
            // y1 = number of whole years within 4-year period
            uint y1 = n / DaysPerYear;
            // Last year has an extra day, so decrement result if 4
            if (y1 == 4)
                y1 = 3;
            // compute year
            year = (int)(y400 * 400 + y100 * 100 + y4 * 4 + y1 + 1);
            // n = day number within year
            n -= y1 * DaysPerYear;
            // dayOfYear = n + 1;
            // Leap year calculation looks different from IsLeapYear since y1, y4,
            // and y100 are relative to year 1, not year 0
            uint[] days = y1 == 3 && (y4 != 24 || y100 == 3) ? s_daysToMonth366 : s_daysToMonth365;
            // All months have less than 32 days, so n >> 5 is a good conservative
            // estimate for the month
            uint m = (n >> 5) + 1;
            // m = 1-based month number
            while (n >= days[m])
                m++;
            // compute month and day
            month = (int)m;
            day = (int)(n - days[m - 1] + 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GetTime(out int hour, out int minute, out int second)
        {
            ulong seconds = UTicks / TicksPerSecond;
            ulong minutes = seconds / 60;
            second = (int)(seconds - (minutes * 60));
            ulong hours = minutes / 60;
            minute = (int)(minutes - (hours * 60));
            hour = (int)((uint)hours % 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GetTime(out int hour, out int minute, out int second, out int millisecond)
        {
            ulong milliseconds = UTicks / TicksPerMillisecond;
            ulong seconds = milliseconds / 1000;
            millisecond = (int)(milliseconds - (seconds * 1000));
            ulong minutes = seconds / 60;
            second = (int)(seconds - (minutes * 60));
            ulong hours = minutes / 60;
            minute = (int)(minutes - (hours * 60));
            hour = (int)((uint)hours % 24);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GetTimePrecise(out int hour, out int minute, out int second, out int tick)
        {
            ulong ticks = UTicks;
            ulong seconds = ticks / TicksPerSecond;
            tick = (int)(ticks - (seconds * TicksPerSecond));
            ulong minutes = seconds / 60;
            second = (int)(seconds - (minutes * 60));
            ulong hours = minutes / 60;
            minute = (int)(minutes - (hours * 60));
            hour = (int)((uint)hours % 24);
        }
    }
}
