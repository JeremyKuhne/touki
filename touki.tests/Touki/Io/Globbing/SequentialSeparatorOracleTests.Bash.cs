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
    [Test]
    [Arguments("a//b", "a/b")]
    [Arguments("a//b", "a//b")]
    [Arguments("a//b", "a///b")]
    [Arguments("a//b", "ab")]
    [Arguments("a//b", "a/x/b")]
    [Arguments("a///b", "a/b")]
    [Arguments("a///b", "a//b")]
    [Arguments("a////b", "a/b")]
    [Arguments("//a", "/a")]
    [Arguments("//a", "//a")]
    [Arguments("//a", "a")]
    [Arguments("a//", "a/")]
    [Arguments("a//", "a//")]
    [Arguments("a//", "a")]
    [Arguments("a//*", "a/b")]
    [Arguments("a//*", "a//b")]
    [Arguments("*//b", "a/b")]
    [Arguments("*//b", "a//b")]
    [Arguments("**//*.cs", "Foo.cs")]
    [Arguments("**//*.cs", "src/Foo.cs")]
    [Arguments("**//*.cs", "src/sub/Foo.cs")]
    [Arguments("a//**//b", "a/b")]
    [Arguments("a//**//b", "a/x/b")]
    [Arguments("a//**//b", "a/x/y/b")]
    public void IsMatch_BashDialect_SequentialSeparators_AgreesWithBash(string pattern, string input)
    {
        string? bashPath = BashInterop.ResolveBashPath();
        if (bashPath is null)
        {
            Skip.Test("bash oracle requires bash on PATH (or Git for Windows installed).");
            return;
        }

        bool oracle = BashInterop.Matches(bashPath, pattern, input);
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Bash) and bash [[ == ]] must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
