// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Selects which case-folding rules a <see cref="GlobSpecification"/> applies when
///  <see cref="GlobOptions.IgnoreCase"/> is set. Each <see cref="GlobDialect"/> has a
///  documented default (see <see cref="GlobDialectExtensions.DefaultIgnoreCaseKind"/>);
///  the value is currently chosen by the factory and not user-configurable. See the
///  follow-up note on <see cref="GlobOptions.IgnoreCase"/>.
/// </summary>
internal enum IgnoreCaseKind
{
    /// <summary>
    ///  Case-sensitive ordinal matching. Selected when
    ///  <see cref="GlobOptions.IgnoreCase"/> is clear regardless of dialect.
    /// </summary>
    Off,

    /// <summary>
    ///  ASCII-only case fold: the 26 ASCII letter pairs (<c>A..Z</c>/<c>a..z</c>) compare
    ///  equal; every other code point (including non-ASCII Latin-1 letters, CJK, emoji)
    ///  compares ordinal. Matches the documented behavior of POSIX
    ///  <c>fnmatch(FNM_CASEFOLD)</c>, bash <c>nocaseglob</c>/<c>nocasematch</c>, and git
    ///  <c>core.ignoreCase</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Backed at runtime by <c>SpanExtensions.EqualsAsciiLetterIgnoreCase</c> /
    ///   <c>StartsWithAsciiLetterIgnoreCase</c> / <c>EndsWithAsciiLetterIgnoreCase</c>.
    ///   <b>Not</b> backed by <see cref="System.Text.Ascii.EqualsIgnoreCase(ReadOnlySpan{char}, ReadOnlySpan{char})"/>,
    ///   which is stricter: it returns <see langword="false"/> when either side contains
    ///   non-ASCII at all, even if both sides are byte-identical. POSIX semantics require
    ///   the non-ASCII characters to compare ordinal, not to short-circuit the match.
    ///  </para>
    /// </remarks>
    Ascii,

    /// <summary>
    ///  Full Unicode ordinal case fold matching <see cref="StringComparison.OrdinalIgnoreCase"/>.
    ///  Implements the behavior of MSBuild item globs, <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c>,
    ///  Win32 <c>FsRtlIsNameInExpression</c> with <c>IgnoreCase</c>, the .NET
    ///  <c>FileSystemName.MatchesSimpleExpression</c>, and PowerShell's <c>-like</c> operator.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Backed at runtime by <c>SpanExtensions.EqualsOrdinalIgnoreCase</c> /
    ///   <c>StartsWithOrdinalIgnoreCase</c> / <c>EndsWithOrdinalIgnoreCase</c>.
    ///  </para>
    /// </remarks>
    Unicode
}
