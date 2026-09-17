// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Extensions.FileSystemGlobbing;

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.FileSystemGlobbing"/> dialect
///  handles runs of three or more consecutive <c>*</c> characters in the pattern, by
///  comparing each verdict against <see cref="Matcher"/>.
/// </summary>
[TestClass]
public class MultipleAsteriskFileSystemGlobbingOracleTests
{
    public static IEnumerable<(string, string)> Rows() => MultipleAsteriskRows.Rows();

    [TestMethod]
    [DynamicData(nameof(Rows))]
    public void IsMatch_FileSystemGlobbingDialect_MultipleAsterisks_AgreesWithMatcher(string pattern, string input)
    {
        Matcher matcher = new(StringComparison.Ordinal);
        matcher.AddInclude(pattern);
        bool oracle = matcher.Match(input).HasMatches;
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(FileSystemGlobbing) and Matcher must agree on pattern '{pattern}' vs input '{input}'");
    }
}
