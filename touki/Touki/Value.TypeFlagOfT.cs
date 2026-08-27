// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    /// <summary>
    ///  Provides strongly typed reconstruction and runtime type metadata for an inline union value.
    /// </summary>
    private abstract class TypeFlag<T> : TypeFlag
    {
        public sealed override Type Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => typeof(T);
        }

        public sealed override object ToObject(in Value value) => To(value)!;
        public abstract T To(in Value value);
    }
}
