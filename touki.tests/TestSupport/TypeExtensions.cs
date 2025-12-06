// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Originally from WinForms
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;

namespace System;

/// <summary>
///  Reflection helpers to assist with type discovery.
/// </summary>
public static class TypeExtensions
{
    /// <param name="type">The targeted type.</param>
    extension(Type type)
    {
        /// <summary>
        ///  Gets a nested type, and if it's a generic type definition, makes it a full type using the parent type's generic arguments.
        /// </summary>
        /// <param name="nestedTypeName">The name of the nested type.</param>
        /// <param name="nestedGenericTypes">Additional nested type parameters, if any.</param>
        /// <returns>Nested types.</returns>
        /// <exception cref="ArgumentException">Could not find the <paramref name="nestedTypeName"/>.</exception>
        /// <exception cref="NotImplementedException">An additional case still needs implemented.</exception>
        /// <remarks>
        ///  <para>
        ///   This is useful when types are not public, and therefore cannot be explicitly specified in code.
        ///  </para>
        /// </remarks>
        public Type GetFullNestedType(
            string nestedTypeName,
            params ReadOnlySpan<Type> nestedGenericTypes)
        {
            int nestedGenericCount = nestedGenericTypes.Length;
            string fullNestedTypeName = nestedTypeName;

            if (nestedGenericCount > 0)
            {
                fullNestedTypeName = $"{nestedTypeName}`{nestedGenericCount}";
            }

            Type nestedType = type.GetNestedType(fullNestedTypeName, BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new ArgumentException($"Could not find {nestedTypeName} in {type.Name}");

            if (!nestedType.IsGenericTypeDefinition)
            {
                return nestedType;
            }

            if (!type.IsGenericType)
            {
                // Parent has no generic parameters. Only the nested types are needed.
                return nestedGenericTypes.Length <= 0
                    ? throw new ArgumentException("Generic type parameters required for nested generic type.", nameof(nestedGenericTypes))
                    : nestedType.MakeGenericType(nestedGenericTypes.ToArray());
            }
            else if (type.IsGenericTypeDefinition)
            {
#pragma warning disable CA2208
                throw new ArgumentException("The parent type cannot be a type definition.", nameof(type));
#pragma warning restore CA2208
            }

            // Parent type has generic parameters.

            Type[] parentTypes = type.GenericTypeArguments;
            Type[] genericArguments = nestedType.GetGenericArguments();
            int genericArgumentCount = genericArguments.Length;

            if ((parentTypes.Length + nestedGenericCount) != genericArgumentCount)
            {
                throw new ArgumentException("Generic type parameter count does not match nested type definition.", nameof(nestedGenericTypes));
            }

            if (nestedGenericCount == 0)
            {
                // Just parent types.
                return nestedType.MakeGenericType(parentTypes);
            }

            // Combine parent and nested types.
            Type[] allTypes = new Type[genericArgumentCount];
            Array.Copy(parentTypes, allTypes, parentTypes.Length);
            for (int i = 0; i < nestedGenericTypes.Length; i++)
            {
                allTypes[parentTypes.Length + i] = nestedGenericTypes[i];
            }

            return nestedType.MakeGenericType(allTypes);
        }
    }
}
