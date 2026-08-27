// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

/// <summary>
///  Provides a non-inlined path for throwing <see cref="OverflowException"/> with an optional message.
/// </summary>
internal static class OverflowAdapter
{
    [DoesNotReturn]
    public static void Throw(string? message) => ThrowOverflow(message);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOverflow(string? message) => throw new OverflowException(message);
}
