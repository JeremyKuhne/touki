// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Options that modify how a <see cref="GlobSpecification"/> is compiled and matched.
/// </summary>
[Flags]
public enum GlobOptions
{
    /// <summary>
    ///  Use the per-<see cref="GlobDialect"/> defaults: no extra options applied beyond
    ///  what the dialect itself specifies.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The defaults vary by dialect:
    ///  </para>
    ///  <para>
    ///   <list type="bullet">
    ///    <item>
    ///     <description>
    ///      <b>Case folding</b>: <see cref="GlobDialect.MSBuild"/> is Unicode ordinal
    ///      case-insensitive by default; every other dialect is case-sensitive by
    ///      default and requires <see cref="IgnoreCase"/> for case-insensitive matching.
    ///      Current <c>MSBuildGlob</c> and <c>FileMatcher.IsMatch</c> APIs do not expose
    ///      case-sensitive matching.
    ///     </description>
    ///    </item>
    ///    <item>
    ///     <description>
    ///      <b>Leading dot</b>: <see cref="GlobDialect.Posix"/>,
    ///      <see cref="GlobDialect.PosixPath"/>, <see cref="GlobDialect.Bash"/>, and
    ///      <see cref="GlobDialect.Git"/> require a literal <c>.</c> in the pattern to
    ///      match a leading <c>.</c> in the input (POSIX <c>FNM_PERIOD</c>). Other
    ///      dialects allow wildcards to consume a leading dot. Override with
    ///      <see cref="MatchLeadingDot"/>.
    ///     </description>
    ///    </item>
    ///    <item>
    ///     <description>
    ///      <b>Globstar</b> (<c>**</c>): implicitly enabled for
    ///      <see cref="GlobDialect.MSBuild"/>, <see cref="GlobDialect.FileSystemGlobbing"/>,
    ///      and <see cref="GlobDialect.Git"/>. <see cref="GlobDialect.Bash"/> and other
    ///      path-aware dialects require <see cref="AllowGlobStar"/>.
    ///     </description>
    ///    </item>
    ///    <item>
    ///     <description>
    ///      <b>Escape character</b>: honored by POSIX-family, Bash, Git
    ///      (<c>\</c>) and PowerShell (<c>`</c>). <see cref="GlobDialect.MSBuild"/>,
    ///      <see cref="GlobDialect.FileSystemGlobbing"/>, and
    ///      <see cref="GlobDialect.Simple"/> have no escape character. Suppress
    ///      with <see cref="NoEscape"/>.
    ///     </description>
    ///    </item>
    ///   </list>
    ///  </para>
    /// </remarks>
    None = 0,

    /// <summary>
    ///  Match characters case-insensitively.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The exact case-fold rule depends on the <see cref="GlobDialect"/> the
    ///   specification is compiled with:
    ///  </para>
    ///  <para>
    ///   <list type="bullet">
    ///    <item>
    ///     <description>
    ///      <see cref="GlobDialect.Posix"/>, <see cref="GlobDialect.PosixPath"/>,
    ///      <see cref="GlobDialect.Bash"/>, and <see cref="GlobDialect.Git"/> use
    ///      <b>ASCII-only</b> case folding - only the 26 ASCII letter pairs match
    ///      case-insensitively; non-ASCII characters compare strictly. This matches the
    ///      documented behavior of POSIX
    ///      <see href="https://man7.org/linux/man-pages/man3/fnmatch.3.html"><c>fnmatch(FNM_CASEFOLD)</c></see>,
    ///      bash
    ///      <see href="https://www.gnu.org/software/bash/manual/html_node/The-Shopt-Builtin.html#index-nocaseglob"><c>nocaseglob</c></see>/<see href="https://www.gnu.org/software/bash/manual/html_node/The-Shopt-Builtin.html#index-nocasematch"><c>nocasematch</c></see>,
    ///      and git
    ///      <see href="https://git-scm.com/docs/git-config#Documentation/git-config.txt-coreignoreCase"><c>core.ignoreCase</c></see>.
    ///     </description>
    ///    </item>
    ///    <item>
    ///     <description>
    ///      <see cref="GlobDialect.MSBuild"/>, <see cref="GlobDialect.FileSystemGlobbing"/>,
    ///      <see cref="GlobDialect.Simple"/>, and
    ///      <see cref="GlobDialect.PowerShell"/> use <b>full Unicode</b> ordinal case
    ///      folding equivalent to <see cref="StringComparison.OrdinalIgnoreCase"/>. This
    ///      matches the documented behavior of
    ///      <see href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.filesystemglobbing.matcher"><c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c></see>,
    ///      <see href="https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matchessimpleexpression"><c>FileSystemName.MatchesSimpleExpression</c></see>,
    ///      and PowerShell
    ///      <see href="https://learn.microsoft.com/powershell/module/microsoft.powershell.core/about/about_wildcards"><c>-like</c></see>.
    ///     </description>
    ///    </item>
    ///   </list>
    ///  </para>
    ///  <para>
    ///   The dialect default is currently fixed; future versions may add an option to
    ///   force <c>IgnoreCaseKind.Ascii</c> or <c>IgnoreCaseKind.Unicode</c> explicitly
    ///   regardless of dialect. Internally the compiled specification already tracks the chosen
    ///   kind separately from this flag.
    ///  </para>
    /// </remarks>
    IgnoreCase = 1 << 0,

