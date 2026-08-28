// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    /// <summary>
    ///  Models the layout of a non-empty <see cref="Nullable{T}"/> for reconstructing nullable values from inline
    ///  storage.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NullableTemplate<T> where T : unmanaged
    {
        public readonly bool _hasValue;
        public readonly T _value;

        /// <summary>
        ///  Creates a template for a non-empty nullable value.
        /// </summary>
        /// <param name="value">The underlying value to store.</param>
        public NullableTemplate(T value)
        {
            _value = value;
            _hasValue = true;
        }
    }
}
