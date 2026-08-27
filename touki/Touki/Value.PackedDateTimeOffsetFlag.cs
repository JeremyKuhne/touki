// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    /// <summary>
    ///  Identifies an inline <see cref="DateTimeOffset"/> stored in packed form and reconstructs it from union storage.
    /// </summary>
    private sealed class PackedDateTimeOffsetFlag : TypeFlag<DateTimeOffset>
    {
        public static PackedDateTimeOffsetFlag Instance { get; } = new();

        public override DateTimeOffset To(in Value value) => value._union.PackedDateTimeOffset.Extract();
    }
}
