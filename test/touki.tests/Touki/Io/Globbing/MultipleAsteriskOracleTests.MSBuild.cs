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
[TestClass]
public class MultipleAsteriskMSBuildOracleTests
{
    public static IEnumerable<(string, string)> Rows() => MultipleAsteriskRows.Rows();

    [TestMethod]
    [DynamicData(nameof(Rows))]
    public void IsMatch_MSBuildDialect_MultipleAsterisks_AgreesWithMSBuildGlob(string pattern, string input)
    {
        bool oracle = MSBuildGlob.Parse(Directory.GetCurrentDirectory(), pattern).IsMatch(input);
        GlobSpecification specification = GlobSpecification.Compile(pattern, GlobDialect.MSBuild);
        bool actual = specification.IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(MSBuild) and MSBuildGlob must agree on pattern '{pattern}' vs input '{input}'");
    }

    [TestMethod]
    [DataRow("a**b", "ab")]
    [DataRow("a**b", "axb")]
    [DataRow("**foo", "foo")]
    [DataRow("foo**", "foo")]
    [DataRow("foo/a**b/baz", "foo/axb/baz")]
    [DataRow("*.cs**", "source.cs")]
    public void IsMatch_MSBuildDialect_MisplacedDoubleStar_FollowsFileMatcherIsMatch(
        string pattern,
        string input)
    {
        bool globOracle = MSBuildGlob.Parse(Directory.GetCurrentDirectory(), pattern).IsMatch(input);
        bool fileMatcherOracle = FileMatcherWrapper.IsMatch(input, pattern);
        GlobSpecification specification = GlobSpecification.Compile(pattern, GlobDialect.MSBuild);
        bool actual = specification.IsMatch(input);

        globOracle.Should().BeFalse(
            because: $"MSBuildGlob validates misplaced '**' in file specs such as '{pattern}'");
        fileMatcherOracle.Should().BeTrue(
            because: $"FileMatcher.IsMatch collapses repeated stars in pattern '{pattern}'");
        actual.Should().Be(fileMatcherOracle);
    }

    [TestMethod]
    [DataRow("**/a/b/*.cs", "a/a/b/source.cs")]
    [DataRow("**/a/a/*.cs", "a/a/a/source.cs")]
    [DataRow("**/a/**/a/*.cs", "a/a/source.cs")]
    [DataRow("**/a/**/a/*.cs", "x/a/x/y/a/source.cs")]
    [DataRow("*.*", "README")]
    [DataRow("*.", "README")]
    [DataRow("LICENSE.*", "LICENSE")]
    [DataRow("?.", "A")]
    [DataRow("?.", "AB")]
    [DataRow("??.", "AB")]
    [DataRow("??.", "ABCD")]
    [DataRow("*..", "README")]
    [DataRow("*..", "README.")]
    [DataRow("LICENSE..", "LICENSE.")]
    [DataRow("*..", "a.")]
    [DataRow("*..", "a..")]
    [DataRow("LICENSE..", "LICENSE..")]
    [DataRow(".", "")]
    [DataRow(".", ".")]
    [DataRow("a*.*b", "ab")]
    [DataRow("a*.*b", "aX.Yb")]
    [DataRow("*.*.*", "README")]
    [DataRow("*.*.*", "README.txt")]
    public void IsMatch_MSBuildDialect_CompatibilityShapes_AgreeWithMSBuildGlob(
        string pattern,
        string input)
    {
        bool oracle = MSBuildGlob.Parse(Directory.GetCurrentDirectory(), pattern).IsMatch(input);
        GlobSpecification specification = GlobSpecification.Compile(pattern, GlobDialect.MSBuild);
        bool actual = specification.IsMatch(input);

        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(MSBuild) and MSBuildGlob must agree on pattern '{pattern}' vs input '{input}'");
    }

    [TestMethod]
    public void IsMatch_MSBuildDialect_TrailingDotPatterns_HaveNoBoundedOracleMismatch()
    {
        string[] candidates = [.. GenerateStrings("ab.", minimumLength: 0, maximumLength: 6)];

        foreach (string patternBody in GenerateStrings("ab*?", minimumLength: 1, maximumLength: 3))
        {
            if (patternBody.Contains("**", StringComparison.Ordinal))
            {
                continue;
            }

            string pattern = patternBody + ".";
            MSBuildGlob oracle = MSBuildGlob.Parse(Directory.GetCurrentDirectory(), pattern);
            GlobSpecification actual = GlobSpecification.Compile(pattern, GlobDialect.MSBuild);

            foreach (string candidate in candidates)
            {
                actual.IsMatch(candidate).Should().Be(
                    oracle.IsMatch(candidate),
                    because: $"MSBuild trailing-dot pattern '{pattern}' must agree for '{candidate}'");
            }
        }
    }

    private static IEnumerable<string> GenerateStrings(
        string alphabet,
        int minimumLength,
        int maximumLength)
    {
        for (int length = minimumLength; length <= maximumLength; length++)
        {
            int valueCount = 1;
            for (int index = 0; index < length; index++)
            {
                valueCount *= alphabet.Length;
            }

            for (int value = 0; value < valueCount; value++)
            {
                char[] characters = new char[length];
                int remaining = value;
                for (int index = 0; index < length; index++)
                {
                    characters[index] = alphabet[remaining % alphabet.Length];
                    remaining /= alphabet.Length;
                }

                yield return new string(characters);
            }
        }
    }

}
