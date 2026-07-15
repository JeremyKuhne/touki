// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Reflection.Metadata;

namespace Touki;

/// <summary>
///  Resolver for types.
/// </summary>
public interface ITypeResolver
{
    /// <summary>
    ///  Resolves the given type name against the specified library. Throws if the type cannot be resolved.
    /// </summary>
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    Type BindToType(TypeName typeName);

    /// <summary>
    ///  Tries to resolve the given type name against the specified library.
    /// </summary>
    bool TryBindToType(
        TypeName typeName,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All), NotNullWhen(true)] out Type? type);
}