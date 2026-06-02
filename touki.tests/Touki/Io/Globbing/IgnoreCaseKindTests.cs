// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Tests for the internal <see cref="IgnoreCaseKind"/> enum and the
///  <see cref="GlobDialectExtensions.DefaultIgnoreCaseKind"/> mapping that selects the
///  case-fold rule per <see cref="GlobDialect"/>. The mappings are documented on
///  <see cref="GlobOptions.IgnoreCase"/>; this pins them down to prevent silent drift.
/// </summary>
public class IgnoreCaseKindTests
{
    [Test]
    [Arguments(GlobDialect.Posix)]
    [Arguments(GlobDialect.PosixPath)]
    [Arguments(GlobDialect.Bash)]
    [Arguments(GlobDialect.Git)]
    [Arguments(GlobDialect.FileSystemGlobbing)]
    [Arguments(GlobDialect.Simple)]
    [Arguments(GlobDialect.PowerShell)]
    public void DefaultIgnoreCaseKind_OptionsNone_ReturnsOff(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.None).Should().Be(IgnoreCaseKind.Off);

    [Test]
    // MSBuild matches case-insensitively regardless of the IgnoreCase option.
    [Arguments(GlobOptions.None)]
    [Arguments(GlobOptions.IgnoreCase)]
    [Arguments(GlobOptions.NoEscape)]
    public void DefaultIgnoreCaseKind_MSBuild_ReturnsUnicodeRegardless(GlobOptions options) =>
        GlobDialect.MSBuild.DefaultIgnoreCaseKind(options).Should().Be(IgnoreCaseKind.Unicode);

    [Test]
    // POSIX-family dialects default to strict ASCII case folding.
    [Arguments(GlobDialect.Posix)]
    [Arguments(GlobDialect.PosixPath)]
    [Arguments(GlobDialect.Bash)]
    [Arguments(GlobDialect.Git)]
    public void DefaultIgnoreCaseKind_PosixFamily_ReturnsAscii(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.IgnoreCase).Should().Be(IgnoreCaseKind.Ascii);

    [Test]
    // .NET-native dialects default to full Unicode ordinal case folding.
    [Arguments(GlobDialect.MSBuild)]
    [Arguments(GlobDialect.FileSystemGlobbing)]
    [Arguments(GlobDialect.Simple)]
    [Arguments(GlobDialect.PowerShell)]
    public void DefaultIgnoreCaseKind_DotNetNative_ReturnsUnicode(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.IgnoreCase).Should().Be(IgnoreCaseKind.Unicode);

    [Test]
    // Other flag bits should not affect the kind decision. expectedAsInt is cast to
    // IgnoreCaseKind inside the method body so the internal enum doesn't leak into the
    // (public-required) test signature.
    [Arguments(GlobDialect.Posix, GlobOptions.IgnoreCase | GlobOptions.MatchLeadingDot, (int)IgnoreCaseKind.Ascii)]
    [Arguments(GlobDialect.Posix, GlobOptions.IgnoreCase | GlobOptions.NoEscape, (int)IgnoreCaseKind.Ascii)]
    [Arguments(GlobDialect.Simple, GlobOptions.IgnoreCase | GlobOptions.AllowGlobStar, (int)IgnoreCaseKind.Unicode)]
    [Arguments(GlobDialect.Posix, GlobOptions.MatchLeadingDot, (int)IgnoreCaseKind.Off)]
    public void DefaultIgnoreCaseKind_UnrelatedFlags_DoNotInfluence(
        GlobDialect dialect, GlobOptions options, int expectedAsInt) =>
        dialect.DefaultIgnoreCaseKind(options).Should().Be((IgnoreCaseKind)expectedAsInt);

