// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Type-name matching adapted from dotnet/winforms at
// 73f0222ea7a75610ba883cac9807bd3a003b6d53 (MIT licensed):
// src/System.Private.Windows.Core/src/System/Reflection/Metadata/TypeNameComparer.cs

using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Touki;

public sealed partial class RegisteredTypeResolver
{
    /// <summary>
    ///  Compares metadata type names by ordinal full name while accounting for array shape and generic arguments.
    /// </summary>
    private sealed class FullNameTypeNameComparer : IEqualityComparer<TypeName>
    {
        internal static FullNameTypeNameComparer Instance { get; } = new();

        public bool Equals(TypeName? left, TypeName? right)
        {
            if (left is null || right is null)
            {
                return left is null && right is null;
            }

            if (left.IsArray || right.IsArray)
            {
                return left.IsArray
                    && right.IsArray
                    && left.IsSZArray == right.IsSZArray
                    && left.GetArrayRank() == right.GetArrayRank()
                    && Equals(left.GetElementType(), right.GetElementType());
            }

            if (left.IsConstructedGenericType || right.IsConstructedGenericType)
            {
                if (!left.IsConstructedGenericType
                    || !right.IsConstructedGenericType
                    || !Equals(left.GetGenericTypeDefinition(), right.GetGenericTypeDefinition()))
                {
                    return false;
                }

                ImmutableArray<TypeName> leftArguments = left.GetGenericArguments();
                ImmutableArray<TypeName> rightArguments = right.GetGenericArguments();
                if (leftArguments.Length != rightArguments.Length)
                {
                    return false;
                }

                for (int index = 0; index < leftArguments.Length; index++)
                {
                    if (!Equals(leftArguments[index], rightArguments[index]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return string.Equals(left.FullName, right.FullName, StringComparison.Ordinal);
        }

        public int GetHashCode(TypeName typeName)
        {
            if (typeName.IsArray)
            {
                return HashCode.Combine(
                    typeName.IsSZArray,
                    typeName.GetArrayRank(),
                    GetHashCode(typeName.GetElementType()));
            }

            if (typeName.IsConstructedGenericType)
            {
                HashCode hashCode = new();
                hashCode.Add(GetHashCode(typeName.GetGenericTypeDefinition()));
                foreach (TypeName argument in typeName.GetGenericArguments())
                {
                    hashCode.Add(GetHashCode(argument));
                }

                return hashCode.ToHashCode();
            }

            return StringComparer.Ordinal.GetHashCode(typeName.FullName);
        }
    }
}
