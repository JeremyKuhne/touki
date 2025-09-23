// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

/// <summary>
///  Helper class for throwing <see cref="ArgumentException"/>s.
/// </summary>
public static class ArgumentExceptionExtensions
{
    extension(ArgumentException)
    {
        /// <inheritdoc cref="ArgumentException(string, string?)"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw(string? message, string? paramName = null) =>
            throw new ArgumentException(message, paramName);
    }
}
