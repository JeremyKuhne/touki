// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.PosixPath"/> dialect handles
///  multiple sequential <c>/</c> characters inside the pattern, by comparing each verdict
///  against <c>fnmatch(3)</c> with <c>FNM_PATHNAME</c> set so wildcards do not cross
///  separators.
/// </summary>
/// <remarks>
///  <para>
///   Linux/macOS only. With <c>FNM_PATHNAME</c>, runs of <c>/</c> in the pattern are
///   <b>not</b> coalesced by the libc implementation - each <c>/</c> is a literal
///   separator that must appear in the input at the corresponding position. This contrasts
///   with the MSBuild dialect (which coalesces).
///  </para>
/// </remarks>
[TestClass]
public class SequentialSeparatorPosixPathOracleTests
{
    private static bool OracleMatches(string pattern, string input) =>
        FnmatchInterop.Matches(pattern, input, flags: FnmatchInterop.FnmPathname);

    private static bool ToukiMatches(string pattern, string input) =>
        GlobSpecification.Compile(pattern, GlobDialect.PosixPath).IsMatch(input);

    [TestMethod]
    [DataRow("a//b", "a/b")]
    [DataRow("a//b", "a//b")]
    [DataRow("a//b", "a///b")]
    [DataRow("a//b", "ab")]
    [DataRow("a//b", "a/x/b")]
    [DataRow("a///b", "a/b")]
    [DataRow("a///b", "a//b")]
    [DataRow("a///b", "a///b")]
    [DataRow("a////b", "a///b")]
    [DataRow("//a", "/a")]
    [DataRow("//a", "//a")]
    [DataRow("//a", "a")]
    [DataRow("a//", "a/")]
    [DataRow("a//", "a//")]
    [DataRow("a//", "a")]
    [DataRow("a//*", "a/b")]
    [DataRow("a//*", "a//b")]
    [DataRow("*//b", "a/b")]
    [DataRow("*//b", "a//b")]
    [DataRow("*//b", "//b")]
    public void IsMatch_PosixPathDialect_SequentialSeparators_AgreesWithFnmatchPathname(string pattern, string input)
    {
        if (!FnmatchInterop.IsSupported)
        {
            Assert.Inconclusive("fnmatch(3) oracle requires Linux or macOS.");
            return;
        }

        bool oracle = OracleMatches(pattern, input);
        bool actual = ToukiMatches(pattern, input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(PosixPath) and fnmatch(3) with FNM_PATHNAME must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
