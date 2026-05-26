// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Selects the glob pattern dialect that <see cref="GlobSpecification"/> compiles.
/// </summary>
/// <remarks>
///  <para>
///   Each dialect targets the documented behavior of an existing matcher. See
///   <c>docs/globbing.md</c> for the dialect matrix and intentional deviations.
///  </para>
/// </remarks>
public enum GlobDialect
{
    /// <summary>
    ///  POSIX <c>fnmatch</c> without <c>FNM_PATHNAME</c>. Wildcards match across
    ///  any character including <c>/</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://pubs.opengroup.org/onlinepubs/9699919799/utilities/V3_chap02.html#tag_18_13">
    ///   IEEE Std 1003.1 §2.13 Pattern Matching Notation</see> and
    ///   <see href="https://man7.org/linux/man-pages/man3/fnmatch.3p.html"><c>fnmatch(3p)</c></see>.
    ///  </para>
    /// </remarks>
    Posix,

    /// <summary>
    ///  <see cref="Posix"/> with path-mode semantics (<c>FNM_PATHNAME</c>). Wildcards
    ///  do not cross separator characters.
    /// </summary>
    PosixPath,

    /// <summary>
    ///  Bash pattern matching including <c>globstar</c> and <c>extglob</c> when enabled
    ///  in <see cref="GlobOptions"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://www.gnu.org/software/bash/manual/html_node/Pattern-Matching.html">
    ///   GNU Bash — Pattern Matching</see>.
    ///  </para>
    /// </remarks>
    Bash,

    /// <summary>
    ///  Git <c>wildmatch</c> / <c>.gitignore</c> semantics.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://git-scm.com/docs/gitignore#_pattern_format">gitignore</see>.
    ///  </para>
    /// </remarks>
    Git,

    /// <summary>
    ///  MSBuild item-wildcard semantics.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/visualstudio/msbuild/msbuild-items#using-wildcards-to-specify-items">
    ///   MSBuild item wildcards</see>.
    ///  </para>
    /// </remarks>
    MSBuild,

    /// <summary>
    ///  <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c> semantics. May diverge
    ///  from <see cref="MSBuild"/> on edge cases; see <c>docs/globbing.md</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.filesystemglobbing.matcher">
    ///   <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c></see>.
    ///  </para>
    /// </remarks>
    FileSystemGlobbing,

    /// <summary>
    ///  Win32 <c>FsRtlIsNameInExpression</c> semantics including the DOS metacharacters
    ///  <c>&lt;</c>, <c>&gt;</c>, and <c>"</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matcheswin32expression">
    ///   <c>FileSystemName.MatchesWin32Expression</c></see>.
    ///  </para>
    ///  <para>
    ///   <b>Not yet implemented.</b> Reserved for a future release; passing this value
    ///   to <see cref="GlobSpecification.Compile(Text.StringSegment, GlobDialect, GlobOptions, GlobPathSeparator, int)"/>
    ///   throws <see cref="GlobFormatException"/> with
    ///   <see cref="GlobCompileErrorCode.FeatureNotEnabled"/>.
    ///  </para>
    /// </remarks>
    [Obsolete("GlobDialect.Win32 is reserved but not yet implemented. Compiling with this value throws GlobFormatException.", error: false)]
    Win32,

    /// <summary>
    ///  Simple file-name expression matching (<c>*</c> and <c>?</c> only).
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matchessimpleexpression">
    ///   <c>FileSystemName.MatchesSimpleExpression</c></see>.
    ///  </para>
    /// </remarks>
    Simple,

    /// <summary>
    ///  PowerShell <c>-like</c> / <c>WildcardPattern</c> semantics.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_wildcards">
    ///   about_Wildcards</see>.
    ///  </para>
    /// </remarks>
    PowerShell
}
