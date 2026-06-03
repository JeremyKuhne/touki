// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.PosixPath"/> dialect
///  handles runs of three or more consecutive <c>*</c> characters in the pattern, by
///  comparing each verdict against <c>fnmatch(3)</c> with <c>FNM_PATHNAME</c>.
///  Linux/macOS only.
/// </summary>
[TestClass]
public class MultipleAsteriskPosixPathOracleTests
{
    public static IEnumerable<(string, string)> Rows() => MultipleAsteriskRows.Rows();

    [TestMethod]
    [DynamicData(nameof(Rows))]
    public void IsMatch_PosixPathDialect_MultipleAsterisks_AgreesWithFnmatchPathname(string pattern, string input)
    {
        if (!FnmatchInterop.IsSupported)
        {
            Assert.Inconclusive("fnmatch(3) oracle requires Linux or macOS.");
            return;
        }

        bool oracle = FnmatchInterop.Matches(pattern, input, flags: FnmatchInterop.FnmPathname);
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.PosixPath).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(PosixPath) and fnmatch(3) with FNM_PATHNAME must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
