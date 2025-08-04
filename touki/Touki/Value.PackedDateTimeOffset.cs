// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    private readonly struct PackedDateTimeOffset
    {
        // The maximum supported number of minutes is +/- 14 hours, but everything goes between -12:00 and +14:00 and
        // almost all offsets are on :30 minute intervals.

        // Windows NT epoch is January 1, 1601. Unix epoch is January 1, 1970. The Gregorian calendar was introduced
        // in 1582 (adopted by Britain in 1752). While it is sort of strange to have an offset that goes back that far,
        // if we do, we still go forward 914 years from 1582 to 2496 (which is well beyond the current date).
        //
        // Our updated algorithm (which exludes offsets that do not fall on 30 minute intervals) gives us 914 years of
        // "ticks" to work with (we did have 457).
        private const ulong BaseTicks = 498283488000000000;
        private const ulong MaxTicks = BaseTicks + TickMask;

        private const ulong TickMask = 0b00000011_11111111_11111111_11111111__11111111_11111111_11111111_11111111;

        // Range constants
        private const int MinOffsetMinutes = -840;  // UTC-14:00
        private const int MaxOffsetMinutes = 840;   // UTC+14:00
        private const int PositiveShiftMinutes = 840; // Shift to make all values positive

        private const int MinuteShift = 58;

        // Validation constants
        private const int IncrementMinutes = 30;    // 30-minute intervals

        private readonly ulong _data;

        private PackedDateTimeOffset(ulong data) => _data = data;

        public static bool TryCreate(ulong ticks, short offsetMinutes, out PackedDateTimeOffset packed)
        {
            bool result = false;
            packed = default;

            if ((ticks is > BaseTicks and < MaxTicks)
                && offsetMinutes >= MinOffsetMinutes
                && offsetMinutes <= MaxOffsetMinutes)
            {
                // Shift to make all values positive
                offsetMinutes += PositiveShiftMinutes;
                int quotient = Math.DivRem(offsetMinutes, IncrementMinutes, out int remainder);

                // Validate: no remainder (30-min increment)
                if (remainder == 0)
                {
                    ulong data = ((ulong)quotient << MinuteShift);
                    data |= (ticks - BaseTicks);
                    packed = new(data);
                    result = true;
                }
            }

            return result;
        }

        public DateTimeOffset Extract()
        {
            DateTimeOffset dateTimeOffset = default;
            ref DateTimeOffsetAccessor accessor = ref Unsafe.As<DateTimeOffset, DateTimeOffsetAccessor>(ref dateTimeOffset);
            accessor._dateTime._dateTimeData = (_data & TickMask) + BaseTicks;
            accessor._offsetMinutes = (short)(((int)(_data >> MinuteShift) * IncrementMinutes) - PositiveShiftMinutes);

            return dateTimeOffset;
        }
    }
}
