// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    /// <summary>
    ///  Provides a layout-compatible view of the packed tick and kind data stored by <see cref="DateTime"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private struct DateTimeAccessor
    {
        internal const ulong TicksMask = 0x3FFFFFFFFFFFFFFF;
        internal ulong _dateTimeData;
    }
}
