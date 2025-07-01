// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Type information for a type <typeparamref name="T"/>.
/// </summary>
public static partial class TypeInfo<T>
{
    private static bool? s_hasReferences;

    /// <summary>
    ///  Returns <see langword="true"/> if the type <typeparamref name="T"/> is a reference type or contains references.
    /// </summary>
    public static bool IsReferenceOrContainsReferences()
    {
        if (s_hasReferences.HasValue)
        {
            return s_hasReferences.Value;
        }

#if NET
        s_hasReferences = RuntimeHelpers.IsReferenceOrContainsReferences<T>();
#else
        s_hasReferences = HasReferences();

        static bool HasReferences()
        {
            if (!typeof(T).IsValueType)
            {
                return true;
            }

            if (typeof(T).IsPrimitive
                || typeof(T).IsEnum
                || typeof(T) == typeof(DateTime))
            {
                return false;
            }

            try
            {
                Type type = typeof(T);
                GCHandle handle = GCHandle.Alloc(default(T), GCHandleType.Pinned);
                handle.Free();
                return false;
            }
            catch (Exception)
            {
                // Contained a reference
                return true;
            }
        }
#endif

        return s_hasReferences.Value;
    }
}
