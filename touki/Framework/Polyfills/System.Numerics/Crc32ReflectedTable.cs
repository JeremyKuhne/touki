// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

internal static class Crc32ReflectedTable
{
    internal static uint[] Generate(uint reflectedPolynomial)
    {
        uint[] table = new uint[256];

        for (int i = 0; i < 256; i++)
        {
            uint val = (uint)i;

            for (int j = 0; j < 8; j++)
            {
                if ((val & 0b0000_0001) == 0)
                {
                    val >>= 1;
                }
                else
                {
                    val = (val >> 1) ^ reflectedPolynomial;
                }
            }

            table[i] = val;
        }

        return table;
    }
}
