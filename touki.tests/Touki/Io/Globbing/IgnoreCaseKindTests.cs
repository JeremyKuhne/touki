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
[TestClass]
public class IgnoreCaseKindTests
{
    [TestMethod]
    [DataRow(GlobDialect.Posix)]
    [DataRow(GlobDialect.PosixPath)]
    [DataRow(GlobDialect.Bash)]
    [DataRow(GlobDialect.Git)]
    [DataRow(GlobDialect.FileSystemGlobbing)]
    [DataRow(GlobDialect.Simple)]
    [DataRow(GlobDialect.PowerShell)]
    public void DefaultIgnoreCaseKind_OptionsNone_ReturnsOff(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.None).Should().Be(IgnoreCaseKind.Off);

    [TestMethod]
    // MSBuild matches case-insensitively regardless of the IgnoreCase option.
    [DataRow(GlobOptions.None)]
    [DataRow(GlobOptions.IgnoreCase)]
    [DataRow(GlobOptions.NoEscape)]
    public void DefaultIgnoreCaseKind_MSBuild_ReturnsUnicodeRegardless(GlobOptions options) =>
        GlobDialect.MSBuild.DefaultIgnoreCaseKind(options).Should().Be(IgnoreCaseKind.Unicode);

    [TestMethod]
    // POSIX-family dialects default to strict ASCII case folding.
    [DataRow(GlobDialect.Posix)]
    [DataRow(GlobDialect.PosixPath)]
    [DataRow(GlobDialect.Bash)]
    [DataRow(GlobDialect.Git)]
    public void DefaultIgnoreCaseKind_PosixFamily_ReturnsAscii(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.IgnoreCase).Should().Be(IgnoreCaseKind.Ascii);

    [TestMethod]
    // .NET-native dialects default to full Unicode ordinal case folding.
    [DataRow(GlobDialect.MSBuild)]
    [DataRow(GlobDialect.FileSystemGlobbing)]
    [DataRow(GlobDialect.Simple)]
    [DataRow(GlobDialect.PowerShell)]
    public void DefaultIgnoreCaseKind_DotNetNative_ReturnsUnicode(GlobDialect dialect) =>
        dialect.DefaultIgnoreCaseKind(GlobOptions.IgnoreCase).Should().Be(IgnoreCaseKind.Unicode);

    [TestMethod]
    // Other flag bits should not affect the kind decision. expectedAsInt is cast to
    // IgnoreCaseKind inside the method body so the internal enum doesn't leak into the
    // (public-required) test signature.
    [DataRow(GlobDialect.Posix, GlobOptions.IgnoreCase | GlobOptions.MatchLeadingDot, (int)IgnoreCaseKind.Ascii)]
    [DataRow(GlobDialect.Posix, GlobOptions.IgnoreCase | GlobOptions.NoEscape, (int)IgnoreCaseKind.Ascii)]
    [DataRow(GlobDialect.Simple, GlobOptions.IgnoreCase | GlobOptions.AllowGlobStar, (int)IgnoreCaseKind.Unicode)]
    [DataRow(GlobDialect.Posix, GlobOptions.MatchLeadingDot, (int)IgnoreCaseKind.Off)]
    public void DefaultIgnoreCaseKind_UnrelatedFlags_DoNotInfluence(
        GlobDialect dialect, GlobOptions options, int expectedAsInt) =>
        dialect.DefaultIgnoreCaseKind(options).Should().Be((IgnoreCaseKind)expectedAsInt);

