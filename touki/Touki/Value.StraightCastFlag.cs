// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public readonly partial struct Value
{
    /// <summary>
    ///  Identifies an inline value whose bits can be reinterpreted directly from union storage.
    /// </summary>
    private sealed class StraightCastFlag<T> : TypeFlag<T>
    {
        public static StraightCastFlag<T> Instance { get; } = new();

        public override T To(in Value value) => Unsafe.As<Union, T>(ref Unsafe.AsRef(in value._union));
    }
}
