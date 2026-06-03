// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- FileSystemGlobbing dialect ---

    [TestMethod]
    // FileSystemGlobbing has implicit globstar (no opt-in needed), no character classes,
    // and no escape character (`\` is literal).
    [DataRow("*.cs", "Foo.cs", true)]
    [DataRow("**/*.cs", "Foo.cs", true)]
    [DataRow("**/*.cs", "src/Foo.cs", true)]
    [DataRow("**/*.cs", "a/b/c/Foo.cs", true)]
    [DataRow("a/**/b", "a/b", true)]
    [DataRow("a/**/b", "a/x/y/b", true)]
    [DataRow("a/?/b", "a/x/b", true)]
    [DataRow("a/?/b", "a//b", false)]
    public void IsMatch_FileSystemGlobbing_BasicCases(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(pattern, GlobDialect.FileSystemGlobbing)
            .IsMatch(input).Should().Be(expected);

    [TestMethod]
    public void Compile_FileSystemGlobbing_BracketsAreLiteral()
    {
        // FileSystemGlobbing does not support character classes; '[' and ']' are literal
        // characters in the pattern.
        GlobSpecification matcher = GlobSpecification.Compile("[abc].txt", GlobDialect.FileSystemGlobbing);
        matcher.IsMatch("[abc].txt").Should().BeTrue();
        matcher.IsMatch("a.txt").Should().BeFalse();
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_BackslashNormalizedToSeparator()
    {
        // FileSystemGlobbing has no escape character. At compile time the factory
        // normalizes cross-separator characters to the matcher's separator (mirroring
        // MSBuildSpecification.Normalize) so the runtime matcher never has to
        // translate. Pattern `\foo` is therefore equivalent to `/foo`, which is
        // anchored to the implicit root and matches the relative file name `foo`.
        GlobSpecification matcher = GlobSpecification.Compile("\\foo", GlobDialect.FileSystemGlobbing);
        matcher.IsMatch("foo").Should().BeTrue();
        matcher.IsMatch("\\foo").Should().BeFalse();
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.FileSystemGlobbing).Separator.Should().Be('/');
}
