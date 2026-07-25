// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    [StructLayout(LayoutKind.Auto)]
    private struct DateTimeOffsetAccessor
    {
        internal DateTimeAccessor _dateTime;
        internal short _offsetMinutes;
    }
}
