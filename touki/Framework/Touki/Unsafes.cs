// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  <see cref="Unsafe"/> methods that don't have a direct equivalent in the .NET Framework build.
/// </summary>
public static unsafe class Unsafes
{
    /// <summary>
    /// Reinterprets the given value of type <typeparamref name="TFrom" /> as a value of type <typeparamref name="TTo" />.
    /// </summary>
    /// <exception cref="NotSupportedException">The sizes of <typeparamref name="TFrom" /> and <typeparamref name="TTo" /> are not the same
    /// or the type parameters are not <see langword="struct"/>s.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTo BitCast<TFrom, TTo>(TFrom source)
        where TFrom : unmanaged
        where TTo : unmanaged
    {
        if (sizeof(TFrom) != sizeof(TTo) || !typeof(TFrom).IsValueType || !typeof(TTo).IsValueType)
        {
            NotSupported.Throw();
        }

        return Unsafe.ReadUnaligned<TTo>(ref Unsafe.As<TFrom, byte>(ref source));
    }
}
