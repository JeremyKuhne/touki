// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Polyfill of System.Runtime.CompilerServices.UnionAttribute for targets that predate .NET 11
// (net10.0 and net472). .NET 11 ships it in the BCL, so it is excluded there. This lives outside the
// net472-only Framework/ tree because it is needed on net10.0 as well; the guard selects every
// non-net11 target. IUnion is intentionally NOT polyfilled - the IUnionMembers provider pattern that
// Value uses to become a union does not reference it.
#if !NET11_0_OR_GREATER

namespace System.Runtime.CompilerServices;

/// <summary>
///  Marks a <see langword="class"/> or <see langword="struct"/> as a union type for the C# union feature.
/// </summary>
/// <remarks>
///  <para>
///   The compiler recognizes a type carrying this attribute as a union type and enables union behaviors
///   (union conversions, union matching, and exhaustiveness) when the type follows the union member pattern.
///  </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class UnionAttribute : Attribute;

#endif
