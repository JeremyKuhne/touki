// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki;

namespace System;

/// <summary>
///  Type extension methods.
/// </summary>
public static class TypeExtensions
{
    extension(Type type)
    {
        /// <summary>
        ///  Determines whether the current type can be assigned to a variable of the specified <paramref name="targetType"/>.
        /// </summary>
        public bool IsAssignableTo(Type? targetType) => targetType?.IsAssignableFrom(type) ?? false;

        /// <summary>
        ///  Gets a value that indicates whether the type is a type definition.
        /// </summary>
        public bool IsTypeDefinition => !type.IsArray
            && !type.IsByRef
            && !type.IsPointer
            && !type.IsConstructedGenericType
            && !type.IsGenericParameter;

        /// <summary>
        ///  Gets the values and names for the specified enum <see cref="Type"/>.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   Reads from <see cref="Enum"/>'s internal <c>GetCachedValuesAndNames</c> cache via
        ///   reflection; faster than <see cref="Enum.GetValues(Type)"/> + <see cref="Enum.GetNames(Type)"/>
        ///   on .NET Framework. Returned arrays are the cached references - do not mutate.
        ///  </para>
        /// </remarks>
        /// <exception cref="ArgumentException">
        ///  <paramref name="type"/> is not an enum type.
        /// </exception>
        public (ulong[] Values, string[] Names) GetEnumValuesAndNames() =>
            EnumDataCache.GetEnumValuesAndNames(type);
    }
}
