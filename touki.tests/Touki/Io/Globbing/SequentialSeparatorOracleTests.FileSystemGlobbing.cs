// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Extensions.FileSystemGlobbing;

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.FileSystemGlobbing"/>
///  dialect handles multiple sequential <c>/</c> characters inside the pattern, by
///  comparing each verdict against <see cref="Matcher"/> from
///  <c>Microsoft.Extensions.FileSystemGlobbing</c>.
/// </summary>
/// <remarks>
///  <para>
///   <c>Microsoft.Extensions.FileSystemGlobbing.Matcher</c> tokenizes its include /
///   exclude patterns by splitting on <c>/</c> and discards empty segments, which
///   effectively coalesces any run of separators down to one. The compiled
///   <see cref="GlobSpecification"/> for <see cref="GlobDialect.FileSystemGlobbing"/> performs
///   the same compile-time collapse so the two implementations agree on every input.
///  </para>
///  <para>
///   If a row here fails, either touki's FileSystemGlobbing dialect drifted or
///   <c>Microsoft.Extensions.FileSystemGlobbing</c> changed its tokenizer - both
///   warrant a deliberate update to the dialect contract in <c>docs/globbing.md</c>.
///  </para>
/// </remarks>
public class SequentialSeparatorFileSystemGlobbingOracleTests
{
    private static bool OracleMatches(string pattern, string input)
    {
        Matcher matcher = new(StringComparison.Ordinal);
        matcher.AddInclude(pattern);
        PatternMatchingResult result = matcher.Match(input);
        return result.HasMatches;
    }

    private static bool ToukiMatches(string pattern, string input) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing).IsMatch(input);

    [Theory]
    // --- Doubled separator between literal segments ---
    [InlineData("a//b", "a/b")]
    [InlineData("a//b", "a//b")]
    [InlineData("a//b", "ab")]
    [InlineData("a//b", "a/x/b")]
    // --- Tripled / quadrupled separator runs ---
    [InlineData("a///b", "a/b")]
    [InlineData("a///b", "a//b")]
    [InlineData("a////b", "a/b")]
    // --- Leading separator runs (rooted by Matcher) ---
    [InlineData("//a", "a")]
    [InlineData("//a", "/a")]
    // --- Trailing separator runs ---
    [InlineData("a//", "a")]
    [InlineData("a//", "a/")]
    // --- Doubled separator surrounding a wildcard ---
    [InlineData("a//*", "a/b")]
    [InlineData("*//b", "a/b")]
    [InlineData("*//b", "x/b")]
    // --- Doubled separator adjacent to globstar ---
    [InlineData("**//*.cs", "Foo.cs")]
    [InlineData("**//*.cs", "src/Foo.cs")]
    [InlineData("**//*.cs", "src/sub/Foo.cs")]
    [InlineData("a//**//b", "a/b")]
    [InlineData("a//**//b", "a/x/b")]
    [InlineData("a//**//b", "a/x/y/b")]
    public void IsMatch_FileSystemGlobbingDialect_SequentialSeparators_AgreesWithMatcher(string pattern, string input)
    {
        bool oracle = OracleMatches(pattern, input);
        bool actual = ToukiMatches(pattern, input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(FileSystemGlobbing) and Matcher must agree on pattern '{pattern}' vs input '{input}'");
    }
}
