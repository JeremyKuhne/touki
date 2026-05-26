// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Build.Globbing;

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.MSBuild"/> dialect handles
///  runs of three or more consecutive <c>*</c> characters in the pattern (<c>***</c>,
///  <c>****</c>, ...), by comparing each verdict against
///  <see cref="MSBuildGlob"/>.
/// </summary>
public class MultipleAsteriskMSBuildOracleTests
{
    public static TheoryData<string, string> Rows => MultipleAsteriskRows.Rows;

    [Theory]
    [MemberData(nameof(Rows))]
    public void IsMatch_MSBuildDialect_MultipleAsterisks_AgreesWithMSBuildGlob(string pattern, string input)
    {
        bool oracle = MSBuildGlob.Parse(Directory.GetCurrentDirectory(), pattern).IsMatch(input);
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(MSBuild) and MSBuildGlob must agree on pattern '{pattern}' vs input '{input}'");
    }
}
