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

    [Test]
    public void IsDriveRootRecursion_DriveRootDoubleStar_ReturnsTrue()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}**").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    [Test]
    public void IsDriveRootRecursion_DriveRootWithSuffix_ReturnsTrue()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}**{Path.DirectorySeparatorChar}*.cs")
            .FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    [Test]
    public void IsDriveRootRecursion_NormalRecursiveSpec_ReturnsFalse()
    {
        MSBuildSpecification spec = new MSBuildSpecification("**/*.cs").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
    public void IsDriveRootRecursion_NonRecursiveSpec_ReturnsFalse()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}*.cs").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
    public void IsDriveRootRecursion_NotFullyQualified_ReturnsFalse()
    {
        // Not fully qualified - the gate only fires post-qualification because that's the only point at
        // which we know what root the spec resolves against.
        MSBuildSpecification spec = new("**");
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
    public void IsDriveRootRecursion_FullyQualifiedNoWildcards_ReturnsFalse()
    {
        // Fully qualified literal path at the drive root, no wildcards: still safe.
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}file.txt")
            .FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
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

    [Test]
    public void IsDriveRootRecursion_UncRoot_ReturnsTrue()
    {
        // UNC paths are Windows-only and we don't need the share to exist; we're just exercising
        // the parser + IsDriveRootRecursion gate. Verifies that the trailing-separator
        // normalization in the gate handles the case where Path.GetPathRoot may return the UNC
        // root with or without a trailing separator depending on input / runtime.
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Skip.Test("UNC paths are Windows-only.");
        }

        MSBuildSpecification spec = new MSBuildSpecification(@"\\nonexistent-server\share\**\*.cs")
            .FullyQualify(Environment.CurrentDirectory);

        spec.IsFullyQualified.Should().BeTrue();
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    // Extra root-recursion edge cases. These all exercise specs that Normalize / FullyQualify
    // need to massage before IsDriveRootRecursion can give a meaningful answer.

    [Test]
    [Arguments(@"C:\**")]                       // canonical
    [Arguments(@"c:\**")]                       // lowercase drive letter
    [Arguments(@"C:\\**")]                      // double separator after drive
    [Arguments(@"C:\\\\**")]                    // four separators
    [Arguments(@"C:/**")]                       // alt separator
    [Arguments(@"  C:\**  ")]                   // leading + trailing whitespace
    [Arguments(@"C:\**\")]                      // trailing separator on the recursive segment
    [Arguments(@"C:\**\**")]                    // duplicate ** (Normalize dedupes)
    [Arguments(@"C:\.\**")]                     // current-directory segment
    [Arguments(@"C:\foo\..\**")]                // parent segment that cancels back to root
    [Arguments(@"\**")]                         // root-relative - Path.GetFullPath resolves to current drive root, so the gate fires
    public void IsDriveRootRecursion_WindowsDriveRootVariants_ReturnTrue(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Skip.Test("Drive-letter roots are Windows-only.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeTrue();
    }

    [Test]
    [Arguments(@"C:**")]                        // drive-relative (no separator) - not fully qualified
    [Arguments(@"C:relative\**")]               // drive-relative path
    [Arguments(@"..\**")]                       // parent-relative
    [Arguments(@".\**")]                        // current-relative
    public void IsDriveRootRecursion_WindowsRelativeWildcardSpecs_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Skip.Test("Drive-letter roots are Windows-only.");
        }

        // Anchor against a synthetic deep base so the assertion isn't affected by what the test
        // host's current directory happens to be. Using Environment.CurrentDirectory would make
        // `..\**` resolve to the drive root if the host happens to run from a one-deep path
        // like C:\repo, flipping the result.
        const string DeepBase = @"C:\synthetic\deep\base";
        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(DeepBase);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
    [Arguments(@"C:\foo\**")]                   // drive-rooted but not at root
    [Arguments(@"C:\Users\**")]
    [Arguments(@"C:\foo\bar\**\*.cs")]
    public void IsDriveRootRecursion_WindowsDriveSubdirectoryRecursion_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Skip.Test("Drive-letter roots are Windows-only.");
        }

        // Drive-rooted recursive specs that don't sit AT the drive root are safe; only specs whose
        // FixedPath equals the drive root trip IsDriveRootRecursion.
        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
    [Arguments(@"\\server\share\**")]
    [Arguments(@"\\server\share\**\*.cs")]
    [Arguments(@"\\server\share\\**")]          // double separator
    [Arguments(@"//server/share/**")]           // alt separators throughout
    [Arguments(@"  \\server\share\**  ")]       // whitespace
    public void IsDriveRootRecursion_UncRootVariants_ReturnTrue(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Skip.Test("UNC paths are Windows-only.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeTrue();
    }

    [Test]
    [Arguments(@"\\server\share\foo\**")]
    [Arguments(@"\\server\share\foo\bar\**\*.cs")]
    public void IsDriveRootRecursion_UncSubdirectoryRecursion_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Skip.Test("UNC paths are Windows-only.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
    [Arguments(@"/**")]
    [Arguments(@"/**/*.cs")]
    [Arguments(@"//**")]                        // double leading separator (Normalize collapses)
    [Arguments(@"/.\**")]
    public void IsDriveRootRecursion_UnixRootRecursion_ReturnTrue(string spec)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Skip.Test("Unix-style absolute roots are not the canonical root on Windows.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeTrue();
    }

    [Test]
    [Arguments(@"/foo/**")]
    [Arguments(@"/usr/local/**/*.h")]
    public void IsDriveRootRecursion_UnixSubdirectoryRecursion_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Skip.Test("Unix-style absolute roots are not the canonical root on Windows.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [Test]
    public void CreateResult_DriveRootRecursionDefault_StopsSearching()
    {
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(DriveRootRecursiveSpec);
        result.Action.Should().Be(MSBuildSearchAction.FailBecauseDriveEnumerationIsForbidden);
        result.GlobFailure.Should().NotBeNullOrEmpty();
        result.Enumerator.Should().BeNull();
    }

    [Test]
    public void CreateResult_DriveRootRecursionOptIn_RunsSearch()
    {
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(
            DriveRootRecursiveSpec,
            options: new MSBuildEnumerationOptions { AllowDriveEnumeration = true });

        result.Action.Should().Be(MSBuildSearchAction.RunSearch);
        result.GlobFailure.Should().BeNull();
        // Don't materialize - that would actually walk the drive. Just confirm the enumerator was built.
        result.Enumerator.Should().NotBeNull();
        result.Enumerator!.Dispose();
    }

    [Test]
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

    [Test]
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

    [Test, Skip("This test is expensive to run, should be checked manually")]
    public void CreateResult_DriveRootRecursion_MatchesFileMatcherOracle()
    {
        // Parity check against MSBuild's FileMatcher. MSBuild's default behavior depends on
        // Traits.Instance.ThrowOnDriveEnumeratingWildcard; in this test environment that trait is
        // off, so the oracle returns LogDriveEnumeratingWildcard (the search proceeds but a
        // warning is logged). Touki diverges intentionally: MSBuildEnumerationOptions.AllowDriveEnumeration
        // defaults to false, so CreateResult returns FailBecauseDriveEnumerationIsForbidden. Both
        // outcomes share the property that the search does NOT run normally.
        FileMatcherWrapper.GetFilesResult oracle = FileMatcherWrapper.GetFiles(
            Environment.CurrentDirectory,
            DriveRootRecursiveSpec);

        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(DriveRootRecursiveSpec);

        oracle.Action.Should().BeOneOf(
            FileMatcherWrapper.SearchAction.FailOnDriveEnumeratingWildcard,
            FileMatcherWrapper.SearchAction.LogDriveEnumeratingWildcard);
        oracle.FileList.Should().BeEmpty();
        result.Action.Should().Be(MSBuildSearchAction.FailBecauseDriveEnumerationIsForbidden);
    }

    [Test]
    [Arguments("foo\0bar")]                 // embedded null character
    [Arguments("foo.../bar.cs")]            // triple-dot sequence
    [Arguments("a**b")]                     // misplaced ** between non-separator chars
    [Arguments("*.cs**")]                   // misplaced ** glued to filename
    public void CreateResult_IllegalIncludeSpec_ReturnsFileSpecAction(string spec)
    {
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(spec);

        result.Action.Should().Be(MSBuildSearchAction.ReturnFileSpec);
        result.Enumerator.Should().BeNull();
        result.GlobFailure.Should().NotBeNullOrEmpty();
        result.FailedExcludeSpec.Should().BeNull();
    }

    [Test]
    public void CreateResult_WhitespaceOnlyIncludeSpec_ReturnsFileSpecAction()
    {
        // A whitespace-only include normalizes to empty. CreateResult treats that as a
        // ReturnFileSpec error (mirroring MSBuild's "return the spec verbatim" for illegal specs)
        // rather than letting the MSBuildSpecification constructor throw.
        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult("   ");

        result.Action.Should().Be(MSBuildSearchAction.ReturnFileSpec);
        result.Enumerator.Should().BeNull();
        result.GlobFailure.Should().NotBeNullOrEmpty();
    }
}
