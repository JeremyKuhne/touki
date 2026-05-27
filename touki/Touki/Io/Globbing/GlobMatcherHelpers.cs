// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Shared helpers used by <see cref="GlobSpecification"/> implementations.
/// </summary>
internal static class GlobMatcherHelpers
{
    /// <summary>
    ///  ASCII-only ordinal case fold: lowercase letters fold to uppercase.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Used by <see cref="CompiledGlobStrategy"/>'s ignore-case path for per-character
    ///   character-class membership. The simple specialized matchers
    ///   (<see cref="LiteralGlobStrategy"/>, etc.) dispatch to BCL
    ///   <see cref="StringComparison.OrdinalIgnoreCase"/> overloads directly.
    ///  </para>
    /// </remarks>
    public static char AsciiFold(char c) =>
        (uint)(c - 'a') <= ('z' - 'a') ? (char)(c - ('a' - 'A')) : c;

    /// <summary>
    ///  Returns <see langword="true"/> when <paramref name="a"/> and <paramref name="b"/>
    ///  compare equal under ASCII-only ordinal case fold (the
    ///  <see cref="IgnoreCaseKind.Ascii"/> rule). Folds both sides to uppercase via
    ///  <see cref="AsciiFold"/> and compares.
    /// </summary>
    public static bool AsciiFoldEquals(char a, char b) => AsciiFold(a) == AsciiFold(b);

    /// <summary>
    ///  Returns <see langword="true"/> when <paramref name="a"/> and <paramref name="b"/>
    ///  compare equal under <see cref="IgnoreCaseKind.Unicode"/> (ordinal IC). Uses
    ///  <see cref="char.ToUpperInvariant"/> on both sides; matches the BCL's
    ///  <see cref="StringComparison.OrdinalIgnoreCase"/> semantics for the BMP.
    /// </summary>
    public static bool UnicodeFoldEquals(char a, char b) =>
        a == b || char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
}
