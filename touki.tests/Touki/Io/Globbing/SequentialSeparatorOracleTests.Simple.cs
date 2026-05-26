// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Simple"/> dialect handles
///  multiple sequential <c>/</c> characters inside the pattern, by comparing each verdict
///  against <see cref="FileSystemName.MatchesSimpleExpression(System.ReadOnlySpan{char}, System.ReadOnlySpan{char}, bool)"/>.
/// </summary>
/// <remarks>
///  <para>
///   <see cref="GlobDialect.Simple"/> is path-unaware: it has no separator concept, so
///   <c>/</c> is a plain literal and runs of <c>/</c> in the pattern are <b>not</b>
///   coalesced &#8212; the input must contain the same number of <c>/</c> characters in
///   the same position. The compiled <see cref="GlobSpecification"/> must agree with the BCL
///   reference on every row.
///  </para>
///  <para>
///   Only available on .NET 10+; <see cref="FileSystemName"/> does not exist on
///   .NET Framework. Run on net10 only.
///  </para>
/// </remarks>
public class SequentialSeparatorSimpleOracleTests
{
    private static bool OracleMatches(string pattern, string input) =>
        FileSystemName.MatchesSimpleExpression(pattern.AsSpan(), input.AsSpan(), ignoreCase: false);

    private static bool ToukiMatches(string pattern, string input) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple).IsMatch(input);

    [Theory]
    // --- Doubled separator: must NOT be coalesced (Simple is path-unaware) ---
    [InlineData("a//b", "a/b")]
    [InlineData("a//b", "a//b")]
    [InlineData("a//b", "a///b")]
    [InlineData("a//b", "ab")]
    // --- Tripled / quadrupled separator runs ---
    [InlineData("a///b", "a/b")]
    [InlineData("a///b", "a//b")]
    [InlineData("a///b", "a///b")]
    [InlineData("a////b", "a///b")]
    [InlineData("a////b", "a////b")]
    // --- Leading separator runs ---
    [InlineData("//a", "/a")]
    [InlineData("//a", "//a")]
    [InlineData("//a", "a")]
    // --- Trailing separator runs ---
    [InlineData("a//", "a/")]
    [InlineData("a//", "a//")]
    [InlineData("a//", "a")]
    // --- Doubled separator surrounding a wildcard ---
    [InlineData("a//*", "a/b")]
    [InlineData("a//*", "a//b")]
    [InlineData("*//b", "a/b")]
    [InlineData("*//b", "a//b")]
    [InlineData("*//b", "//b")]
    public void IsMatch_SimpleDialect_SequentialSeparators_AgreesWithBcl(string pattern, string input)
    {
        bool oracle = OracleMatches(pattern, input);
        bool actual = ToukiMatches(pattern, input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Simple) and FileSystemName.MatchesSimpleExpression must agree on pattern '{pattern}' vs input '{input}'");
    }
}
