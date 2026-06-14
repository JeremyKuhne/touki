// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Marks a type whose instances must be deterministically disposed - released on every code path via a
///  <see langword="using"/> declaration/statement or a <see langword="try"/>/<see langword="finally"/> that calls
///  <c>Dispose</c>. Such a type owns a resource (a pooled or manually managed buffer, a handle, a lock, a
///  reference count) whose release cannot be left to the garbage collector.
/// </summary>
/// <remarks>
///  <para>
///   The Touki analyzer <c>TOUKI0010</c> reports a freshly produced value of an annotated type that is bound to a
///   local and then neither disposed, placed in a <see langword="using"/>, nor handed off (returned, stored, or
///   passed on). A type is recognized by this attribute's fully qualified name,
///   <c>Touki.MustDisposeAttribute</c>; a marker attribute of the same simple name in a different namespace is not
///   recognized.
///  </para>
///  <para>
///   This is the disposal counterpart to <see cref="NonCopyableAttribute"/>: a scope type that owns a resource
///   commonly wants both - <c>[NonCopyable]</c> so the single owner is not duplicated and <c>[MustDispose]</c> so
///   the owner is not leaked. Applying this attribute does not change runtime behavior; it only drives static
///   analysis.
///  </para>
///  <para>
///   No off-the-shelf analyzer covers what this marker enables, for three reasons:
///  </para>
///  <para>
///   - <b>The scope types dispose by pattern, not by interface.</b> The owning types this attribute targets are
///   commonly <see langword="ref"/> structs consumed by a <see langword="using"/> through a public <c>Dispose</c>
///   method, and a <see langword="ref"/> struct cannot implement <see cref="IDisposable"/>. The .NET SDK rule
///   <c>CA2000</c> and the third-party IDisposableAnalyzers (<c>IDISP*</c>) are built around the
///   <see cref="IDisposable"/> <em>interface</em>, so they never see these values at all.
///  </para>
///  <para>
///   - <b>This is an opt-in marker, not a heuristic.</b> <c>CA2000</c> ships disabled by default because it guesses
///   which values "look disposable" and is prone to false positives. <c>TOUKI0010</c> fires only on types
///   deliberately annotated with this attribute, so it is as precise as <see cref="NonCopyableAttribute"/> rather
///   than a guess.
///  </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MustDisposeAttribute : Attribute
{
}
