// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Extensions on <see cref="GlobDialect"/> that translate dialect metadata into the
///  primitives the matchers consume.
/// </summary>
internal static class GlobDialectExtensions
{
    /// <summary>
    ///  Returns the case-fold rule a given dialect applies when
    ///  <see cref="GlobOptions.IgnoreCase"/> is set. See
    ///  <see cref="GlobDialect"/> for the per-dialect source citations.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   POSIX-family dialects (<see cref="GlobDialect.Posix"/>,
    ///   <see cref="GlobDialect.PosixPath"/>, <see cref="GlobDialect.Bash"/>,
    ///   <see cref="GlobDialect.Git"/>) default to <see cref="IgnoreCaseKind.Ascii"/> because
    ///   the documented reference implementations fold only the ASCII letter range in their
    ///   POSIX/C locale.
    ///  </para>
    ///  <para>
    ///   .NET-native and Windows-native dialects
    ///   (<see cref="GlobDialect.MSBuild"/>, <see cref="GlobDialect.FileSystemGlobbing"/>,
    ///   <see cref="GlobDialect.Win32"/>, <see cref="GlobDialect.Simple"/>,
    ///   <see cref="GlobDialect.PowerShell"/>) default to <see cref="IgnoreCaseKind.Unicode"/>
    ///   because their documented implementations use
    ///   <see cref="StringComparison.OrdinalIgnoreCase"/> or the Windows kernel
    ///   <c>RtlUpcaseUnicodeChar</c> case table.
    ///  </para>
    /// </remarks>
    public static IgnoreCaseKind DefaultIgnoreCaseKind(this GlobDialect dialect, GlobOptions options)
    {
        // MSBuild matches case-insensitively by default; the IgnoreCase flag is
        // implicit. All other dialects require the flag to be set explicitly.
        if (dialect == GlobDialect.MSBuild)
        {
            return IgnoreCaseKind.Unicode;
        }

        if ((options & GlobOptions.IgnoreCase) == 0)
        {
            return IgnoreCaseKind.Off;
        }

        return dialect switch
        {
            GlobDialect.Posix
                or GlobDialect.PosixPath
                or GlobDialect.Bash
                or GlobDialect.Git => IgnoreCaseKind.Ascii,
            _ => IgnoreCaseKind.Unicode,
        };
    }

    /// <summary>
    ///  Returns the escape character the scanner should honor when parsing patterns for
    ///  the given dialect, or <c>'\0'</c> when the dialect has no escape character.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   POSIX-family, MSBuild, FileSystemGlobbing, and Win32 dialects use backslash
    ///   (<c>\</c>) as the escape character. PowerShell <c>WildcardPattern</c> /
    ///   <c>-like</c> uses backtick (<c>`</c>) per
    ///   <see href="https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_wildcards">
    ///   about_Wildcards</see>. The <see cref="GlobDialect.Simple"/> dialect (matching
    ///   <c>FileSystemName.MatchesSimpleExpression</c>) has no escape character.
    ///  </para>
    ///  <para>
    ///   When <see cref="GlobOptions.NoEscape"/> is set the method returns <c>'\0'</c>
    ///   regardless of dialect.
    ///  </para>
    /// </remarks>
    public static char GetEscapeChar(this GlobDialect dialect, GlobOptions options)
    {
        if ((options & GlobOptions.NoEscape) != 0
            || dialect == GlobDialect.Simple
            || dialect == GlobDialect.MSBuild
            || dialect == GlobDialect.FileSystemGlobbing)
        {
            return '\0';
        }

        return dialect == GlobDialect.PowerShell ? '`' : '\\';
    }

    /// <summary>
    ///  Returns <see langword="true"/> when the dialect's documented default allows a
    ///  leading <c>.</c> in the input to be matched by a wildcard (<c>?</c>, <c>*</c>,
    ///  or a character class), without the caller having to set
    ///  <see cref="GlobOptions.MatchLeadingDot"/> explicitly.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   POSIX-family dialects (<see cref="GlobDialect.Posix"/>,
    ///   <see cref="GlobDialect.PosixPath"/>, <see cref="GlobDialect.Bash"/>,
    ///   <see cref="GlobDialect.Git"/>) treat a leading <c>.</c> as a "hidden" marker
    ///   per <c>fnmatch</c>'s <c>FNM_PERIOD</c> rule, requiring a literal <c>.</c>
    ///   in the pattern. All other dialects - including PowerShell, Win32,
    ///   Simple, MSBuild, FileSystemGlobbing - do not.
    ///  </para>
    /// </remarks>
    public static bool MatchesLeadingDotByDefault(this GlobDialect dialect) => dialect switch
    {
        GlobDialect.Posix
            or GlobDialect.PosixPath
            or GlobDialect.Bash
            or GlobDialect.Git => false,
        _ => true,
    };

