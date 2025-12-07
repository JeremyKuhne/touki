// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Type extension methods.
/// </summary>
public static class TypeExtensions
{
    extension(Type? type)
    {
        /// <summary>
        ///  Determines whether the current type can be assigned to a variable of the specified <paramref name="targetType"/>.
        /// </summary>
        public bool IsAssignableTo(Type? targetType) => targetType?.IsAssignableFrom(type) ?? false;
    }
}
