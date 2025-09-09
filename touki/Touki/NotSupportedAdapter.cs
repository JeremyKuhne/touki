// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal static class NotSupportedAdapter
{
    [DoesNotReturn]
    public static void Throw(string? message = null) => ThrowNotSupported(message);

    [DoesNotReturn]
    private static void ThrowNotSupported(string? message) =>
        throw new NotSupportedException(message);
}
