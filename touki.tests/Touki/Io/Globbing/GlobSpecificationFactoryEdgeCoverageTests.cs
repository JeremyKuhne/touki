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
    [Test]
    [Arguments("[[:alpha:]]", "A", true)]
    [Arguments("[[:alpha:]]", "1", false)]
    [Arguments("[[:digit:]]", "7", true)]
    [Arguments("[[:digit:]]", "a", false)]
    [Arguments("[[:upper:]]", "M", true)]
    [Arguments("[[:upper:]]", "m", false)]
    [Arguments("[[:lower:]]", "m", true)]
    [Arguments("[[:lower:]]", "M", false)]
    [Arguments("[[:alnum:]]", "9", true)]
    [Arguments("[[:alnum:]]", "Z", true)]
    [Arguments("[[:alnum:]]", "_", false)]
    [Arguments("[[:xdigit:]]", "F", true)]
    [Arguments("[[:xdigit:]]", "f", true)]
    [Arguments("[[:xdigit:]]", "g", false)]
    [Arguments("[[:space:]]", " ", true)]
    [Arguments("[[:space:]]", "\t", true)]
    [Arguments("[[:space:]]", "A", false)]
    [Arguments("[[:blank:]]", " ", true)]
    [Arguments("[[:blank:]]", "\t", true)]
    [Arguments("[[:blank:]]", "\n", false)]
    [Arguments("[[:print:]]", "A", true)]
    [Arguments("[[:print:]]", "\u0001", false)]
    [Arguments("[[:graph:]]", "A", true)]
    [Arguments("[[:graph:]]", " ", false)]
    [Arguments("[[:punct:]]", "!", true)]
    [Arguments("[[:punct:]]", "A", false)]
    public void PosixNamedClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [Test]
    // Equivalence class `[=c=]` expands to literal `c` (no locale support).
    [Arguments("[[=a=]]", "a", true)]
    [Arguments("[[=a=]]", "b", false)]
    public void PosixEquivalenceClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [Test]
    // Collating symbol `[.c.]` expands to literal `c` (no locale support).
    [Arguments("[[.a.]]", "a", true)]
    [Arguments("[[.a.]]", "z", false)]
    public void PosixCollatingSymbol(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [Test]
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

    [Test]
    // SuffixGlobStrategy's Unicode case-fold leading-dot equality returns false when
    // the suffix is genuinely different from the input.
    [Arguments("*.\u00C9", "a.\u00ea", false)]
    public void SuffixGlobStrategy_Unicode_LeadingDotFalseMismatch(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
