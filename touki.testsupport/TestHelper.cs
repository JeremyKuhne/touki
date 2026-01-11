// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection;

namespace Touki.TestSupport;

/// <summary>
///  Helpers for tests.
/// </summary>
public static class TestHelper
{
    /// <summary>
    ///  Invokes the finalizer of an object directly, bypassing the normal garbage collection process.
    /// </summary>
    public static void InvokeFinalizer(object @object)
    {
        // Find the special finalizer method and invoke it directly
        MethodInfo? finalizerMethod = @object.GetType().GetMethod(
            "Finalize",
            BindingFlags.NonPublic | BindingFlags.Instance);

        finalizerMethod?.Invoke(@object, null);
    }
}
