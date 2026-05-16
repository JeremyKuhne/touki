// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

public class MSBuildEnumerationResultTests
{
    private static string CurrentDriveRoot
    {
        get
        {
            string root = Path.GetPathRoot(Environment.CurrentDirectory)
                ?? throw new InvalidOperationException("No drive root.");
            return root;
        }
    }

    private static string DriveRootRecursiveSpec => $"{CurrentDriveRoot}**{Path.DirectorySeparatorChar}*.touki-no-such-file";

    [Fact]
    public void IsDriveRootRecursion_DriveRootDoubleStar_ReturnsTrue()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}**").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    [Fact]
    public void IsDriveRootRecursion_DriveRootWithSuffix_ReturnsTrue()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}**{Path.DirectorySeparatorChar}*.cs")
            .FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    [Fact]
    public void IsDriveRootRecursion_NormalRecursiveSpec_ReturnsFalse()
    {
        MSBuildSpecification spec = new MSBuildSpecification("**/*.cs").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Fact]
    public void IsDriveRootRecursion_NonRecursiveSpec_ReturnsFalse()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}*.cs").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Fact]
    public void IsDriveRootRecursion_NotFullyQualified_ReturnsFalse()
    {
        // Not fully qualified — the gate only fires post-qualification because that's the only point at
        // which we know what root the spec resolves against.
        MSBuildSpecification spec = new("**");
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Fact]
    public void IsDriveRootRecursion_FullyQualifiedNoWildcards_ReturnsFalse()
    {
        // Fully qualified literal path at the drive root, no wildcards: still safe.
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}file.txt")
            .FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Fact]
    public void IsDriveRootRecursion_DriveRootStarStarSegment_ReturnsTrue()
    {
        // Drive-rooted, wildcard path begins with "**\" but is not a simple recursive match
        // (extra fixed segments after the **). Still drive enumeration.
        MSBuildSpecification spec = new MSBuildSpecification(
                $"{CurrentDriveRoot}**{Path.DirectorySeparatorChar}foo{Path.DirectorySeparatorChar}*.cs")
            .FullyQualify(Environment.CurrentDirectory);

        spec.IsSimpleRecursiveMatch.Should().BeFalse();
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    [Fact]
    public void CreateResult_DriveRootRecursionDefault_StopsSearching()
    {
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(DriveRootRecursiveSpec);
        result.Action.Should().Be(MSBuildSearchAction.FailBecauseDriveEnumerationIsForbidden);
        result.GlobFailure.Should().NotBeNullOrEmpty();
        result.Enumerator.Should().BeNull();
    }

    [Fact]
    public void CreateResult_DriveRootRecursionOptIn_RunsSearch()
    {
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(
            DriveRootRecursiveSpec,
            options: new MSBuildEnumerationOptions { AllowDriveEnumeration = true });

        result.Action.Should().Be(MSBuildSearchAction.RunSearch);
        result.GlobFailure.Should().BeNull();
        // Don't materialize — that would actually walk the drive. Just confirm the enumerator was built.
        result.Enumerator.Should().NotBeNull();
        result.Enumerator!.Dispose();
    }

    [Fact]
    public void CreateResult_NormalInclude_RunsSearch()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "a.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "b.txt"), string.Empty);

        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(
            "*.txt",
            projectDirectory: tempFolder.TempPath);

        result.Action.Should().Be(MSBuildSearchAction.RunSearch);
        result.GlobFailure.Should().BeNull();
        result.FailedExcludeSpec.Should().BeNull();

        using MSBuildEnumerator enumerator = result.Enumerator!;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEquivalentTo("a.txt", "b.txt");
    }

    [Fact]
    public void CreateResult_WithExcludes_RunsSearchExcludingFiltered()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "keep.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "skip.txt"), string.Empty);

        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(
            fileSpec: "*.txt",
            excludeSpecs: "skip.txt",
            projectDirectory: tempFolder.TempPath);

        result.Action.Should().Be(MSBuildSearchAction.RunSearch);

        using MSBuildEnumerator enumerator = result.Enumerator!;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEquivalentTo("keep.txt");
    }

    [Fact]
    public void CreateResult_DriveRootRecursion_MatchesFileMatcherOracle()
    {
        // Parity check against MSBuild's FileMatcher: both should refuse drive-root recursion by default.
        // MSBuild's real SearchAction enum has more values than FileMatcherWrapper exposes (notably
        // FailBecauseDriveEnumerationIsForbidden = 5), so the wrapper hands back an undefined enum value.
        // We just assert the oracle did NOT run the search.
        FileMatcherWrapper.GetFilesResult oracle = FileMatcherWrapper.GetFiles(
            Environment.CurrentDirectory,
            DriveRootRecursiveSpec);

        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(DriveRootRecursiveSpec);

        oracle.Action.Should().NotBe(FileMatcherWrapper.SearchAction.RunSearch);
        oracle.FileList.Should().BeEmpty();
        result.Action.Should().Be(MSBuildSearchAction.FailBecauseDriveEnumerationIsForbidden);
    }
}
