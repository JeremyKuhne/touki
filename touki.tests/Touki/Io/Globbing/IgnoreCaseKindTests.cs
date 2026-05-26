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
    [Theory]
    [InlineData(GlobDialect.Posix)]
    [InlineData(GlobDialect.PosixPath)]
    [InlineData(GlobDialect.Bash)]
    [InlineData(GlobDialect.Git)]
    [InlineData(GlobDialect.FileSystemGlobbing)]
    [InlineData(GlobDialect.Win32)]
    [InlineData(GlobDialect.Simple)]
    [InlineData(GlobDialect.PowerShell)]
    public void DefaultIgnoreCaseKind_OptionsNone_ReturnsOff(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.None).Should().Be(IgnoreCaseKind.Off);

    [Theory]
    // MSBuild matches case-insensitively regardless of the IgnoreCase option.
    [InlineData(GlobOptions.None)]
    [InlineData(GlobOptions.IgnoreCase)]
    [InlineData(GlobOptions.NoEscape)]
    public void DefaultIgnoreCaseKind_MSBuild_ReturnsUnicodeRegardless(GlobOptions options) =>
        GlobDialect.MSBuild.DefaultIgnoreCaseKind(options).Should().Be(IgnoreCaseKind.Unicode);

    [Theory]
    // POSIX-family dialects default to strict ASCII case folding.
    [InlineData(GlobDialect.Posix)]
    [InlineData(GlobDialect.PosixPath)]
    [InlineData(GlobDialect.Bash)]
    [InlineData(GlobDialect.Git)]
    public void DefaultIgnoreCaseKind_PosixFamily_ReturnsAscii(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.IgnoreCase).Should().Be(IgnoreCaseKind.Ascii);

    [Theory]
    // .NET / Windows-native dialects default to full Unicode ordinal case folding.
    [InlineData(GlobDialect.MSBuild)]
    [InlineData(GlobDialect.FileSystemGlobbing)]
    [InlineData(GlobDialect.Win32)]
    [InlineData(GlobDialect.Simple)]
    [InlineData(GlobDialect.PowerShell)]
    public void DefaultIgnoreCaseKind_DotNetNative_ReturnsUnicode(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.IgnoreCase).Should().Be(IgnoreCaseKind.Unicode);

    [Theory]
    // Other flag bits should not affect the kind decision. expectedAsInt is cast to
    // IgnoreCaseKind inside the method body so the internal enum doesn't leak into the
    // (public-required) test signature.
    [InlineData(GlobDialect.Posix, GlobOptions.IgnoreCase | GlobOptions.MatchLeadingDot, (int)IgnoreCaseKind.Ascii)]
    [InlineData(GlobDialect.Posix, GlobOptions.IgnoreCase | GlobOptions.NoEscape, (int)IgnoreCaseKind.Ascii)]
    [InlineData(GlobDialect.Simple, GlobOptions.IgnoreCase | GlobOptions.AllowGlobStar, (int)IgnoreCaseKind.Unicode)]
    [InlineData(GlobDialect.Posix, GlobOptions.MatchLeadingDot, (int)IgnoreCaseKind.Off)]
    public void DefaultIgnoreCaseKind_UnrelatedFlags_DoNotInfluence(
        GlobDialect dialect, GlobOptions options, int expectedAsInt) =>
        dialect.DefaultIgnoreCaseKind(options).Should().Be((IgnoreCaseKind)expectedAsInt);

    [Theory]
    // The compiled matcher must expose the resolved kind so callers (and tests) can
    // verify the dialect's default flowed through to the runtime path.
    [InlineData(GlobDialect.Posix, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [InlineData(GlobDialect.Posix, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Ascii)]
    [InlineData(GlobDialect.Simple, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [InlineData(GlobDialect.Simple, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Unicode)]
    [InlineData(GlobDialect.PowerShell, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [InlineData(GlobDialect.PowerShell, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Unicode)]
    public void GlobSpecification_IgnoreCaseKind_FlowsFromDialectAndOptions(
        GlobDialect dialect, GlobOptions options, int expectedAsInt) =>
        ((IgnoreCaseKind)GlobSpecification.Compile("abc", dialect, options).TestAccessor.Dynamic.IgnoreCaseKind).Should().Be((IgnoreCaseKind)expectedAsInt);

    // -- GetEscapeChar --------------------------------------------------------------

    [Theory]
    [InlineData(GlobDialect.Posix, GlobOptions.None, '\\')]
    [InlineData(GlobDialect.Posix, GlobOptions.NoEscape, '\0')]
    [InlineData(GlobDialect.PosixPath, GlobOptions.None, '\\')]
    [InlineData(GlobDialect.Bash, GlobOptions.None, '\\')]
    [InlineData(GlobDialect.Git, GlobOptions.None, '\\')]
    [InlineData(GlobDialect.MSBuild, GlobOptions.None, '\0')]
    [InlineData(GlobDialect.FileSystemGlobbing, GlobOptions.None, '\0')]
    [InlineData(GlobDialect.Win32, GlobOptions.None, '\\')]
    [InlineData(GlobDialect.PowerShell, GlobOptions.None, '`')]
    [InlineData(GlobDialect.PowerShell, GlobOptions.NoEscape, '\0')]
    // Simple has no escape character regardless of options.
    [InlineData(GlobDialect.Simple, GlobOptions.None, '\0')]
    [InlineData(GlobDialect.Simple, GlobOptions.NoEscape, '\0')]
    public void GetEscapeChar_PerDialect(GlobDialect dialect, GlobOptions options, char expected) =>
        dialect.GetEscapeChar(options).Should().Be(expected);

    // -- MatchesLeadingDotByDefault ------------------------------------------------

    [Theory]
    // POSIX family enforces FNM_PERIOD: a leading '.' must be matched literally.
    [InlineData(GlobDialect.Posix, false)]
    [InlineData(GlobDialect.PosixPath, false)]
    [InlineData(GlobDialect.Bash, false)]
    [InlineData(GlobDialect.Git, false)]
    // Other dialects don't restrict leading dots.
    [InlineData(GlobDialect.MSBuild, true)]
    [InlineData(GlobDialect.FileSystemGlobbing, true)]
    [InlineData(GlobDialect.Win32, true)]
    [InlineData(GlobDialect.Simple, true)]
    [InlineData(GlobDialect.PowerShell, true)]
    public void MatchesLeadingDotByDefault_PerDialect(GlobDialect dialect, bool expected) =>
        dialect.MatchesLeadingDotByDefault().Should().Be(expected);

    // -- IsPathAware ---------------------------------------------------------------

    [Theory]
    // Path-aware dialects: their wildcards do not cross the path separator.
    [InlineData(GlobDialect.PosixPath, true)]
    [InlineData(GlobDialect.Bash, true)]
    [InlineData(GlobDialect.Git, true)]
    [InlineData(GlobDialect.MSBuild, true)]
    [InlineData(GlobDialect.FileSystemGlobbing, true)]
    // Path-unaware dialects: separator is just another character.
    [InlineData(GlobDialect.Posix, false)]
    [InlineData(GlobDialect.Simple, false)]
    [InlineData(GlobDialect.PowerShell, false)]
    [InlineData(GlobDialect.Win32, false)]
    public void IsPathAware_PerDialect(GlobDialect dialect, bool expected) =>
        dialect.IsPathAware().Should().Be(expected);

    // -- DefaultSeparator ----------------------------------------------------------

    [Theory]
    // POSIX-family, FileSystemGlobbing, and MSBuild default to '/'.
    [InlineData(GlobDialect.PosixPath, '/')]
    [InlineData(GlobDialect.Bash, '/')]
    [InlineData(GlobDialect.Git, '/')]
    [InlineData(GlobDialect.FileSystemGlobbing, '/')]
    [InlineData(GlobDialect.MSBuild, '/')]
    // Path-unaware dialects have no separator.
    [InlineData(GlobDialect.Posix, '\0')]
    [InlineData(GlobDialect.Simple, '\0')]
    [InlineData(GlobDialect.PowerShell, '\0')]
    [InlineData(GlobDialect.Win32, '\0')]
    public void DefaultSeparator_PerDialect(GlobDialect dialect, char expected) =>
        dialect.DefaultSeparator().Should().Be(expected);

    [Fact]
    public void DefaultSeparator_MSBuild_IsForwardSlash() =>
        GlobDialect.MSBuild.DefaultSeparator().Should().Be('/');
}
