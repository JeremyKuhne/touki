// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

[TestClass]
public class ExtGlobExcludeProbeTests
{
    [TestMethod]
    [DataRow("bin/**", "src/bin/foo.cs", false)]    // 'bin/**' is root-anchored under MSBuild; shouldn't match nested.
    [DataRow("bin/**", "bin/foo.cs", true)]
    [DataRow("@(bin|obj)/**", "src/bin/foo.cs", false)]
    [DataRow("@(bin|obj)/**", "bin/foo.cs", true)]
    [DataRow("@(bin|obj)/**", "obj/foo.cs", true)]
    [DataRow("@(bin|obj)/**", "src/foo.cs", false)]
    public void MsBuildDialect_RootAnchored(string pattern, string input, bool expected)
    {
        bool actual = GlobSpecification
            .Compile(pattern, GlobDialect.MSBuild, GlobOptions.AllowExtGlob)
            .IsMatch(input);
        actual.Should().Be(expected, $"pattern='{pattern}' input='{input}'");
    }
}