    [TestMethod]
    // The compiled matcher must expose the resolved kind so callers (and tests) can
    // verify the dialect's default flowed through to the runtime path.
    [DataRow(GlobDialect.Posix, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [DataRow(GlobDialect.Posix, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Ascii)]
    [DataRow(GlobDialect.Simple, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [DataRow(GlobDialect.Simple, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Unicode)]
    [DataRow(GlobDialect.PowerShell, GlobOptions.None, (int)IgnoreCaseKind.Off)]
    [DataRow(GlobDialect.PowerShell, GlobOptions.IgnoreCase, (int)IgnoreCaseKind.Unicode)]
    public void GlobSpecification_IgnoreCaseKind_FlowsFromDialectAndOptions(
        GlobDialect dialect, GlobOptions options, int expectedAsInt) =>
        ((IgnoreCaseKind)GlobSpecification.Compile("abc", dialect, options).TestAccessor.Dynamic.IgnoreCaseKind).Should().Be((IgnoreCaseKind)expectedAsInt);

    // -- GetEscapeChar --------------------------------------------------------------

    [TestMethod]
    [DataRow(GlobDialect.Posix, GlobOptions.None, '\\')]
    [DataRow(GlobDialect.Posix, GlobOptions.NoEscape, '\0')]
    [DataRow(GlobDialect.PosixPath, GlobOptions.None, '\\')]
    [DataRow(GlobDialect.Bash, GlobOptions.None, '\\')]
    [DataRow(GlobDialect.Git, GlobOptions.None, '\\')]
    [DataRow(GlobDialect.MSBuild, GlobOptions.None, '\0')]
    [DataRow(GlobDialect.FileSystemGlobbing, GlobOptions.None, '\0')]
    [DataRow(GlobDialect.PowerShell, GlobOptions.None, '`')]
    [DataRow(GlobDialect.PowerShell, GlobOptions.NoEscape, '\0')]
    // Simple has no escape character regardless of options.
    [DataRow(GlobDialect.Simple, GlobOptions.None, '\0')]
    [DataRow(GlobDialect.Simple, GlobOptions.NoEscape, '\0')]
    public void GetEscapeChar_PerDialect(GlobDialect dialect, GlobOptions options, char expected) =>
        dialect.GetEscapeChar(options).Should().Be(expected);

    // -- MatchesLeadingDotByDefault ------------------------------------------------

    [TestMethod]
    // POSIX family enforces FNM_PERIOD: a leading '.' must be matched literally.
    [DataRow(GlobDialect.Posix, false)]
    [DataRow(GlobDialect.PosixPath, false)]
    [DataRow(GlobDialect.Bash, false)]
    [DataRow(GlobDialect.Git, false)]
    // Other dialects don't restrict leading dots.
    [DataRow(GlobDialect.MSBuild, true)]
    [DataRow(GlobDialect.FileSystemGlobbing, true)]
    [DataRow(GlobDialect.Simple, true)]
    [DataRow(GlobDialect.PowerShell, true)]
    public void MatchesLeadingDotByDefault_PerDialect(GlobDialect dialect, bool expected) =>
        dialect.MatchesLeadingDotByDefault().Should().Be(expected);

    // -- IsPathAware ---------------------------------------------------------------

    [TestMethod]
    // Path-aware dialects: their wildcards do not cross the path separator.
    [DataRow(GlobDialect.PosixPath, true)]
    [DataRow(GlobDialect.Bash, true)]
    [DataRow(GlobDialect.Git, true)]
    [DataRow(GlobDialect.MSBuild, true)]
    [DataRow(GlobDialect.FileSystemGlobbing, true)]
    // Path-unaware dialects: separator is just another character.
    [DataRow(GlobDialect.Posix, false)]
    [DataRow(GlobDialect.Simple, false)]
    [DataRow(GlobDialect.PowerShell, false)]
    public void IsPathAware_PerDialect(GlobDialect dialect, bool expected) =>
        dialect.IsPathAware().Should().Be(expected);

    // -- DefaultSeparator ----------------------------------------------------------

    [TestMethod]
    // POSIX-family, FileSystemGlobbing, and MSBuild default to '/'.
    [DataRow(GlobDialect.PosixPath, '/')]
    [DataRow(GlobDialect.Bash, '/')]
    [DataRow(GlobDialect.Git, '/')]
    [DataRow(GlobDialect.FileSystemGlobbing, '/')]
    [DataRow(GlobDialect.MSBuild, '/')]
    // Path-unaware dialects have no separator.
    [DataRow(GlobDialect.Posix, '\0')]
    [DataRow(GlobDialect.Simple, '\0')]
    [DataRow(GlobDialect.PowerShell, '\0')]
    public void DefaultSeparator_PerDialect(GlobDialect dialect, char expected) =>
        dialect.DefaultSeparator().Should().Be(expected);

    [TestMethod]
    public void DefaultSeparator_MSBuild_IsForwardSlash() =>
        GlobDialect.MSBuild.DefaultSeparator().Should().Be('/');
}