    /// <summary>
    ///  Returns <see langword="true"/> when the dialect is path-aware: wildcards
    ///  (<c>?</c> and <c>*</c>) do not match the configured path separator, and
    ///  matching the separator requires a literal or a globstar (<c>**</c>) when
    ///  enabled. Path-unaware dialects treat the separator as an ordinary character.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Path-aware dialects: <see cref="GlobDialect.PosixPath"/>,
    ///   <see cref="GlobDialect.Bash"/>, <see cref="GlobDialect.Git"/>,
    ///   <see cref="GlobDialect.MSBuild"/>,
    ///   <see cref="GlobDialect.FileSystemGlobbing"/>. All other dialects
    ///   - including <see cref="GlobDialect.Posix"/> - are
    ///   path-unaware.
    ///  </para>
    /// </remarks>
    public static bool IsPathAware(this GlobDialect dialect) => dialect switch
    {
        GlobDialect.PosixPath
            or GlobDialect.Bash
            or GlobDialect.Git
            or GlobDialect.MSBuild
            or GlobDialect.FileSystemGlobbing => true,

        _ => false,
    };

    /// <summary>
    ///  Returns the path separator character the dialect's documented default uses
    ///  for path-aware matching. For path-unaware dialects returns <c>'\0'</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   POSIX-family dialects (<see cref="GlobDialect.PosixPath"/>,
    ///   <see cref="GlobDialect.Bash"/>, <see cref="GlobDialect.Git"/>),
    ///   <see cref="GlobDialect.FileSystemGlobbing"/>, and
    ///   <see cref="GlobDialect.MSBuild"/> default to <c>'/'</c>.
    ///  </para>
    ///  <para>
    ///   Callers can override the per-dialect default through the
    ///   <see cref="GlobPathSeparator"/> argument on
    ///   <see cref="GlobSpecification.Compile(StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>.
    ///  </para>
    /// </remarks>
    public static char DefaultSeparator(this GlobDialect dialect) => dialect switch
    {
        GlobDialect.PosixPath
            or GlobDialect.Bash
            or GlobDialect.Git
            or GlobDialect.FileSystemGlobbing
            or GlobDialect.MSBuild => '/',
        _ => '\0',
    };

    /// <summary>
    ///  Returns <see langword="true"/> when the dialect supports POSIX-style character
    ///  class expressions (<c>[abc]</c>, <c>[a-z]</c>, <c>[:alpha:]</c>, etc.).
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <see cref="GlobDialect.Simple"/> (matching
    ///   <c>FileSystemName.MatchesSimpleExpression</c>),
    ///   <see cref="GlobDialect.MSBuild"/>, and
    ///   <see cref="GlobDialect.FileSystemGlobbing"/> treat <c>[</c> and <c>]</c> as
    ///   literal characters. Every other dialect supports bracket expressions.
    ///  </para>
    /// </remarks>
    public static bool HasCharacterClasses(this GlobDialect dialect) =>
        dialect is not (GlobDialect.Simple or GlobDialect.MSBuild or GlobDialect.FileSystemGlobbing);

    /// <summary>
    ///  Returns <see langword="true"/> when the dialect enables <c>**</c> globstar by
    ///  default (without an explicit <see cref="GlobOptions.AllowGlobStar"/> flag).
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <see cref="GlobDialect.MSBuild"/>,
    ///   <see cref="GlobDialect.FileSystemGlobbing"/>, and
    ///   <see cref="GlobDialect.Git"/> always support <c>**</c> per their documented
    ///   behavior. <see cref="GlobDialect.Bash"/> matches <c>shopt -s globstar</c>
    ///   (off by default; opt in via the flag). Other dialects ignore the flag because
    ///   they are not path-aware.
    ///  </para>
    /// </remarks>
    public static bool GlobStarIsImplicit(this GlobDialect dialect) =>
        dialect is GlobDialect.MSBuild or GlobDialect.FileSystemGlobbing or GlobDialect.Git;
}
