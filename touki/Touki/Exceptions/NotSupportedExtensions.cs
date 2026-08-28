// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

/// <summary>
///  Provides a non-returning helper for throwing <see cref="NotSupportedException"/> with an optional message.
/// </summary>
internal static class NotSupportedExtensions
{
    extension(NotSupportedException)
    {
        /// <summary>
        ///  Throws a <see cref="NotSupportedException"/> with the specified message.
        /// </summary>
        /// <param name="message">The message for the exception.</param>
        [DoesNotReturn]
        public static void Throw(string? message = null) => ThrowNotSupported(message);
    }

    [DoesNotReturn]
    private static void ThrowNotSupported(string? message) =>
        throw new NotSupportedException(message);
}
