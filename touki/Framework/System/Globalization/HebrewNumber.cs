// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Touki;

namespace System.Globalization;

////////////////////////////////////////////////////////////////////////////
//
// class HebrewNumber
//
//  Provides static methods for formatting integer values into
//  Hebrew text and parsing Hebrew number text.
//
//  Limitations:
//      Parse can only handle value 1 ~ 999.
//      Append() can only handle 1 ~ 999. If value is greater than 5000,
//      5000 will be subtracted from the value.
//
////////////////////////////////////////////////////////////////////////////

internal static class HebrewNumber
{
    ////////////////////////////////////////////////////////////////////////////
    //
    //  Append
    //
    //  Converts the given number to Hebrew letters according to the numeric
    //  value of each Hebrew letter, appending to the supplied StringBuilder.
    //  Basically, this converts the lunar year and the lunar month to letters.
    //
    //  The character of a year is described by three letters of the Hebrew
    //  alphabet, the first and third giving, respectively, the days of the
    //  weeks on which the New Year occurs and Passover begins, while the
    //  second is the initial of the Hebrew word for defective, normal, or
    //  complete.
    //
    //  Defective Year : Both Heshvan and Kislev are defective (353 or 383 days)
    //  Normal Year    : Heshvan is defective, Kislev is full  (354 or 384 days)
    //  Complete Year  : Both Heshvan and Kislev are full      (355 or 385 days)
    //
    ////////////////////////////////////////////////////////////////////////////

    internal static void Append(ValueStringBuilder builder, int number)
    {
        int outputBufferStartingLength = builder.Length;

        char unitsChar;         // tens and units chars
        int hundreds;           // hundreds and tens values

        //  Adjust the number if greater than 5000.

        if (number > 5000)
        {
            number -= 5000;
        }

        Debug.Assert(number is > 0 and <= 999, "Number is out of range.");

        //  Get the Hundreds.

        hundreds = number / 100;

        if (hundreds > 0)
        {
            number -= hundreds * 100;

            // \x05e7 = 100
            // \x05e8 = 200
            // \x05e9 = 300
            // \x05ea = 400
            // If the number is greater than 400, use the multiples of 400.
            for (int i = 0; i < (hundreds / 4); i++)
            {
                builder.Append('\x05ea');
            }

            int remains = hundreds % 4;
            if (remains > 0)
            {
                builder.Append((char)((int)'\x05e6' + remains));
            }
        }

        //  Get the Tens.

        char tensChar = (number / 10) switch
        {
            (1) => '\x05d9',// Hebrew Letter Yod
            (2) => '\x05db',// Hebrew Letter Kaf
            (3) => '\x05dc',// Hebrew Letter Lamed
            (4) => '\x05de',// Hebrew Letter Mem
            (5) => '\x05e0',// Hebrew Letter Nun
            (6) => '\x05e1',// Hebrew Letter Samekh
            (7) => '\x05e2',// Hebrew Letter Ayin
            (8) => '\x05e4',// Hebrew Letter Pe
            (9) => '\x05e6',// Hebrew Letter Tsadi
            _ => '\x0'
        };

        number %= 10;

        //  Get the Units.

        unitsChar = (char)(number > 0 ? ((int)'\x05d0' + number - 1) : 0);

        if ((unitsChar == '\x05d4') &&            // Hebrew Letter He  (5)
            (tensChar == '\x05d9'))               // Hebrew Letter Yod (10)
        {
            unitsChar = '\x05d5';                 // Hebrew Letter Vav (6)
            tensChar = '\x05d8';                  // Hebrew Letter Tet (9)
        }

        if ((unitsChar == '\x05d5') &&            // Hebrew Letter Vav (6)
            (tensChar == '\x05d9'))               // Hebrew Letter Yod (10)
        {
            unitsChar = '\x05d6';                 // Hebrew Letter Zayin (7)
            tensChar = '\x05d8';                  // Hebrew Letter Tet (9)
        }

        //  Copy the appropriate info to the given buffer.

        if (tensChar != '\x0')
        {
            builder.Append(tensChar);
        }

        if (unitsChar != '\x0')
        {
            builder.Append(unitsChar);
        }

        if (builder.Length - outputBufferStartingLength > 1)
        {
            builder.Insert(builder.Length - 1, '"', 1);
        }
        else
        {
            builder.Append('\'');
        }
    }
}

