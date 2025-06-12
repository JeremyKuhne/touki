// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    private sealed class DateTimeOffsetFlag : TypeFlag<DateTimeOffset>
    {
        public static DateTimeOffsetFlag Instance { get; } = new();

        public override DateTimeOffset To(in Value value)
            => new(new DateTime(value._union.Ticks, DateTimeKind.Utc));
    }
}
