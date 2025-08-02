// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Helper class for throwing exceptions.
/// </summary>
public static class ThrowHelper
{
    /// <inheritdoc cref="InvalidOperationException(string)"/>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalidOperation(string message) => throw new InvalidOperationException(message);

    /// <inheritdoc cref="ArgumentException(string, string)"/>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgument(string paramName, string? message = null) =>
        throw new ArgumentException(message, paramName);

    /// <inheritdoc cref="ArgumentOutOfRangeException(string, string)"/>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentOutOfRange(string paramName, string? message = null) =>
        throw new ArgumentOutOfRangeException(paramName, message);

    /// <inheritdoc cref="OutOfMemoryException"/>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowOutOfMemory() =>
#pragma warning disable CA2201 // Do not raise reserved exception types
        throw new OutOfMemoryException();
#pragma warning restore CA2201
}
