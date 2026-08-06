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
    UnterminatedClass = 1,

    /// <summary>
    ///  An extended-glob construct <c>?(…)</c>/<c>*(…)</c>/<c>+(…)</c>/<c>@(…)</c>/<c>!(…)</c>
    ///  was not terminated.
    /// </summary>
    UnterminatedExtGlob = 2,

    /// <summary>
    ///  A backslash escape was placed at the end of the pattern with nothing to escape.
    /// </summary>
    DanglingEscape = 3,

    /// <summary>
    ///  A character-class range had its endpoints out of order (for example <c>[z-a]</c>).
    /// </summary>
    InvalidClassRange = 4,

    /// <summary>
    ///  A feature was used that is not allowed by the configured
    ///  <see cref="GlobDialect"/> or <see cref="GlobOptions"/>.
    /// </summary>
    FeatureNotEnabled = 5,

    /// <summary>
    ///  The pattern exceeded an internal size limit (token count, literal length, etc.).
    /// </summary>
    PatternTooLarge = 6,

    /// <summary>
    ///  An extended-glob construct's body was malformed (for example, an empty
    ///  alternation list <c>?()</c>).
    /// </summary>
    InvalidExtGlobBody = 7,

    /// <summary>
    ///  An extended-glob construct exceeded an internal complexity limit
    ///  (nesting depth or alternative count).
    /// </summary>
    FeatureLimitExceeded = 8,

    /// <summary>
    ///  A FileSystemGlobbing parent segment (<c>..</c>) appeared after a
    ///  non-parent segment.
    /// </summary>
    ParentSegmentNotAtBeginning = 9
}
