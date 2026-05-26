// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- POSIX bracket-expression extras ---

    [Theory]
    // [:alpha:] -> A-Za-z (positive class).
    [InlineData("[[:alpha:]]", "a", true)]
    [InlineData("[[:alpha:]]", "Z", true)]
    [InlineData("[[:alpha:]]", "5", false)]
    [InlineData("[[:alpha:]]", " ", false)]
    // [:digit:] -> 0-9.
    [InlineData("[[:digit:]]", "0", true)]
    [InlineData("[[:digit:]]", "9", true)]
    [InlineData("[[:digit:]]", "a", false)]
    // [:upper:] / [:lower:].
    [InlineData("[[:upper:]]", "A", true)]
    [InlineData("[[:upper:]]", "a", false)]
    [InlineData("[[:lower:]]", "a", true)]
    [InlineData("[[:lower:]]", "A", false)]
    // [:alnum:] union.
    [InlineData("[[:alnum:]]", "a", true)]
    [InlineData("[[:alnum:]]", "5", true)]
    [InlineData("[[:alnum:]]", "_", false)]
    // [:xdigit:] hex.
    [InlineData("[[:xdigit:]]", "f", true)]
    [InlineData("[[:xdigit:]]", "F", true)]
    [InlineData("[[:xdigit:]]", "g", false)]
    // Negated class with named class inside.
    [InlineData("[![:digit:]]", "a", true)]
    [InlineData("[![:digit:]]", "5", false)]
    // Combined with literal characters.
    [InlineData("[[:alpha:]_]", "_", true)]
    [InlineData("[[:alpha:]_]", "a", true)]
    [InlineData("[[:alpha:]_]", "5", false)]
    public void IsMatch_Posix_NamedClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // [:space:] covers tab, LF, VT, FF, CR, and space.
    [InlineData("[[:space:]]", " ", true)]
    [InlineData("[[:space:]]", "\t", true)]
    [InlineData("[[:space:]]", "\n", true)]
    [InlineData("[[:space:]]", "a", false)]
    // [:blank:] is tab + space only.
    [InlineData("[[:blank:]]", " ", true)]
    [InlineData("[[:blank:]]", "\t", true)]
    [InlineData("[[:blank:]]", "\n", false)]
    public void IsMatch_Posix_WhitespaceClasses(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // [=e=] / [.ch.] are accepted as no-ops equivalent to including their inner
    // characters as a literal run (POSIX C/locale fallback semantics: every collating
    // element is a single character and equivalence classes are singletons).
    [InlineData("[[=e=]]", "e", true)]
    [InlineData("[[=e=]]", "f", false)]
    [InlineData("[[.ch.]]", "c", true)]
    [InlineData("[[.ch.]]", "h", true)]
    [InlineData("[[.ch.]]", "x", false)]
    public void IsMatch_Posix_EquivAndCollating_AcceptedAsLiterals(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);
}
