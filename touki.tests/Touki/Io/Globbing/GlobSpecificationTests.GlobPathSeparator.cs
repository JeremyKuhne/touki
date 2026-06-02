// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- GlobPathSeparator overload ---

    [Test]
    // DialectDefault keeps each dialect's documented separator.
    [Arguments(GlobDialect.PosixPath, GlobPathSeparator.DialectDefault, '/')]
    [Arguments(GlobDialect.Bash, GlobPathSeparator.DialectDefault, '/')]
    [Arguments(GlobDialect.MSBuild, GlobPathSeparator.DialectDefault, '/')]
    [Arguments(GlobDialect.Git, GlobPathSeparator.DialectDefault, '/')]
    [Arguments(GlobDialect.FileSystemGlobbing, GlobPathSeparator.DialectDefault, '/')]
    // Explicit ForwardSlash / Backslash forces the chosen char.
    [Arguments(GlobDialect.PosixPath, GlobPathSeparator.ForwardSlash, '/')]
    [Arguments(GlobDialect.PosixPath, GlobPathSeparator.Backslash, '\\')]
    [Arguments(GlobDialect.MSBuild, GlobPathSeparator.Backslash, '\\')]
    [Arguments(GlobDialect.Git, GlobPathSeparator.Backslash, '\\')]
    public void Compile_GlobPathSeparator_PathAware(GlobDialect dialect, GlobPathSeparator separator, char expected) =>
        GlobSpecification.Compile("*", dialect, GlobOptions.None, separator).Separator.Should().Be(expected);

    [Test]
    // Path-unaware dialects ignore the GlobPathSeparator override; their separator stays '\0'.
    [Arguments(GlobDialect.Posix, GlobPathSeparator.ForwardSlash)]
    [Arguments(GlobDialect.Posix, GlobPathSeparator.Backslash)]
    [Arguments(GlobDialect.Simple, GlobPathSeparator.ForwardSlash)]
    [Arguments(GlobDialect.PowerShell, GlobPathSeparator.Backslash)]
    public void Compile_GlobPathSeparator_PathUnaware_IsIgnored(GlobDialect dialect, GlobPathSeparator separator) =>
        GlobSpecification.Compile("*", dialect, GlobOptions.None, separator).Separator.Should().Be('\0');

    [Test]
    public void Compile_GlobPathSeparator_Backslash_GlobStarRecognizedAcrossBackslash()
    {
        // With Backslash separator, the encoder must treat `**\` as a segment-bounded globstar
        // (mirroring the default `**/` recognition for forward-slash separators).
        GlobSpecification matcher = GlobSpecification.Compile(
            @"**\*.cs",
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar,
            GlobPathSeparator.Backslash);
        matcher.IsMatch("Foo.cs").Should().BeTrue();
        matcher.IsMatch(@"a\b\Foo.cs").Should().BeTrue();
        // Path-aware `*` cannot cross the (backslash) separator: a literal `\` in the
        // input segment portion blocks the trailing `*` from gobbling it.
        matcher.IsMatch(@"a\b\Foo\Bar.cs").Should().BeTrue();
    }

    [Test]
    public void Compile_GlobPathSeparator_Backslash_StarBlockedByBackslash()
    {
        // Pattern `*.cs` with backslash separator: `*` is path-aware AnyRun and cannot
        // cross `\`. So `a\Foo.cs` should NOT match (the `a\` portion cannot be absorbed).
        GlobSpecification matcher = GlobSpecification.Compile(
            "*.cs",
            GlobDialect.PosixPath,
            GlobOptions.None,
            GlobPathSeparator.Backslash);
        matcher.IsMatch("Foo.cs").Should().BeTrue();
        matcher.IsMatch(@"a\Foo.cs").Should().BeFalse();
        // Forward slash is now a literal character; `*` matches it.
        matcher.IsMatch("a/Foo.cs").Should().BeTrue();
    }

    [Test]
    public void Compile_GlobPathSeparator_OSDefault_ResolvesToPlatformSeparator()
    {
        char expected = Path.DirectorySeparatorChar;
        GlobSpecification matcher = GlobSpecification.Compile(
            "*",
            GlobDialect.PosixPath,
            GlobOptions.None,
            GlobPathSeparator.OSDefault);
        matcher.Separator.Should().Be(expected);
    }

    [Test]
    public void Compile_GlobPathSeparator_NoEscapeDialect_NormalizesCrossSeparator()
    {
        // Compile-time normalization: when the dialect has no escape character
        // (MSBuild, FileSystemGlobbing, Simple), cross-separator literals in the
        // pattern are translated to the resolved separator so the matcher's NFA and
        // file-system inputs agree without per-call translation. Mirrors
        // MSBuildSpecification.Normalize.
        GlobSpecification matcher = GlobSpecification.Compile(
            "**/*.cs",
            GlobDialect.MSBuild,
            GlobOptions.None,
            GlobPathSeparator.Backslash);

        // Pattern `**/*.cs` was written with `/`; after normalization the encoder sees
        // `**\*.cs` and recognizes the segment-bounded globstar.
        matcher.Separator.Should().Be('\\');
        matcher.IsMatch(@"Foo.cs").Should().BeTrue();
        matcher.IsMatch(@"src\Foo.cs").Should().BeTrue();
        matcher.IsMatch(@"a\b\c\Foo.cs").Should().BeTrue();
        matcher.IsMatch(@"Foo.txt").Should().BeFalse();
    }

    [Test]
    public void Compile_GlobPathSeparator_EscapeDialect_LeavesCrossSeparatorAlone()
    {
        // For dialects with `\` as escape (POSIX-family, Bash, Git), the compile-time
        // normalization is skipped so escape sequences are not corrupted. A POSIX `\*`
        // means "literal *" regardless of GlobPathSeparator override.
        GlobSpecification matcher = GlobSpecification.Compile(
            @"a\*b",
            GlobDialect.PosixPath,
            GlobOptions.None,
            GlobPathSeparator.Backslash);

        // The `\*` is an escaped literal `*`, not a separator + wildcard.
        matcher.IsMatch("a*b").Should().BeTrue();
        matcher.IsMatch("axb").Should().BeFalse();
    }
}
