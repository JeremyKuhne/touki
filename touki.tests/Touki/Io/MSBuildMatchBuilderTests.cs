// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki.Io;

public class MSBuildMatchBuilderTests
{
    [Test]
    public void FromSpecification_StringStringOverload_NoExcludes_MatchesIncludeFiles()
    {
        using TempFolder folder = new();
        File.WriteAllText(Path.Join(folder, "a.txt"), "1");
        File.WriteAllText(Path.Join(folder, "b.cs"), "2");

        using IEnumerationMatcher matcher = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "*.txt",
            excludeSpecifications: string.Empty,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath,
            out StringSegment startDirectory);

        startDirectory.ToString().Should().Be(folder.TempPath);
        matcher.MatchesFile(folder.TempPath.AsSpan(), "a.txt".AsSpan()).Should().BeTrue();
        matcher.MatchesFile(folder.TempPath.AsSpan(), "b.cs".AsSpan()).Should().BeFalse();
    }

    [Test]
    public void FromSpecification_StringStringOverload_WithExcludes_FiltersExcluded()
    {
        using TempFolder folder = new();

        using IEnumerationMatcher matcher = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "**/*.cs",
            excludeSpecifications: "**/skip.cs",
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: folder.TempPath,
            out StringSegment startDirectory);

        startDirectory.ToString().Should().Be(folder.TempPath);
        matcher.MatchesFile(folder.TempPath.AsSpan(), "keep.cs".AsSpan()).Should().BeTrue();
        matcher.MatchesFile(folder.TempPath.AsSpan(), "skip.cs".AsSpan()).Should().BeFalse();
    }

    [Test]
    public void FromSpecification_StringStringOverload_NullRoot_UsesCurrentDirectory()
    {
        using IEnumerationMatcher matcher = MSBuildMatchBuilder.FromSpecification(
            includeSpecification: "*.txt",
            excludeSpecifications: string.Empty,
            matchType: MatchType.Simple,
            matchCasing: MatchCasing.PlatformDefault,
            rootDirectory: null,
            out StringSegment startDirectory);

        matcher.Should().NotBeNull();
        startDirectory.ToString().Should().Be(Environment.CurrentDirectory);
    }
}
