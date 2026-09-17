// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

[TestClass]
public class MSBuildMatchAnyFileTests
{
    [TestMethod]
    public void DirectoryFinished_AfterOutsideRoot_AllowsMatchingInsideRoot()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "MSBuildRoot");
        string outsideRoot = Path.Combine(Path.GetTempPath(), "OtherRoot");
        using MSBuildMatchAnyFile matcher = new(
            expression: "*.txt",
            rootPath,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.CaseSensitive,
            rootMatchCasing: MatchCasing.CaseSensitive,
            useMSBuildFileNameSemantics: false);

        matcher.MatchesFile(outsideRoot, "file.txt").Should().BeFalse();
        matcher.DirectoryFinished(outsideRoot);
        matcher.MatchesFile(rootPath, "file.txt").Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_MSBuildFileNameSemantics_AppliesFileSystemPolicy()
    {
        using MSBuildMatchAnyFile matcher = new(
            expression: "*.*",
            rootPath: default,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.CaseSensitive,
            rootMatchCasing: MatchCasing.CaseSensitive,
            useMSBuildFileNameSemantics: true);

        matcher.MatchesFile(default, "README").Should().BeTrue();
    }

    [TestMethod]
    public void MatchesFile_LogicalSemantics_DoesNotApplyFileSystemPolicy()
    {
        using MSBuildMatchAnyFile matcher = new(
            expression: "*.*",
            rootPath: default,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.CaseSensitive,
            rootMatchCasing: MatchCasing.CaseSensitive,
            useMSBuildFileNameSemantics: false);

        matcher.MatchesFile(default, "README").Should().BeFalse();
    }

    [TestMethod]
    public void MatchesFile_RootAndFileCasingDiffer_AppliesEachCasing()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "MSBuildRoot");
        string currentDirectory = Path.Combine(Path.GetTempPath(), "msbuildroot");
        using MSBuildMatchAnyFile matcher = new(
            expression: "file.txt",
            rootPath,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.CaseSensitive,
            rootMatchCasing: MatchCasing.CaseInsensitive,
            useMSBuildFileNameSemantics: false);

        matcher.MatchesFile(currentDirectory, "file.txt").Should().BeTrue();
        matcher.MatchesFile(currentDirectory, "FILE.TXT").Should().BeFalse();
    }
}