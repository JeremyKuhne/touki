// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    /// <summary>
    ///  Provides a layout-compatible view of the date-time data and offset minutes stored by
    ///  <see cref="DateTimeOffset"/>.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    private struct DateTimeOffsetAccessor
    {
        internal DateTimeAccessor _dateTime;
        internal short _offsetMinutes;
    }
}
