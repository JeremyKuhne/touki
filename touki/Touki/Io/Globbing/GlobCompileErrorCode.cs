// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Identifies the reason a <see cref="GlobSpecification"/> failed to compile.
/// </summary>
public enum GlobCompileErrorCode
{
    /// <summary>
    ///  No error.
    /// </summary>
    None = 0,

    /// <summary>
    ///  A character class <c>[…]</c> was not terminated before the end of the pattern.
    /// </summary>
    UnterminatedClass,

    /// <summary>
    ///  An extended-glob construct <c>?(…)</c>/<c>*(…)</c>/<c>+(…)</c>/<c>@(…)</c>/<c>!(…)</c>
    ///  was not terminated.
    /// </summary>
    UnterminatedExtGlob,

    /// <summary>
    ///  A backslash escape was placed at the end of the pattern with nothing to escape.
    /// </summary>
    DanglingEscape,

    /// <summary>
    ///  A character-class range had its endpoints out of order (for example <c>[z-a]</c>).
    /// </summary>
    InvalidClassRange,

    /// <summary>
    ///  A feature was used that is not allowed by the configured
    ///  <see cref="GlobDialect"/> or <see cref="GlobOptions"/>.
    /// </summary>
    FeatureNotEnabled,

    /// <summary>
    ///  The pattern exceeded an internal size limit (token count, literal length, etc.).
    /// </summary>
    PatternTooLarge
}
