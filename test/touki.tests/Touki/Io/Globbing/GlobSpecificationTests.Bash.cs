// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

[TestClass]
public partial class GlobSpecificationTests
{
    // --- Bash dialect ---

    [TestMethod]
    // Bash without `shopt -s globstar` is path-aware and treats `**` like `*` (no
    // segment crossing). Bracket classes, `[!neg]` negation, and `\`-escape are all
    // supported.
    [DataRow("*.cs", "Foo.cs", true)]
    [DataRow("*.cs", "src/Foo.cs", false)]
    [DataRow("a/?/b", "a/x/b", true)]
    [DataRow("a/?/b", "a//b", false)]
    [DataRow("[abc].txt", "a.txt", true)]
    [DataRow("[!abc].txt", "d.txt", true)]
    [DataRow("[!abc].txt", "a.txt", false)]
    [DataRow("\\*.cs", "*.cs", true)]
    [DataRow("\\*.cs", "Foo.cs", false)]
    public void IsMatch_Bash_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Bash)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    // Bash with `shopt -s globstar` (opt-in via AllowGlobStar) matches across segments.
    [DataRow("**/*.cs", "Foo.cs", true)]
    [DataRow("**/*.cs", "src/Foo.cs", true)]
    [DataRow("**/*.cs", "a/b/c/Foo.cs", true)]
    [DataRow("**/*.cs", "Foo.txt", false)]
    [DataRow("a/**/b", "a/b", true)]
    [DataRow("a/**/b", "a/x/b", true)]
    [DataRow("a/**/b", "a/x/y/b", true)]
    public void IsMatch_Bash_GlobStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    public void Compile_Bash_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.Bash).Separator.Should().Be('/');
}
