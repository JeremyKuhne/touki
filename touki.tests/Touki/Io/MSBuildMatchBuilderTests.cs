// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

[TestClass]
public class MSBuildMatchBuilderTests
{
    [TestMethod]
    public void FromSpecification_StringStringOverload_NoExcludes_MatchesIncludeFiles()
    {
        using TempFolder folder = new();
        File.WriteAllText(Path.Join(folder, "a.txt"), "1");
        File.WriteAllText(Path.Join(folder, "b.cs"), "2");

        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "*.txt",
            excludeSpecifications: string.Empty,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        result.StartDirectory.ToString().Should().Be(folder.TempPath);
        matcher.MatchesFile(folder.TempPath.AsSpan(), "a.txt".AsSpan()).Should().BeTrue();
        matcher.MatchesFile(folder.TempPath.AsSpan(), "b.cs".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void FromSpecification_StringStringOverload_WithExcludes_FiltersExcluded()
    {
        using TempFolder folder = new();

        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/*.cs",
            excludeSpecifications: "**/skip.cs",
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        result.StartDirectory.ToString().Should().Be(folder.TempPath);
        matcher.MatchesFile(folder.TempPath.AsSpan(), "keep.cs".AsSpan()).Should().BeTrue();
        matcher.MatchesFile(folder.TempPath.AsSpan(), "skip.cs".AsSpan()).Should().BeFalse();
    }

    [TestMethod]
    public void FromSpecification_Win32MatchType_DoesNotChangeLogicalFileExclude()
    {
        using TempFolder folder = new();
        const string Candidate = "fileA.txt";
        const string ExcludeFileName = "file>.txt";

        FileMatcherWrapper.IsMatch(Candidate, ExcludeFileName).Should().BeFalse();
        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/*",
            excludeSpecifications: $"**/{ExcludeFileName}",
            matchType: MatchType.Win32,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        matcher.MatchesFile(folder.TempPath, Candidate).Should().BeTrue();
    }

    [TestMethod]
    public void FromSpecification_Win32MatchType_DoesNotChangeLogicalDirectoryExclude()
    {
        using TempFolder folder = new();
        const string Candidate = "objA";
        const string ExcludeDirectoryName = "obj>";

        FileMatcherWrapper.IsMatch(Candidate, ExcludeDirectoryName).Should().BeFalse();
        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/*",
            excludeSpecifications: $"**/{ExcludeDirectoryName}/**",
            matchType: MatchType.Win32,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        matcher.MatchesDirectory(folder.TempPath, Candidate)
            .Should().NotBe(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [TestMethod]
    public void FromSpecification_Win32IncludeToken_LiteralExcludeIsNotDropped()
    {
        using TempFolder folder = new();

        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/file>.txt",
            excludeSpecifications: "**/fileA.txt",
            matchType: MatchType.Win32,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        matcher.MatchesFile(folder.TempPath, "fileA.txt").Should().BeFalse();
    }

    [TestMethod]
    public void FromSpecification_StringStringOverload_NullRoot_UsesCurrentDirectory()
    {
        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "*.txt",
            excludeSpecifications: string.Empty,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: null);
        using IFileSystemMatcherSession matcher = result.Session;

        matcher.Should().NotBeNull();
        result.StartDirectory.ToString().Should().Be(Environment.CurrentDirectory);
    }

    [TestMethod]
    public void FromSpecification_WildcardedTerminalGlobstarExclude_PrunesAtAnyDepth()
    {
        using TempFolder folder = new();
        string nested = Path.Combine(folder.TempPath, "src");

        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/*",
            excludeSpecifications: "**/obj*/**",
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        matcher.MatchesDirectory(folder.TempPath, "obj-one")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.MatchesDirectory(folder.TempPath, "src")
            .Should().NotBe(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.DirectoryFinished(folder.TempPath);
        matcher.MatchesDirectory(nested, "obj-two")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.MatchesDirectory(nested, "lib")
            .Should().NotBe(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [TestMethod]
    public void FromSpecification_FixedTerminalGlobstarExclude_PrunesAtRootBoundary()
    {
        using TempFolder folder = new();

        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/*",
            excludeSpecifications: "obj/**",
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        matcher.MatchesDirectory(folder.TempPath, "obj")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.MatchesDirectory(folder.TempPath, "src")
            .Should().NotBe(DirectoryMatchType.NoDescendantFilesMatch);
    }

    [TestMethod]
    public void MatchMSBuildSubtree_WildcardDirectoryProof_MatchesNestedFiles()
    {
        using TempFolder folder = new();
        using MatchMSBuildSubtree matcher = new(
            folder.TempPath,
            folder.TempPath,
            "obj*",
            MatchType.Simple,
            MatchCasing.PlatformDefault);

        matcher.MatchesDirectory(folder.TempPath, "obj-one")
            .Should().Be(DirectoryMatchType.AllDescendantFilesMatch);
        matcher.DirectoryFinished(folder.TempPath);
        matcher.MatchesFile(Path.Combine(folder.TempPath, "obj-one", "nested"), "file.cs")
            .Should().BeTrue();
        matcher.DirectoryFinished(Path.Combine(folder.TempPath, "obj-one", "nested"));
        matcher.MatchesFile(Path.Combine(folder.TempPath, "src"), "file.cs")
            .Should().BeFalse();
    }

    [TestMethod]
    public void FromSpecification_ChildRootExclude_ApproachIsConservativeAndFilesAreExcluded()
    {
        using TempFolder folder = new();
        MSBuildMatchBuildResult result = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/*",
            excludeSpecifications: "src/obj/**",
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath);
        using IFileSystemMatcherSession matcher = result.Session;

        matcher.MatchesDirectory(folder.TempPath, "src")
            .Should().Be(DirectoryMatchType.MayContainMatchingFiles);
        matcher.DirectoryFinished(folder.TempPath);
        string sourceDirectory = Path.Combine(folder.TempPath, "src");
        matcher.MatchesDirectory(sourceDirectory, "obj")
            .Should().Be(DirectoryMatchType.NoDescendantFilesMatch);
        matcher.DirectoryFinished(sourceDirectory);
        matcher.MatchesFile(Path.Combine(sourceDirectory, "obj"), "file.cs")
            .Should().BeFalse();
    }
}
