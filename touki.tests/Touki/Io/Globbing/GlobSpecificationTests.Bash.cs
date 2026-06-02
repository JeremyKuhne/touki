// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- Bash dialect ---

    [Test]
    // Bash without `shopt -s globstar` is path-aware and treats `**` like `*` (no
    // segment crossing). Bracket classes, `[!neg]` negation, and `\`-escape are all
    // supported.
    [Arguments("*.cs", "Foo.cs", true)]
    [Arguments("*.cs", "src/Foo.cs", false)]
    [Arguments("a/?/b", "a/x/b", true)]
    [Arguments("a/?/b", "a//b", false)]
    [Arguments("[abc].txt", "a.txt", true)]
    [Arguments("[!abc].txt", "d.txt", true)]
    [Arguments("[!abc].txt", "a.txt", false)]
    [Arguments("\\*.cs", "*.cs", true)]
    [Arguments("\\*.cs", "Foo.cs", false)]
    public void IsMatch_Bash_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Bash)
            .IsMatch(input).Should().Be(expected);

    [Test]
    // Bash with `shopt -s globstar` (opt-in via AllowGlobStar) matches across segments.
    [Arguments("**/*.cs", "Foo.cs", true)]
    [Arguments("**/*.cs", "src/Foo.cs", true)]
    [Arguments("**/*.cs", "a/b/c/Foo.cs", true)]
    [Arguments("**/*.cs", "Foo.txt", false)]
    [Arguments("a/**/b", "a/b", true)]
    [Arguments("a/**/b", "a/x/b", true)]
    [Arguments("a/**/b", "a/x/y/b", true)]
    public void IsMatch_Bash_GlobStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [Test]
    public void Compile_Bash_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.Bash).Separator.Should().Be('/');
}
