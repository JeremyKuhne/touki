// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Marks a value type as "non-copyable": copying an instance by value is a likely bug because the type owns a
///  resource (a pooled or manually managed buffer, a handle, a lock) that must not be duplicated.
/// </summary>
/// <remarks>
///  <para>
///   The Touki analyzers (<c>TOUKI0003</c> and <c>TOUKI0004</c>) report defensive copies and by-value copies of
///   types annotated with this attribute. A type is recognized by this attribute's fully qualified name,
///   <c>Touki.NonCopyableAttribute</c>; a marker attribute of the same simple name in a different namespace is not
///   recognized.
///  </para>
///  <para>
///   Applying this attribute does not change runtime behavior; it only drives static analysis. Prefer passing
///   annotated values by <see langword="ref"/>, <see langword="in"/>, or <see langword="ref"/>
///   <see langword="readonly"/> rather than by value.
///  </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.GenericParameter, AllowMultiple = false, Inherited = false)]
public sealed class NonCopyableAttribute : Attribute
{
}