    /// <summary>
    ///  When set, wildcards (<c>?</c>, <c>*</c>, character classes) may match a leading
    ///  <c>.</c>, overriding dialects that otherwise require a literal dot.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Defaults vary by dialect. POSIX-family dialects and the current Git dialect
    ///   require a literal leading dot; other dialects allow wildcard matches. The
    ///   current path-aware implementation applies this restriction only at the start
    ///   of the complete input, not after each separator.
    ///  </para>
    /// </remarks>
    MatchLeadingDot = 1 << 1,

    /// <summary>
    ///  Disable the dialect's escape character, treating it as a literal. This is
    ///  backslash for POSIX-family, Bash, and Git patterns, and backtick for PowerShell.
    /// </summary>
    NoEscape = 1 << 2,

    /// <summary>
    ///  Enable the <c>**</c> (globstar) wildcard. Only meaningful for path-aware dialects
    ///  such as <see cref="GlobDialect.PosixPath"/>, <see cref="GlobDialect.Bash"/>,
    ///  <see cref="GlobDialect.Git"/>, <see cref="GlobDialect.MSBuild"/>, and
    ///  <see cref="GlobDialect.FileSystemGlobbing"/>.
    /// </summary>
    AllowGlobStar = 1 << 3,

    /// <summary>
    ///  Enable extended-glob constructs: <c>?(...)</c>, <c>*(...)</c>, <c>+(...)</c>,
    ///  <c>@(...)</c>, <c>!(...)</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Models bash extglob (<c>shopt -s extglob</c>) and the GNU/glibc
    ///   <c>fnmatch(FNM_EXTMATCH)</c> extension. Each construct is a
    ///   <c>|</c>-separated list of inner glob patterns. Inner wildcards still
    ///   respect path semantics - wildcard tokens inside an alternative do not
    ///   cross the path separator on path-aware dialects. FileSystemGlobbing
    ///   continues to treat an ordinary <c>?</c> inside the body as a literal.
    ///   When a FileSystemGlobbing pattern contains extglob, its whole-pattern
    ///   compatibility rewrites (<c>*.*</c>, leading <c>**.</c>, separator
    ///   runs, trailing separators, and parent placement) are not applied.
    ///  </para>
    ///  <para>
    ///   The compile pipeline enforces hard limits to bound worst-case
    ///   alternation backtracking: max nesting depth of 8 levels, max 32
    ///   alternatives per construct. Patterns that exceed either cap fail to
    ///   compile with <see cref="GlobCompileErrorCode.FeatureLimitExceeded"/>.
    ///   Empty bodies (<c>?()</c>) fail with
    ///   <see cref="GlobCompileErrorCode.InvalidExtGlobBody"/>; an empty
    ///   alternative (<c>?(|)</c>) is allowed and matches the empty string.
    ///  </para>
    /// </remarks>
    AllowExtGlob = 1 << 4
}
