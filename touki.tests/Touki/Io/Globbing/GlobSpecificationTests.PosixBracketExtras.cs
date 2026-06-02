// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- POSIX bracket-expression extras ---

    [Test]
    // [:alpha:] -> A-Za-z (positive class).
    [Arguments("[[:alpha:]]", "a", true)]
    [Arguments("[[:alpha:]]", "Z", true)]
    [Arguments("[[:alpha:]]", "5", false)]
    [Arguments("[[:alpha:]]", " ", false)]
    // [:digit:] -> 0-9.
    [Arguments("[[:digit:]]", "0", true)]
    [Arguments("[[:digit:]]", "9", true)]
    [Arguments("[[:digit:]]", "a", false)]
    // [:upper:] / [:lower:].
    [Arguments("[[:upper:]]", "A", true)]
    [Arguments("[[:upper:]]", "a", false)]
    [Arguments("[[:lower:]]", "a", true)]
    [Arguments("[[:lower:]]", "A", false)]
    // [:alnum:] union.
    [Arguments("[[:alnum:]]", "a", true)]
    [Arguments("[[:alnum:]]", "5", true)]
    [Arguments("[[:alnum:]]", "_", false)]
    // [:xdigit:] hex.
    [Arguments("[[:xdigit:]]", "f", true)]
    [Arguments("[[:xdigit:]]", "F", true)]
    [Arguments("[[:xdigit:]]", "g", false)]
    // Negated class with named class inside.
    [Arguments("[![:digit:]]", "a", true)]
    [Arguments("[![:digit:]]", "5", false)]
    // Combined with literal characters.
    [Arguments("[[:alpha:]_]", "_", true)]
    [Arguments("[[:alpha:]_]", "a", true)]
    [Arguments("[[:alpha:]_]", "5", false)]
    public void IsMatch_Posix_NamedClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);

    [Test]
    // [:space:] covers tab, LF, VT, FF, CR, and space.
    [Arguments("[[:space:]]", " ", true)]
    [Arguments("[[:space:]]", "\t", true)]
    [Arguments("[[:space:]]", "\n", true)]
    [Arguments("[[:space:]]", "a", false)]
    // [:blank:] is tab + space only.
    [Arguments("[[:blank:]]", " ", true)]
    [Arguments("[[:blank:]]", "\t", true)]
    [Arguments("[[:blank:]]", "\n", false)]
    public void IsMatch_Posix_WhitespaceClasses(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);

    [Test]
    // [=e=] / [.ch.] are accepted as no-ops equivalent to including their inner
    // characters as a literal run (POSIX C/locale fallback semantics: every collating
    // element is a single character and equivalence classes are singletons).
    [Arguments("[[=e=]]", "e", true)]
    [Arguments("[[=e=]]", "f", false)]
    [Arguments("[[.ch.]]", "c", true)]
    [Arguments("[[.ch.]]", "h", true)]
    [Arguments("[[.ch.]]", "x", false)]
    public void IsMatch_Posix_EquivAndCollating_AcceptedAsLiterals(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);
}
