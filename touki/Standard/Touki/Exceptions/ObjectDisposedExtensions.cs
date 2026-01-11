// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Exceptions;

/// <summary>
///  ObjectDisposedException helper to allow using new patterns for throwing <see cref="ObjectDisposedException"/>s.
/// </summary>
public static class ObjectDisposedExtensions
{
    extension(ObjectDisposedException)
    {
        /// <summary>Throws an <see cref="ObjectDisposedException"/> if <paramref name="condition"/> is true.</summary>
        public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
        {
            if (condition)
            {
                ThrowObjectDisposed(instance);
            }
        }
    }

    [DoesNotReturn]
    private static void ThrowObjectDisposed(object instance) =>
        throw new ObjectDisposedException(instance?.GetType().FullName);
}
