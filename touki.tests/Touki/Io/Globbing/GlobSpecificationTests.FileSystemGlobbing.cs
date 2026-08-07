// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- FileSystemGlobbing dialect ---

    [TestMethod]
    // FileSystemGlobbing has implicit globstar (no opt-in needed), no character classes,
    // no question-mark wildcard, and no escape character (`\` is literal).
    [DataRow("*.cs", "Foo.cs", true)]
    [DataRow("**/*.cs", "Foo.cs", true)]
    [DataRow("**/*.cs", "src/Foo.cs", true)]
    [DataRow("**/*.cs", "a/b/c/Foo.cs", true)]
    [DataRow("a/**/b", "a/b", true)]
    [DataRow("a/**/b", "a/x/y/b", true)]
    [DataRow("a/?/b", "a/?/b", true)]
    [DataRow("a/?/b", "a/x/b", false)]
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
    public void Compile_FileSystemGlobbing_ExtGlobQuestionMarkInBody_IsLiteral()
    {
        using GlobSpecification matcher = GlobSpecification.Compile(
            "@(a?b|c)",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        matcher.IsMatch("a?b").Should().BeTrue();
        matcher.IsMatch("axb").Should().BeFalse();
        matcher.IsMatch("c").Should().BeTrue();
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_ExtGlobQuestionMarkAfterLiteral_IsExtGlob()
    {
        using GlobSpecification matcher = GlobSpecification.Compile(
            "a?(b)",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        matcher.IsMatch("a").Should().BeTrue();
        matcher.IsMatch("ab").Should().BeTrue();
        matcher.IsMatch("a?(b)").Should().BeFalse();
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_StarRunBeforeExtGlob_PreservesWildcardAndOperator()
    {
        using GlobSpecification matcher = GlobSpecification.Compile(
            "ab***(e|f)g",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        matcher.IsMatch("abXYZeg").Should().BeTrue();
        matcher.IsMatch("abfg").Should().BeTrue();
        matcher.IsMatch("abXYZg").Should().BeTrue();
        matcher.IsMatch("abXYZeh").Should().BeFalse();
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_StarRunSegmentBeforeExtGlob_CollapsesToOneSegment()
    {
        using GlobSpecification matcher = GlobSpecification.Compile(
            "***/@(x)",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        matcher.IsMatch("a/x").Should().BeTrue();
        matcher.IsMatch("a/b/x").Should().BeFalse();
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_ExtGlobPattern_DoesNotApplyWholePatternRewrites()
    {
        using GlobSpecification parentMatcher = GlobSpecification.Compile(
            "@(foo/../bar|baz)",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        parentMatcher.IsMatch("foo/../bar").Should().BeTrue();

        using GlobSpecification recursiveSuffixMatcher = GlobSpecification.Compile(
            "@(foo/**.cs|bar)",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob);

        recursiveSuffixMatcher.IsMatch("foo/file.cs").Should().BeTrue();
        recursiveSuffixMatcher.IsMatch("foo/x/file.cs").Should().BeFalse();
    }

    [TestMethod]
    public void TryCompile_FileSystemGlobbing_NormalizedPrefixPreservesErrorPosition()
    {
        bool success = GlobSpecification.TryCompile(
            "**.cs/?(",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob,
            out GlobSpecification? result,
            out GlobCompileError error);

        success.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.UnterminatedExtGlob);
        error.Position.Should().Be(6);
    }

    [TestMethod]
    public void TryCompile_FileSystemGlobbing_NormalizedPrefixAdjacentEmptyExtGlob_PreservesErrorPosition()
    {
        bool success = GlobSpecification.TryCompile(
            "**.cs/@(a)@()",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.AllowExtGlob,
            out GlobSpecification? result,
            out GlobCompileError error);

        success.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.InvalidExtGlobBody);
        error.Position.Should().Be(10);
    }

    [TestMethod]
    public void TryCompile_FileSystemGlobbing_NormalizedEncoderFailure_HasNoSourcePosition()
    {
        string pattern = "x/**." + new string('a', char.MaxValue + 1) + "/z";

        bool success = GlobSpecification.TryCompile(
            pattern,
            GlobDialect.FileSystemGlobbing,
            GlobOptions.None,
            out GlobSpecification? result,
            out GlobCompileError error);

        success.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.PatternTooLarge);
        error.Position.Should().Be(-1);
    }

    [TestMethod]
    public void TryCompile_FileSystemGlobbing_ParentSegmentAfterNonParent_ReturnsError()
    {
        bool success = GlobSpecification.TryCompile(
            "a/../b",
            GlobDialect.FileSystemGlobbing,
            GlobOptions.None,
            out GlobSpecification? result,
            out GlobCompileError error);

        success.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.ParentSegmentNotAtBeginning);
        error.Position.Should().Be(2);
        error.Message.Should().Be("\"..\" can be only added at the beginning of the pattern.");
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_ParentSegmentAfterNonParent_ThrowsGlobFormatException()
    {
        Action action = () => GlobSpecification.Compile("a/../b", GlobDialect.FileSystemGlobbing);

        GlobFormatException exception = action.Should().Throw<GlobFormatException>().Which;
        exception.Error.Code.Should().Be(GlobCompileErrorCode.ParentSegmentNotAtBeginning);
        exception.Error.Position.Should().Be(2);
    }

    [TestMethod]
    public void Compile_FileSystemGlobbing_SeparatorIsForwardSlash() =>
        GlobSpecification.Compile("*", GlobDialect.FileSystemGlobbing).Separator.Should().Be('/');
}
