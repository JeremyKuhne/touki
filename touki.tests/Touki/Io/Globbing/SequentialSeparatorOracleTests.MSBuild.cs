// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Build.Globbing;

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.MSBuild"/> dialect handles
///  multiple sequential <c>/</c> characters inside the pattern, by comparing each
///  verdict against <see cref="MSBuildGlob"/> from <c>Microsoft.Build</c>.
/// </summary>
/// <remarks>
///  <para>
///   MSBuild's <c>FileSpec</c> normalization (mirrored by
///   <c>Touki.Io.MSBuildSpecification.Normalize</c>) coalesces runs of separators inside
///   the wildcard portion of a spec down to a single separator. The compiled
///   <see cref="GlobSpecification"/> for <see cref="GlobDialect.MSBuild"/> applies the same
///   normalization at compile time, so touki and MSBuild should agree on every input.
///  </para>
///  <para>
///   If a row here ever fails, either touki's MSBuild dialect drifted or MSBuild's
///   <c>MSBuildGlob</c> changed its coalescing rules &#8212; both warrant a deliberate
///   update to the dialect contract documented in <c>docs/globbing.md</c>.
///  </para>
/// </remarks>
public class SequentialSeparatorMSBuildOracleTests
{
    private static bool OracleMatches(string pattern, string input)
    {
        // Parse with an empty fixed-directory portion so the entire pattern is treated
        // as the wildcard glob. MSBuildGlob normalizes input paths against globRoot
        // internally; passing the current directory keeps the comparison stable across
        // hosts.
        MSBuildGlob glob = MSBuildGlob.Parse(Directory.GetCurrentDirectory(), pattern);
        return glob.IsMatch(input);
    }

    private static bool ToukiMatches(string pattern, string input) =>
        GlobSpecification.Compile(pattern, GlobDialect.MSBuild).IsMatch(input);

    [Theory]
    // --- Doubled separator between literal segments ---
    [InlineData("a//b", "a/b")]
    [InlineData("a//b", "a//b")]
    [InlineData("a//b", "a///b")]
    [InlineData("a//b", "ab")]
    [InlineData("a//b", "a/x/b")]
    // --- Tripled / quadrupled separator runs ---
    [InlineData("a///b", "a/b")]
    [InlineData("a///b", "a//b")]
    [InlineData("a////b", "a/b")]
    // --- Leading separator runs ---
    [InlineData("//a", "/a")]
    [InlineData("//a", "a")]
    [InlineData("//a", "//a")]
    // --- Trailing separator runs ---
    [InlineData("a//", "a/")]
    [InlineData("a//", "a")]
    [InlineData("a//", "a//")]
    // --- Doubled separator surrounding a wildcard ---
    [InlineData("a//*", "a/b")]
    [InlineData("a//*", "a//b")]
    [InlineData("*//b", "a/b")]
    [InlineData("*//b", "a//b")]
    // --- Doubled separator adjacent to globstar ---
    [InlineData("**//*.cs", "Foo.cs")]
    [InlineData("**//*.cs", "src/Foo.cs")]
    [InlineData("**//*.cs", "src/sub/Foo.cs")]
    [InlineData("a//**//b", "a/b")]
    [InlineData("a//**//b", "a/x/b")]
    [InlineData("a//**//b", "a/x/y/b")]
    public void IsMatch_MSBuildDialect_SequentialSeparators_AgreesWithMSBuildGlob(string pattern, string input)
    {
        bool oracle = OracleMatches(pattern, input);
        bool actual = ToukiMatches(pattern, input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(MSBuild) and MSBuildGlob must agree on pattern '{pattern}' vs input '{input}'");
    }
}
