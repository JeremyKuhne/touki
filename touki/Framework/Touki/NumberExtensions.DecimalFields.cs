// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public static unsafe partial class NumberExtensions
{
#pragma warning disable CS0649 // Field 'DecimalFields._flags' is never assigned to, and will always have its default value 0
    private struct DecimalFields
    {
        // Matching the layout of the decimal type in .NET Framework.
        internal uint _flags;
        internal uint _hi;
        internal uint _lo;
        internal uint _mid;
    }
#pragma warning restore CS0649
}
