// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Selects the glob pattern dialect that <see cref="GlobSpecification"/> compiles.
/// </summary>
/// <remarks>
///  <para>
///   Each dialect models the syntax and core behavior of an existing matcher.
///   Defaults and edge cases are not always drop-in compatible. See
///   <c>docs/globbing.md</c> for the dialect matrix and intentional differences.
///  </para>
/// </remarks>
public enum GlobDialect
{
    /// <summary>
    ///  POSIX <c>fnmatch</c> without <c>FNM_PATHNAME</c>. Wildcards match across
    ///  any character including <c>/</c>. Touki applies <c>FNM_PERIOD</c>-style
    ///  leading-dot protection by default; use <see cref="GlobOptions.MatchLeadingDot"/>
    ///  to model a call without that flag.
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
    ///  do not cross separator characters. Leading-dot protection currently applies
    ///  only at the start of the complete input, not after every separator.
    /// </summary>
    PosixPath,

    /// <summary>
    ///  Bash pattern matching including <c>globstar</c> and <c>extglob</c> when enabled
    ///  in <see cref="GlobOptions"/>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://www.gnu.org/software/bash/manual/html_node/Pattern-Matching.html">
    ///   GNU Bash - Pattern Matching</see>.
    ///  </para>
    /// </remarks>
    Bash,

    /// <summary>
    ///  Git <c>wildmatch</c> / <c>.gitignore</c> semantics. Touki protects a
    ///  leading dot by default; use <see cref="GlobOptions.MatchLeadingDot"/> to
    ///  align with ordinary Git <c>wildmatch</c> behavior.
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
    ///  from <see cref="MSBuild"/> on edge cases. Touki is case-sensitive by default,
    ///  unlike the parameterless <c>Matcher</c>. Ordinary <c>?</c> characters are
    ///  literals, not single-character wildcards; see <c>docs/globbing.md</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.filesystemglobbing.matcher">
    ///   <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c></see>.
    ///  </para>
    /// </remarks>
    FileSystemGlobbing,

    /// <summary>
    ///  Simple file-name expression matching (<c>*</c> and <c>?</c> only). Touki
    ///  defaults to case-sensitive matching and treats backslash literally, unlike
    ///  the defaults of <c>FileSystemName.MatchesSimpleExpression</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matchessimpleexpression">
    ///   <c>FileSystemName.MatchesSimpleExpression</c></see>.
    ///  </para>
    /// </remarks>
    Simple,

    /// <summary>
    ///  PowerShell <c>WildcardPattern</c>-style semantics. Touki defaults to
    ///  case-sensitive matching; use <see cref="GlobOptions.IgnoreCase"/> for
    ///  <c>-like</c>-style casing. Bracket-expression extensions differ; see
    ///  <c>docs/globbing.md</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   See <see href="https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_wildcards">
    ///   about_Wildcards</see>.
    ///  </para>
    /// </remarks>
    PowerShell
}
