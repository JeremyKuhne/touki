﻿// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;

namespace System.Buffers.Text;

internal static partial class FormattingHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(ulong value)
    {
        int digits = 1;
        uint part;
        if (value >= 10000000)
        {
            if (value >= 100000000000000)
            {
                part = (uint)(value / 100000000000000);
                digits += 14;
            }
            else
            {
                part = (uint)(value / 10000000);
                digits += 7;
            }
        }
        else
        {
            part = (uint)value;
        }

        if (part < 10)
        {
            // no-op
        }
        else if (part < 100)
        {
            digits++;
        }
        else if (part < 1000)
        {
            digits += 2;
        }
        else if (part < 10000)
        {
            digits += 3;
        }
        else if (part < 100000)
        {
            digits += 4;
        }
        else if (part < 1000000)
        {
            digits += 5;
        }
        else
        {
            Debug.Assert(part < 10000000);
            digits += 6;
        }

        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(uint value)
    {
        int digits = 1;
        if (value >= 100000)
        {
            value /= 100000;
            digits += 5;
        }

        if (value < 10)
        {
            // no-op
        }
        else if (value < 100)
        {
            digits++;
        }
        else if (value < 1000)
        {
            digits += 2;
        }
        else if (value < 10000)
        {
            digits += 3;
        }
        else
        {
            Debug.Assert(value < 100000);
            digits += 4;
        }

        return digits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountHexDigits(ulong value)
    {
        // The number of hex digits is log16(value) + 1, or log2(value) / 4 + 1
        return (BitOperations.Log2(value) >> 2) + 1;
    }

    // Counts the number of trailing '0' digits in a decimal number.
    // e.g., value =      0 => retVal = 0, valueWithoutTrailingZeros = 0
    //       value =   1234 => retVal = 0, valueWithoutTrailingZeros = 1234
    //       value = 320900 => retVal = 2, valueWithoutTrailingZeros = 3209
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDecimalTrailingZeros(uint value, out uint valueWithoutTrailingZeros)
    {
        int zeroCount = 0;

        if (value != 0)
        {
            while (true)
            {
                uint temp = value / 10;
                if (value != (temp * 10))
                {
                    break;
                }

                value = temp;
                zeroCount++;
            }
        }

        valueWithoutTrailingZeros = value;
        return zeroCount;
    }
}
