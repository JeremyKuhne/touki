// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  One-shot glob matching helpers for callers that don't need to cache a compiled
///  <see cref="GlobSpecification"/>.
/// </summary>
public static class Glob
{
    /// <summary>
    ///  Compiles <paramref name="pattern"/> and tests whether <paramref name="input"/>
    ///  matches it. Equivalent to
    ///  <see cref="GlobSpecification.Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>
    ///  followed by <see cref="GlobSpecification.IsMatch(ReadOnlySpan{char})"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   This helper allocates a specification on every call. For repeated use of the
    ///   same pattern, compile once and cache the resulting <see cref="GlobSpecification"/>.
    ///  </para>
    /// </remarks>
    /// <exception cref="GlobFormatException">The pattern is invalid.</exception>
    public static bool IsMatch(
        StringSegment pattern,
        ReadOnlySpan<char> input,
        GlobDialect dialect,
        GlobOptions options = GlobOptions.None)
    {
        GlobSpecification specification = GlobSpecification.Compile(pattern, dialect, options);
        return specification.IsMatch(input);
    }
}
