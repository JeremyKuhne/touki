﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    private static class TypeFlags
    {
        internal static StraightCastFlag<bool> Boolean { get; } = StraightCastFlag<bool>.Instance;
        internal static StraightCastFlag<char> Char { get; } = StraightCastFlag<char>.Instance;
        internal static StraightCastFlag<byte> Byte { get; } = StraightCastFlag<byte>.Instance;
        internal static StraightCastFlag<sbyte> SByte { get; } = StraightCastFlag<sbyte>.Instance;
        internal static StraightCastFlag<short> Int16 { get; } = StraightCastFlag<short>.Instance;
        internal static StraightCastFlag<ushort> UInt16 { get; } = StraightCastFlag<ushort>.Instance;
        internal static StraightCastFlag<int> Int32 { get; } = StraightCastFlag<int>.Instance;
        internal static StraightCastFlag<uint> UInt32 { get; } = StraightCastFlag<uint>.Instance;
        internal static StraightCastFlag<long> Int64 { get; } = StraightCastFlag<long>.Instance;
        internal static StraightCastFlag<ulong> UInt64 { get; } = StraightCastFlag<ulong>.Instance;
        internal static StraightCastFlag<float> Single { get; } = StraightCastFlag<float>.Instance;
        internal static StraightCastFlag<double> Double { get; } = StraightCastFlag<double>.Instance;
        internal static StraightCastFlag<DateTime> DateTime { get; } = StraightCastFlag<DateTime>.Instance;
        internal static DateTimeOffsetFlag DateTimeOffset { get; } = DateTimeOffsetFlag.Instance;
        internal static PackedDateTimeOffsetFlag PackedDateTimeOffset { get; } = PackedDateTimeOffsetFlag.Instance;
    }
}
