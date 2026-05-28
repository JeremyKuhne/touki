// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public class ExtGlobExcludeProbeTests
{
    [Theory]
    [InlineData("bin/**", "src/bin/foo.cs", false)]    // 'bin/**' is root-anchored under MSBuild; shouldn't match nested.
    [InlineData("bin/**", "bin/foo.cs", true)]
    [InlineData("@(bin|obj)/**", "src/bin/foo.cs", false)]
    [InlineData("@(bin|obj)/**", "bin/foo.cs", true)]
    [InlineData("@(bin|obj)/**", "obj/foo.cs", true)]
    [InlineData("@(bin|obj)/**", "src/foo.cs", false)]
    public void MsBuildDialect_RootAnchored(string pattern, string input, bool expected)
    {
        bool actual = GlobSpecification
            .Compile(pattern, GlobDialect.MSBuild, GlobOptions.AllowExtGlob)
            .IsMatch(input);
        actual.Should().Be(expected, $"pattern='{pattern}' input='{input}'");
    }
}
