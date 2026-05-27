// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Coverage tests for the POSIX bracket sub-forms and the rarely-used
///  <c>AppendPosixNamedClass</c> entries that don't appear in the dialect/oracle
///  suites, plus the MSBuild input-side separator-coalescing path.
/// </summary>
public class GlobSpecificationFactoryEdgeCoverageTests
{
    // POSIX named character classes. Each `[[:NAME:]]` form expands inline to its
    // ASCII range list; coverage walks the lookup table in AppendPosixNamedClass.
    [Theory]
    [InlineData("[[:alpha:]]", "A", true)]
    [InlineData("[[:alpha:]]", "1", false)]
    [InlineData("[[:digit:]]", "7", true)]
    [InlineData("[[:digit:]]", "a", false)]
    [InlineData("[[:upper:]]", "M", true)]
    [InlineData("[[:upper:]]", "m", false)]
    [InlineData("[[:lower:]]", "m", true)]
    [InlineData("[[:lower:]]", "M", false)]
    [InlineData("[[:alnum:]]", "9", true)]
    [InlineData("[[:alnum:]]", "Z", true)]
    [InlineData("[[:alnum:]]", "_", false)]
    [InlineData("[[:xdigit:]]", "F", true)]
    [InlineData("[[:xdigit:]]", "f", true)]
    [InlineData("[[:xdigit:]]", "g", false)]
    [InlineData("[[:space:]]", " ", true)]
    [InlineData("[[:space:]]", "\t", true)]
    [InlineData("[[:space:]]", "A", false)]
    [InlineData("[[:blank:]]", " ", true)]
    [InlineData("[[:blank:]]", "\t", true)]
    [InlineData("[[:blank:]]", "\n", false)]
    [InlineData("[[:print:]]", "A", true)]
    [InlineData("[[:print:]]", "\u0001", false)]
    [InlineData("[[:graph:]]", "A", true)]
    [InlineData("[[:graph:]]", " ", false)]
    [InlineData("[[:punct:]]", "!", true)]
    [InlineData("[[:punct:]]", "A", false)]
    public void PosixNamedClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [Theory]
    // Equivalence class `[=c=]` expands to literal `c` (no locale support).
    [InlineData("[[=a=]]", "a", true)]
    [InlineData("[[=a=]]", "b", false)]
    public void PosixEquivalenceClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [Theory]
    // Collating symbol `[.c.]` expands to literal `c` (no locale support).
    [InlineData("[[.a.]]", "a", true)]
    [InlineData("[[.a.]]", "z", false)]
    public void PosixCollatingSymbol(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [Fact]
    public void TryCompile_DefaultMaxPatternLengthOverload_CallsThrough()
    {
        // The TryCompile overload without explicit maxPatternLength routes through
        // the convenience wrapper.
        bool ok = GlobSpecification.TryCompile(
            "abc",
            GlobDialect.Posix,
            GlobOptions.None,
            out GlobSpecification? matcher,
            out GlobCompileError error);

        ok.Should().BeTrue();
        matcher.Should().NotBeNull();
        error.IsError.Should().BeFalse();
    }

    [Theory]
    // SuffixGlobStrategy's Unicode case-fold leading-dot equality returns false when
    // the suffix is genuinely different from the input.
    [InlineData("*.\u00C9", "a.\u00ea", false)]
    public void SuffixGlobStrategy_Unicode_LeadingDotFalseMismatch(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
