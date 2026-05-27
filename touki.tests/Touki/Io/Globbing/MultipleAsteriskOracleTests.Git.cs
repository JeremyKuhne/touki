// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Git"/> dialect handles
///  runs of three or more consecutive <c>*</c> characters in the pattern, by comparing
///  each verdict against <c>LibGit2Sharp</c>'s gitignore evaluator. Shares the
///  one-shot scratch repository fixture with the sequential-separator suite.
/// </summary>
public sealed class MultipleAsteriskGitOracleTests : IClassFixture<SequentialSeparatorGitOracleTests.RepoFixture>
{
    private readonly SequentialSeparatorGitOracleTests.RepoFixture _fixture;

    public MultipleAsteriskGitOracleTests(SequentialSeparatorGitOracleTests.RepoFixture fixture) => _fixture = fixture;

    public static TheoryData<string, string> Rows => MultipleAsteriskRows.Rows;

    [Theory]
    [MemberData(nameof(Rows))]
    public void IsMatch_GitDialect_MultipleAsterisks_AgreesWithLibGit2(string pattern, string input)
    {
        // Documented Git dialect divergence. After `***`+ &rarr; `**` normalization
        // touki compiles `a/**` as a trailing globstar that matches zero or more
        // segments &mdash; including the empty case `a/` &mdash; while gitignore
        // requires at least one path component after the prefix. Tracked in
        // docs/globbing-feature-plan.md "Multiple-asterisk-run behavior" findings.
        if ((pattern, input) == ("a/***", "a/"))
        {
            Assert.Skip("Documented Git trailing-globstar must-consume-one-segment divergence.");
            return;
        }

        bool oracle = _fixture.IsIgnored(pattern, input);
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.Git).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Git) and LibGit2Sharp gitignore must agree on pattern '{pattern}' vs input '{input}'");
    }
}
