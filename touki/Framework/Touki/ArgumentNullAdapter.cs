// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal static class ArgumentNullAdapter
{
    public static void ThrowIfNull<T>(T? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null)
        {
            ThrowNull(paramName);
        }
    }

    [DoesNotReturn]
    private static void ThrowNull(string? paramName) =>
        throw new ArgumentNullException(paramName, "Value cannot be null.");
}
