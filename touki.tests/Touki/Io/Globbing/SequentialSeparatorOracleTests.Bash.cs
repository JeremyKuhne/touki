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
[TestClass]
public class SequentialSeparatorBashOracleTests
{
    [TestMethod]
    [DataRow("a//b", "a/b")]
    [DataRow("a//b", "a//b")]
    [DataRow("a//b", "a///b")]
    [DataRow("a//b", "ab")]
    [DataRow("a//b", "a/x/b")]
    [DataRow("a///b", "a/b")]
    [DataRow("a///b", "a//b")]
    [DataRow("a////b", "a/b")]
    [DataRow("//a", "/a")]
    [DataRow("//a", "//a")]
    [DataRow("//a", "a")]
    [DataRow("a//", "a/")]
    [DataRow("a//", "a//")]
    [DataRow("a//", "a")]
    [DataRow("a//*", "a/b")]
    [DataRow("a//*", "a//b")]
    [DataRow("*//b", "a/b")]
    [DataRow("*//b", "a//b")]
    [DataRow("**//*.cs", "Foo.cs")]
    [DataRow("**//*.cs", "src/Foo.cs")]
    [DataRow("**//*.cs", "src/sub/Foo.cs")]
    [DataRow("a//**//b", "a/b")]
    [DataRow("a//**//b", "a/x/b")]
    [DataRow("a//**//b", "a/x/y/b")]
    public void IsMatch_BashDialect_SequentialSeparators_AgreesWithBash(string pattern, string input)
    {
        string? bashPath = BashInterop.ResolveBashPath();
        if (bashPath is null)
        {
            Assert.Inconclusive("bash oracle requires bash on PATH (or Git for Windows installed).");
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
