// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Posix"/> dialect handles
///  multiple sequential <c>/</c> characters inside the pattern, by comparing each verdict
///  against <c>fnmatch(3)</c> with no flags (no <c>FNM_PATHNAME</c>, so <c>/</c> is a
///  plain literal character).
/// </summary>
/// <remarks>
///  <para>
///   Linux/macOS only - the oracle calls <c>libc</c>'s <c>fnmatch</c> via P/Invoke.
///   Windows runs skip via <see cref="Assert.Inconclusive(string)"/>.
///  </para>
/// </remarks>
[TestClass]
public class SequentialSeparatorPosixOracleTests
{
    private static bool OracleMatches(string pattern, string input) =>
        FnmatchInterop.Matches(pattern, input, flags: 0);

    private static bool ToukiMatches(string pattern, string input) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input);

    [TestMethod]
    // --- Doubled separator between literal segments (Posix: / is just a literal) ---
    [DataRow("a//b", "a/b")]
    [DataRow("a//b", "a//b")]
    [DataRow("a//b", "a///b")]
    [DataRow("a//b", "ab")]
    [DataRow("a//b", "a/x/b")]
    // --- Tripled / quadrupled separator runs ---
    [DataRow("a///b", "a/b")]
    [DataRow("a///b", "a//b")]
    [DataRow("a///b", "a///b")]
    [DataRow("a////b", "a///b")]
    [DataRow("a////b", "a////b")]
    // --- Leading separator runs ---
    [DataRow("//a", "/a")]
    [DataRow("//a", "//a")]
    [DataRow("//a", "a")]
    // --- Trailing separator runs ---
    [DataRow("a//", "a/")]
    [DataRow("a//", "a//")]
    [DataRow("a//", "a")]
    // --- Doubled separator surrounding a wildcard ---
    [DataRow("a//*", "a/b")]
    [DataRow("a//*", "a//b")]
    [DataRow("*//b", "a/b")]
    [DataRow("*//b", "a//b")]
    [DataRow("*//b", "//b")]
    public void IsMatch_PosixDialect_SequentialSeparators_AgreesWithFnmatch(string pattern, string input)
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
            because: $"GlobSpecification(Posix) and fnmatch(3) (no flags) must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
