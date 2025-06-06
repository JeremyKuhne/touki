// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal static class ThrowHelper
{
    [DoesNotReturn]
    internal static void ThrowArgumentException(string? paramName = null, string? message = null)
    {
        throw new ArgumentException(message, paramName);
    }
}
