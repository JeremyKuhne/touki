// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

/// <summary>
///  Helper class for throwing exceptions.
/// </summary>
public static class ExceptionExtensions
{
    extension(InvalidOperationException)
    {
        /// <inheritdoc cref="InvalidOperationException(string)"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw(string message) => throw new InvalidOperationException(message);
    }

    extension(ArgumentOutOfRangeException)
    {
        /// <inheritdoc cref="ArgumentOutOfRangeException(string, string)"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Throw(string paramName, string? message = null) =>
            throw new ArgumentOutOfRangeException(paramName, message);
    }

    extension(OutOfMemoryException)
    {
        /// <inheritdoc cref="OutOfMemoryException"/>
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void Throw() =>
#pragma warning disable CA2201 // Do not raise reserved exception types
            throw new OutOfMemoryException();
#pragma warning restore CA2201
    }
}
