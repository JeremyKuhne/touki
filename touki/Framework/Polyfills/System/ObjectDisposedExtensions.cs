// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System;

/// <summary>
///  ObjectDisposedException helper to allow using new patterns for throwing <see cref="ObjectDisposedException"/>s.
/// </summary>
public static class ObjectDisposedExtensions
{
    extension(ObjectDisposedException)
    {
        /// <summary>
        ///  Throws an <see cref="ObjectDisposedException"/> if <paramref name="condition"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="instance">The object whose type's name is used to construct the exception's message.</param>
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
        {
            if (condition)
            {
                ThrowObjectDisposed(instance);
            }
        }

        /// <summary>
        ///  Throws an <see cref="ObjectDisposedException"/> if <paramref name="condition"/> is <see langword="true"/>.
        /// </summary>
        /// <param name="condition">The condition to evaluate.</param>
        /// <param name="type">The type whose name is used to construct the exception's message.</param>
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, Type type)
        {
            if (condition)
            {
                ThrowObjectDisposed(type);
            }
        }
    }

    [DoesNotReturn]
    private static void ThrowObjectDisposed(object instance) =>
        throw new ObjectDisposedException(instance?.GetType().FullName);

    [DoesNotReturn]
    private static void ThrowObjectDisposed(Type type) =>
        throw new ObjectDisposedException(type?.FullName);
}
