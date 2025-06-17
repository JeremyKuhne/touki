// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal static class OverflowAdapter
{
    [DoesNotReturn]
    public static void Throw(string? message) => ThrowOverflow(message);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOverflow(string? message) => throw new OverflowException(message);
}
