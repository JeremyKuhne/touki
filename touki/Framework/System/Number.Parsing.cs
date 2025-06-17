// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Touki;

namespace System;

// The Parse methods provided by the numeric classes convert a
// string to a numeric value. The optional style parameter specifies the
// permitted style of the numeric string. It must be a combination of bit flags
// from the NumberStyles enumeration. The optional info parameter
// specifies the NumberFormatInfo instance to use when parsing the
// string. If the info parameter is null or omitted, the numeric
// formatting information is obtained from the current culture.
//
// Numeric strings produced by the Format methods using the Currency,
// Decimal, Engineering, Fixed point, General, or Number standard formats
// (the C, D, E, F, G, and N format specifiers) are guaranteed to be parseable
// by the Parse methods if the NumberStyles.Any style is
// specified. Note, however, that the Parse methods do not accept
// NaNs or Infinities.

internal static partial class Number
{
    private const int Int32Precision = 10;
    private const int UInt32Precision = Int32Precision;
    private const int Int64Precision = 19;
    private const int UInt64Precision = 20;

    private const int DoubleMaxExponent = 309;
    private const int DoubleMinExponent = -324;

    private const int FloatingPointMaxExponent = DoubleMaxExponent;
    private const int FloatingPointMinExponent = DoubleMinExponent;

    private const int SingleMaxExponent = 39;
    private const int SingleMinExponent = -45;

    private const int HalfMaxExponent = 5;
    private const int HalfMinExponent = -8;

    internal static double NumberToDouble(ref NumberBuffer number)
    {
        number.CheckConsistency();
        double result;

        if ((number.DigitsCount == 0) || (number.Scale < DoubleMinExponent))
        {
            result = 0;
        }
        else if (number.Scale > DoubleMaxExponent)
        {
            result = double.PositiveInfinity;
        }
        else
        {
            ulong bits = NumberToDoubleFloatingPointBits(ref number, in FloatingPointInfo.s_double);
            result = BitConverters.UInt64BitsToDouble(bits);
        }

        return number.IsNegative ? -result : result;
    }

    internal static float NumberToSingle(ref NumberBuffer number)
    {
        number.CheckConsistency();
        float result;

        if ((number.DigitsCount == 0) || (number.Scale < SingleMinExponent))
        {
            result = 0;
        }
        else if (number.Scale > SingleMaxExponent)
        {
            result = float.PositiveInfinity;
        }
        else
        {
            uint bits = NumberToSingleFloatingPointBits(ref number, in FloatingPointInfo.s_single);
            result = BitConverters.UInt32BitsToSingle(bits);
        }

        return number.IsNegative ? -result : result;
    }
}
