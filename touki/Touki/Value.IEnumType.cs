// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    private interface IEnumType
    {
        Type UnderlyingType { get; }
        bool IsSigned { get; }
        int Size { get; }
        ulong AsUlong(in Value value);
    }
}
