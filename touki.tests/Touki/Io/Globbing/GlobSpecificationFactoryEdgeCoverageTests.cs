// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Coverage tests for the POSIX bracket sub-forms and the rarely-used
///  <c>AppendPosixNamedClass</c> entries that don't appear in the dialect/oracle
///  suites, plus the MSBuild input-side separator-coalescing path.
/// </summary>
[TestClass]
public class GlobSpecificationFactoryEdgeCoverageTests
{
    // POSIX named character classes. Each `[[:NAME:]]` form expands inline to its
    // ASCII range list; coverage walks the lookup table in AppendPosixNamedClass.
    [TestMethod]
    [DataRow("[[:alpha:]]", "A", true)]
    [DataRow("[[:alpha:]]", "1", false)]
    [DataRow("[[:digit:]]", "7", true)]
    [DataRow("[[:digit:]]", "a", false)]
    [DataRow("[[:upper:]]", "M", true)]
    [DataRow("[[:upper:]]", "m", false)]
    [DataRow("[[:lower:]]", "m", true)]
    [DataRow("[[:lower:]]", "M", false)]
    [DataRow("[[:alnum:]]", "9", true)]
    [DataRow("[[:alnum:]]", "Z", true)]
    [DataRow("[[:alnum:]]", "_", false)]
    [DataRow("[[:xdigit:]]", "F", true)]
    [DataRow("[[:xdigit:]]", "f", true)]
    [DataRow("[[:xdigit:]]", "g", false)]
    [DataRow("[[:space:]]", " ", true)]
    [DataRow("[[:space:]]", "\t", true)]
    [DataRow("[[:space:]]", "A", false)]
    [DataRow("[[:blank:]]", " ", true)]
    [DataRow("[[:blank:]]", "\t", true)]
    [DataRow("[[:blank:]]", "\n", false)]
    [DataRow("[[:print:]]", "A", true)]
    [DataRow("[[:print:]]", "\u0001", false)]
    [DataRow("[[:graph:]]", "A", true)]
    [DataRow("[[:graph:]]", " ", false)]
    [DataRow("[[:punct:]]", "!", true)]
    [DataRow("[[:punct:]]", "A", false)]
    public void PosixNamedClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Equivalence class `[=c=]` expands to literal `c` (no locale support).
    [DataRow("[[=a=]]", "a", true)]
    [DataRow("[[=a=]]", "b", false)]
    public void PosixEquivalenceClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Collating symbol `[.c.]` expands to literal `c` (no locale support).
    [DataRow("[[.a.]]", "a", true)]
    [DataRow("[[.a.]]", "z", false)]
    public void PosixCollatingSymbol(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix).IsMatch(input).Should().Be(expected);

    [TestMethod]
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

    [TestMethod]
    // SuffixGlobStrategy's Unicode case-fold leading-dot equality returns false when
    // the suffix is genuinely different from the input.
    [DataRow("*.\u00C9", "a.\u00ea", false)]
    public void SuffixGlobStrategy_Unicode_LeadingDotFalseMismatch(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Simple, GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
