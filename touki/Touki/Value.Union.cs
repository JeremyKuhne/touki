﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    private struct Union
    {
        [FieldOffset(0)] public byte Byte;
        [FieldOffset(0)] public sbyte SByte;
        [FieldOffset(0)] public char Char;
        [FieldOffset(0)] public bool Boolean;
        [FieldOffset(0)] public short Int16;
        [FieldOffset(0)] public ushort UInt16;
        [FieldOffset(0)] public int Int32;
        [FieldOffset(0)] public uint UInt32;
        [FieldOffset(0)] public long Int64;
        [FieldOffset(0)] public long Ticks;
        [FieldOffset(0)] public ulong UInt64;
        [FieldOffset(0)] public float Single;                   // 4 bytes
        [FieldOffset(0)] public double Double;                  // 8 bytes
        [FieldOffset(0)] public DateTime DateTime;              // 8 bytes  (ulong)
        [FieldOffset(0)] public PackedDateTimeOffset PackedDateTimeOffset;
        [FieldOffset(0)] public (int Offset, int Count) Segment;
    }
}
