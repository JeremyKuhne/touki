// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- Bash dialect ---

    [Theory]
    // Bash without `shopt -s globstar` is path-aware and treats `**` like `*` (no
    // segment crossing). Bracket classes, `[!neg]` negation, and `\`-escape are all
    // supported.
    [InlineData("*.cs", "Foo.cs", true)]
    [InlineData("*.cs", "src/Foo.cs", false)]
    [InlineData("a/?/b", "a/x/b", true)]
    [InlineData("a/?/b", "a//b", false)]
    [InlineData("[abc].txt", "a.txt", true)]
    [InlineData("[!abc].txt", "d.txt", true)]
    [InlineData("[!abc].txt", "a.txt", false)]
    [InlineData("\\*.cs", "*.cs", true)]
    [InlineData("\\*.cs", "Foo.cs", false)]
    public void IsMatch_Bash_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Bash)
            .IsMatch(input).Should().Be(expected);

    [Theory]
    // Bash with `shopt -s globstar` (opt-in via AllowGlobStar) matches across segments.
    [InlineData("**/*.cs", "Foo.cs", true)]
    [InlineData("**/*.cs", "src/Foo.cs", true)]
    [InlineData("**/*.cs", "a/b/c/Foo.cs", true)]
    [InlineData("**/*.cs", "Foo.txt", false)]
    [InlineData("a/**/b", "a/b", true)]
    [InlineData("a/**/b", "a/x/b", true)]
    [InlineData("a/**/b", "a/x/y/b", true)]
    public void IsMatch_Bash_GlobStar(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar)
            .IsMatch(input).Should().Be(expected);

    [Fact]
    public void Compile_Bash_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.Bash).Separator.Should().Be('/');
}
