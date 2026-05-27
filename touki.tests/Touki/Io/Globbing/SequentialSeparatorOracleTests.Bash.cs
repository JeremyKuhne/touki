// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Bash"/> dialect handles
///  multiple sequential <c>/</c> characters inside the pattern, by shelling out to
///  <c>bash</c> via <see cref="BashInterop"/>.
/// </summary>
public class SequentialSeparatorBashOracleTests
{
    [Theory]
    [InlineData("a//b", "a/b")]
    [InlineData("a//b", "a//b")]
    [InlineData("a//b", "a///b")]
    [InlineData("a//b", "ab")]
    [InlineData("a//b", "a/x/b")]
    [InlineData("a///b", "a/b")]
    [InlineData("a///b", "a//b")]
    [InlineData("a////b", "a/b")]
    [InlineData("//a", "/a")]
    [InlineData("//a", "//a")]
    [InlineData("//a", "a")]
    [InlineData("a//", "a/")]
    [InlineData("a//", "a//")]
    [InlineData("a//", "a")]
    [InlineData("a//*", "a/b")]
    [InlineData("a//*", "a//b")]
    [InlineData("*//b", "a/b")]
    [InlineData("*//b", "a//b")]
    [InlineData("**//*.cs", "Foo.cs")]
    [InlineData("**//*.cs", "src/Foo.cs")]
    [InlineData("**//*.cs", "src/sub/Foo.cs")]
    [InlineData("a//**//b", "a/b")]
    [InlineData("a//**//b", "a/x/b")]
    [InlineData("a//**//b", "a/x/y/b")]
    public void IsMatch_BashDialect_SequentialSeparators_AgreesWithBash(string pattern, string input)
    {
        string? bashPath = BashInterop.ResolveBashPath();
        if (bashPath is null)
        {
            Assert.Skip("bash oracle requires bash on PATH (or Git for Windows installed).");
            return;
        }

        bool oracle = BashInterop.Matches(bashPath, pattern, input);
#pragma warning disable CS0618 // AllowExtGlob is reserved but not implemented; passed for forward-compat with the eventual bash extglob oracle.
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);
#pragma warning restore CS0618
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Bash) and bash [[ == ]] must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