    [Test]
    // The compiled matcher must expose the resolved kind so callers (and tests) can
    // verify the dialect's default flowed through to the runtime path.
    [Arguments(GlobDialect.Posix, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [Arguments(GlobDialect.Posix, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Ascii)]
    [Arguments(GlobDialect.Simple, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [Arguments(GlobDialect.Simple, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Unicode)]
    [Arguments(GlobDialect.PowerShell, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [Arguments(GlobDialect.PowerShell, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Unicode)]
    public void GlobSpecification_IgnoreCaseKind_FlowsFromDialectAndOptions(
        GlobDialect dialect, GlobOptions options, int expectedAsInt) =>
        ((IgnoreCaseKind)GlobSpecification.Compile("abc", dialect, options).TestAccessor.Dynamic.IgnoreCaseKind).Should().Be((IgnoreCaseKind)expectedAsInt);

    // -- GetEscapeChar --------------------------------------------------------------

    [Test]
    [Arguments(GlobDialect.Posix, GlobOptions.None, '\\')]
    [Arguments(GlobDialect.Posix, GlobOptions.NoEscape, '\0')]
    [Arguments(GlobDialect.PosixPath, GlobOptions.None, '\\')]
    [Arguments(GlobDialect.Bash, GlobOptions.None, '\\')]
    [Arguments(GlobDialect.Git, GlobOptions.None, '\\')]
    [Arguments(GlobDialect.MSBuild, GlobOptions.None, '\0')]
    [Arguments(GlobDialect.FileSystemGlobbing, GlobOptions.None, '\0')]
    [Arguments(GlobDialect.PowerShell, GlobOptions.None, '`')]
    [Arguments(GlobDialect.PowerShell, GlobOptions.NoEscape, '\0')]
    // Simple has no escape character regardless of options.
    [Arguments(GlobDialect.Simple, GlobOptions.None, '\0')]
    [Arguments(GlobDialect.Simple, GlobOptions.NoEscape, '\0')]
    public void GetEscapeChar_PerDialect(GlobDialect dialect, GlobOptions options, char expected) =>
        dialect.GetEscapeChar(options).Should().Be(expected);

    // -- MatchesLeadingDotByDefault ------------------------------------------------

    [Test]
    // POSIX family enforces FNM_PERIOD: a leading '.' must be matched literally.
    [Arguments(GlobDialect.Posix, false)]
    [Arguments(GlobDialect.PosixPath, false)]
    [Arguments(GlobDialect.Bash, false)]
    [Arguments(GlobDialect.Git, false)]
    // Other dialects don't restrict leading dots.
    [Arguments(GlobDialect.MSBuild, true)]
    [Arguments(GlobDialect.FileSystemGlobbing, true)]
    [Arguments(GlobDialect.Simple, true)]
    [Arguments(GlobDialect.PowerShell, true)]
    public void MatchesLeadingDotByDefault_PerDialect(GlobDialect dialect, bool expected) =>
        dialect.MatchesLeadingDotByDefault().Should().Be(expected);

    // -- IsPathAware ---------------------------------------------------------------

    [Test]
    // Path-aware dialects: their wildcards do not cross the path separator.
    [Arguments(GlobDialect.PosixPath, true)]
    [Arguments(GlobDialect.Bash, true)]
    [Arguments(GlobDialect.Git, true)]
    [Arguments(GlobDialect.MSBuild, true)]
    [Arguments(GlobDialect.FileSystemGlobbing, true)]
    // Path-unaware dialects: separator is just another character.
    [Arguments(GlobDialect.Posix, false)]
    [Arguments(GlobDialect.Simple, false)]
    [Arguments(GlobDialect.PowerShell, false)]
    public void IsPathAware_PerDialect(GlobDialect dialect, bool expected) =>
        dialect.IsPathAware().Should().Be(expected);

    // -- DefaultSeparator ----------------------------------------------------------

    [Test]
    // POSIX-family, FileSystemGlobbing, and MSBuild default to '/'.
    [Arguments(GlobDialect.PosixPath, '/')]
    [Arguments(GlobDialect.Bash, '/')]
    [Arguments(GlobDialect.Git, '/')]
    [Arguments(GlobDialect.FileSystemGlobbing, '/')]
    [Arguments(GlobDialect.MSBuild, '/')]
    // Path-unaware dialects have no separator.
    [Arguments(GlobDialect.Posix, '\0')]
    [Arguments(GlobDialect.Simple, '\0')]
    [Arguments(GlobDialect.PowerShell, '\0')]
    public void DefaultSeparator_PerDialect(GlobDialect dialect, char expected) =>
        dialect.DefaultSeparator().Should().Be(expected);

    [Test]
    public void DefaultSeparator_MSBuild_IsForwardSlash() =>
        GlobDialect.MSBuild.DefaultSeparator().Should().Be('/');
}
