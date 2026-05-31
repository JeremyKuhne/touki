// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Selects the path separator character used by path-aware glob dialects at match time.
/// </summary>
/// <remarks>
///  <para>
///   The matcher accepts exactly one separator character at match time; inputs that
///   mix separators must be normalized by the caller. See design decision D2 in
///   <c>docs/globbing-feature-plan.md</c>.
///  </para>
///  <para>
///   Each path-aware dialect documents a default separator
///   (<see cref="GlobDialect.PosixPath"/>, <see cref="GlobDialect.Bash"/>,
///   <see cref="GlobDialect.Git"/>, <see cref="GlobDialect.MSBuild"/>, and
///   <see cref="GlobDialect.FileSystemGlobbing"/> default to forward-slash). Passing this enum
///   to <see cref="GlobSpecification.Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>
///   overrides that default; <see cref="DialectDefault"/> keeps it.
///  </para>
///  <para>
///   For path-unaware dialects (<see cref="GlobDialect.Posix"/>,
///   <see cref="GlobDialect.Simple"/>, <see cref="GlobDialect.PowerShell"/>) this
///   parameter is ignored; the matcher has no notion of segments and wildcards cross
///   any character.
///  </para>
/// </remarks>
public enum GlobPathSeparator
{
    /// <summary>
    ///  Use the dialect's documented default separator.
    /// </summary>
    DialectDefault,

    /// <summary>
    ///  Use the OS directory separator (<see cref="System.IO.Path.DirectorySeparatorChar"/>):
    ///  <c>\</c> on Windows, <c>/</c> on Unix-like systems.
    /// </summary>
    OSDefault,

    /// <summary>
    ///  Use <c>/</c> regardless of OS.
    /// </summary>
    ForwardSlash,

    /// <summary>
    ///  Use <c>\</c> regardless of OS.
    /// </summary>
    Backslash,
}
