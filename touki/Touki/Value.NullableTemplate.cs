// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NullableTemplate<T> where T : unmanaged
    {
        public readonly bool _hasValue;
        public readonly T _value;

        public NullableTemplate(T value)
        {
            _value = value;
            _hasValue = true;
        }
    }
}
