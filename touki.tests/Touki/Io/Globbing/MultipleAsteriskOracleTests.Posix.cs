// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Posix"/> dialect handles
///  runs of three or more consecutive <c>*</c> characters in the pattern, by comparing
///  each verdict against <c>fnmatch(3)</c> with no flags. Linux/macOS only.
/// </summary>
public class MultipleAsteriskPosixOracleTests
{
    public static TheoryData<string, string> Rows => MultipleAsteriskRows.Rows;

    [Theory]
    [MemberData(nameof(Rows))]
    public void IsMatch_PosixDialect_MultipleAsterisks_AgreesWithFnmatch(string pattern, string input)
    {
        if (!FnmatchInterop.IsSupported)
        {
            Assert.Skip("fnmatch(3) oracle requires Linux or macOS.");
            return;
        }

        bool oracle = FnmatchInterop.Matches(pattern, input, flags: 0);
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Posix) and fnmatch(3) (no flags) must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
