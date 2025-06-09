// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

internal static class ObjectDisposedAdapter
{
    public static void ThrowIf([DoesNotReturnIf(true)] bool condition, object instance)
    {
        if (condition)
        {
            ThrowObjectDisposed(instance);
        }
    }

    [DoesNotReturn]
    private static void ThrowObjectDisposed(object instance) =>
        throw new ObjectDisposedException(instance?.GetType().FullName);
}
