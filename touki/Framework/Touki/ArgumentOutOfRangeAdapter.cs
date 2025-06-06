// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal static class ArgumentOutOfRangeAdapter
{
    public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            ThrowNegative(value, paramName);
    }

    [DoesNotReturn]
    private static void ThrowNegative<T>(T value, string? paramName) =>
    throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be positive. ({value})");
}
