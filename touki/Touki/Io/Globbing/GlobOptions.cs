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
    ///      <b>Case folding</b>: <see cref="GlobDialect.MSBuild"/> is
    ///      case-insensitive (Unicode) even without <see cref="IgnoreCase"/>; every
    ///      other dialect is case-sensitive by default.
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
    ///      <see cref="GlobDialect.Bash"/>, and <see cref="GlobDialect.Git"/>. Other
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
    ///      <b>ASCII-only</b> case folding &#8211; only the 26 ASCII letter pairs match
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
    ///      <see cref="GlobDialect.Win32"/>, <see cref="GlobDialect.Simple"/>, and
    ///      <see cref="GlobDialect.PowerShell"/> use <b>full Unicode</b> ordinal case
    ///      folding equivalent to <see cref="StringComparison.OrdinalIgnoreCase"/>. This
    ///      matches the documented behavior of
    ///      <see href="https://learn.microsoft.com/dotnet/api/microsoft.extensions.filesystemglobbing.matcher"><c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c></see>,
    ///      <see href="https://learn.microsoft.com/dotnet/api/system.io.enumeration.filesystemname.matchessimpleexpression"><c>FileSystemName.MatchesSimpleExpression</c></see>,
    ///      Win32
    ///      <see href="https://learn.microsoft.com/windows-hardware/drivers/ddi/ntifs/nf-ntifs-fsrtlisnameinexpression"><c>FsRtlIsNameInExpression</c></see>,
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
    ///  <c>.</c>. Equivalent to <c>fnmatch</c> being called without <c>FNM_PERIOD</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   The default (flag cleared) matches POSIX behavior: a leading <c>.</c> must be
    ///   matched by a literal <c>.</c> in the pattern.
    ///  </para>
    /// </remarks>
    MatchLeadingDot = 1 << 1,

    /// <summary>
    ///  Treat the backslash character as a literal rather than as an escape character.
    ///  Equivalent to <c>FNM_NOESCAPE</c>.
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
    ///  Enable extended-glob constructs: <c>?(…)</c>, <c>*(…)</c>, <c>+(…)</c>,
    ///  <c>@(…)</c>, <c>!(…)</c>.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   <b>Not yet implemented.</b> The flag is reserved on the public surface so
    ///   the eventual API doesn't shift bit values, but the compile pipeline does
    ///   not currently parse or match extglob constructs. Patterns containing
    ///   <c>?(…)</c>, <c>*(…)</c>, <c>+(…)</c>, <c>@(…)</c>, or <c>!(…)</c> compile
    ///   today as if those characters were literal, which is silently incorrect for
    ///   the extglob semantics. Tracked as task F1.3 in
    ///   <c>docs/globbing-feature-plan.md</c>.
    ///  </para>
    /// </remarks>
    [Obsolete("GlobOptions.AllowExtGlob is reserved but not yet implemented; setting it has no effect on the compiled matcher.", error: false)]
    AllowExtGlob = 1 << 4
}
