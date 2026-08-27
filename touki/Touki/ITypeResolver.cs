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
    ///  Resolves the given type name. Throws if the type cannot be resolved.
    /// </summary>
    /// <param name="typeName">The type name to resolve.</param>
    /// <returns>The resolved type.</returns>
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
    Type BindToType(TypeName typeName);

    /// <summary>
    ///  Tries to resolve the given type name.
    /// </summary>
    /// <param name="typeName">The type name to resolve.</param>
    /// <param name="type">
    ///  When this method returns <see langword="true"/>, the resolved type; otherwise, <see langword="null"/>.
    /// </param>
    /// <returns>
    ///  <see langword="true"/> if <paramref name="typeName"/> was resolved; otherwise, <see langword="false"/>.
    /// </returns>
    bool TryBindToType(
        TypeName typeName,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All), NotNullWhen(true)] out Type? type);
}