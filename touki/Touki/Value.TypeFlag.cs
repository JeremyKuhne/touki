// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    private abstract class TypeFlag
    {
        public abstract Type Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;
        }

        public abstract object ToObject(in Value value);
    }
}
