// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- POSIX bracket-expression extras ---

    [TestMethod]
    // [:alpha:] -> A-Za-z (positive class).
    [DataRow("[[:alpha:]]", "a", true)]
    [DataRow("[[:alpha:]]", "Z", true)]
    [DataRow("[[:alpha:]]", "5", false)]
    [DataRow("[[:alpha:]]", " ", false)]
    // [:digit:] -> 0-9.
    [DataRow("[[:digit:]]", "0", true)]
    [DataRow("[[:digit:]]", "9", true)]
    [DataRow("[[:digit:]]", "a", false)]
    // [:upper:] / [:lower:].
    [DataRow("[[:upper:]]", "A", true)]
    [DataRow("[[:upper:]]", "a", false)]
    [DataRow("[[:lower:]]", "a", true)]
    [DataRow("[[:lower:]]", "A", false)]
    // [:alnum:] union.
    [DataRow("[[:alnum:]]", "a", true)]
    [DataRow("[[:alnum:]]", "5", true)]
    [DataRow("[[:alnum:]]", "_", false)]
    // [:xdigit:] hex.
    [DataRow("[[:xdigit:]]", "f", true)]
    [DataRow("[[:xdigit:]]", "F", true)]
    [DataRow("[[:xdigit:]]", "g", false)]
    // Negated class with named class inside.
    [DataRow("[![:digit:]]", "a", true)]
    [DataRow("[![:digit:]]", "5", false)]
    // Combined with literal characters.
    [DataRow("[[:alpha:]_]", "_", true)]
    [DataRow("[[:alpha:]_]", "a", true)]
    [DataRow("[[:alpha:]_]", "5", false)]
    public void IsMatch_Posix_NamedClass(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // [:space:] covers tab, LF, VT, FF, CR, and space.
    [DataRow("[[:space:]]", " ", true)]
    [DataRow("[[:space:]]", "\t", true)]
    [DataRow("[[:space:]]", "\n", true)]
    [DataRow("[[:space:]]", "a", false)]
    // [:blank:] is tab + space only.
    [DataRow("[[:blank:]]", " ", true)]
    [DataRow("[[:blank:]]", "\t", true)]
    [DataRow("[[:blank:]]", "\n", false)]
    public void IsMatch_Posix_WhitespaceClasses(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // [=e=] / [.ch.] are accepted as no-ops equivalent to including their inner
    // characters as a literal run (POSIX C/locale fallback semantics: every collating
    // element is a single character and equivalence classes are singletons).
    [DataRow("[[=e=]]", "e", true)]
    [DataRow("[[=e=]]", "f", false)]
    [DataRow("[[.ch.]]", "c", true)]
    [DataRow("[[.ch.]]", "h", true)]
    [DataRow("[[.ch.]]", "x", false)]
    public void IsMatch_Posix_EquivAndCollating_AcceptedAsLiterals(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix)
            .IsMatch(input).Should().Be(expected);
}
