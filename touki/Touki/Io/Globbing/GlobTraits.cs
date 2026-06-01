// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Compile-time properties of an encoded glob program, discovered as a side effect
///  of the single encode pass and consumed by <see cref="CompiledGlobStrategy"/> to
///  select match-loop variants and gate optional fast paths.
/// </summary>
[Flags]
internal enum GlobTraits
{
    /// <summary>
    ///  The program contains no globstar, extended-glob, or negation constructs.
    /// </summary>
    None = 0,

    /// <summary>
    ///  The program contains at least one <see cref="GlobOpCodes.GlobStar"/> opcode.
    ///  Selects the globstar-aware match loop over the simpler single-savepoint
    ///  variant (the common case - only a handful of patterns in a typical project
    ///  use <c>**</c>).
    /// </summary>
    GlobStar = 1 << 0,

    /// <summary>
    ///  The program contains at least one extended-glob alternation block
    ///  (<c>?(...)</c>, <c>*(...)</c>, <c>+(...)</c>, <c>&#x40;(...)</c>, or
    ///  <c>!(...)</c>). Selects the recursive alternation walker.
    /// </summary>
    ExtGlob = 1 << 1,

    /// <summary>
    ///  The program contains at least one negation alternation (<c>!(...)</c>),
    ///  top-level or nested. Gates the directory-pruning path.
    /// </summary>
    Negation = 1 << 2,
}
