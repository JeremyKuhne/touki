// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

[TestClass]
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

    [TestMethod]
    public void Create_DefaultRequest_Throws()
    {
        Action action = () => MSBuildEnumerator.Create(default);

        action.Should().Throw<ArgumentException>().WithParameterName("request");
    }

    [TestMethod]
    public void CreateResult_DefaultRequest_Throws()
    {
        Action action = () => MSBuildEnumerator.CreateResult(default);

        action.Should().Throw<ArgumentException>().WithParameterName("request");
    }

    [TestMethod]
    public void IsDriveRootRecursion_DriveRootDoubleStar_ReturnsTrue()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}**").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    [TestMethod]
    public void IsDriveRootRecursion_DriveRootWithSuffix_ReturnsTrue()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}**{Path.DirectorySeparatorChar}*.cs")
            .FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    [TestMethod]
    public void IsDriveRootRecursion_NormalRecursiveSpec_ReturnsFalse()
    {
        MSBuildSpecification spec = new MSBuildSpecification("**/*.cs").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
    public void IsDriveRootRecursion_NonRecursiveSpec_ReturnsFalse()
    {
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}*.cs").FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
    public void IsDriveRootRecursion_NotFullyQualified_ReturnsFalse()
    {
        // Not fully qualified - the gate only fires post-qualification because that's the only point at
        // which we know what root the spec resolves against.
        MSBuildSpecification spec = new("**");
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
    public void IsDriveRootRecursion_FullyQualifiedNoWildcards_ReturnsFalse()
    {
        // Fully qualified literal path at the drive root, no wildcards: still safe.
        MSBuildSpecification spec = new MSBuildSpecification($"{CurrentDriveRoot}file.txt")
            .FullyQualify(Environment.CurrentDirectory);
        spec.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
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

    [TestMethod]
    public void IsDriveRootRecursion_UncRoot_ReturnsTrue()
    {
        // UNC paths are Windows-only and we don't need the share to exist; we're just exercising
        // the parser + IsDriveRootRecursion gate. Verifies that the trailing-separator
        // normalization in the gate handles the case where Path.GetPathRoot may return the UNC
        // root with or without a trailing separator depending on input / runtime.
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Assert.Inconclusive("UNC paths are Windows-only.");
        }

        MSBuildSpecification spec = new MSBuildSpecification(@"\\nonexistent-server\share\**\*.cs")
            .FullyQualify(Environment.CurrentDirectory);

        spec.IsFullyQualified.Should().BeTrue();
        spec.IsDriveRootRecursion.Should().BeTrue();
    }

    // Extra root-recursion edge cases. These all exercise specs that Normalize / FullyQualify
    // need to massage before IsDriveRootRecursion can give a meaningful answer.

    [TestMethod]
    [DataRow(@"C:\**")]                       // canonical
    [DataRow(@"c:\**")]                       // lowercase drive letter
    [DataRow(@"C:\\**")]                      // double separator after drive
    [DataRow(@"C:\\\\**")]                    // four separators
    [DataRow(@"C:/**")]                       // alt separator
    [DataRow(@"  C:\**  ")]                   // leading + trailing whitespace
    [DataRow(@"C:\**\")]                      // trailing separator on the recursive segment
    [DataRow(@"C:\**\**")]                    // duplicate ** (Normalize dedupes)
    [DataRow(@"C:\.\**")]                     // current-directory segment
    [DataRow(@"C:\foo\..\**")]                // parent segment that cancels back to root
    [DataRow(@"\**")]                         // root-relative - Path.GetFullPath resolves to current drive root, so the gate fires
    public void IsDriveRootRecursion_WindowsDriveRootVariants_ReturnTrue(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Assert.Inconclusive("Drive-letter roots are Windows-only.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(@"C:**")]                        // drive-relative (no separator) - not fully qualified
    [DataRow(@"C:relative\**")]               // drive-relative path
    [DataRow(@"..\**")]                       // parent-relative
    [DataRow(@".\**")]                        // current-relative
    public void IsDriveRootRecursion_WindowsRelativeWildcardSpecs_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Assert.Inconclusive("Drive-letter roots are Windows-only.");
        }

        // Anchor against a synthetic deep base so the assertion isn't affected by what the test
        // host's current directory happens to be. Using Environment.CurrentDirectory would make
        // `..\**` resolve to the drive root if the host happens to run from a one-deep path
        // like C:\repo, flipping the result.
        const string DeepBase = @"C:\synthetic\deep\base";
        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(DeepBase);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(@"C:\foo\**")]                   // drive-rooted but not at root
    [DataRow(@"C:\Users\**")]
    [DataRow(@"C:\foo\bar\**\*.cs")]
    public void IsDriveRootRecursion_WindowsDriveSubdirectoryRecursion_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Assert.Inconclusive("Drive-letter roots are Windows-only.");
        }

        // Drive-rooted recursive specs that don't sit AT the drive root are safe; only specs whose
        // FixedPath equals the drive root trip IsDriveRootRecursion.
        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(@"\\server\share\**")]
    [DataRow(@"\\server\share\**\*.cs")]
    [DataRow(@"\\server\share\\**")]          // double separator
    [DataRow(@"//server/share/**")]           // alt separators throughout
    [DataRow(@"  \\server\share\**  ")]       // whitespace
    public void IsDriveRootRecursion_UncRootVariants_ReturnTrue(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Assert.Inconclusive("UNC paths are Windows-only.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(@"\\server\share\foo\**")]
    [DataRow(@"\\server\share\foo\bar\**\*.cs")]
    public void IsDriveRootRecursion_UncSubdirectoryRecursion_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform != PlatformID.Win32NT)
        {
            Assert.Inconclusive("UNC paths are Windows-only.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
    [DataRow(@"/**")]
    [DataRow(@"/**/*.cs")]
    [DataRow(@"//**")]                        // double leading separator (Normalize collapses)
    [DataRow(@"/.\**")]
    public void IsDriveRootRecursion_UnixRootRecursion_ReturnTrue(string spec)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Assert.Inconclusive("Unix-style absolute roots are not the canonical root on Windows.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeTrue();
    }

    [TestMethod]
    [DataRow(@"/foo/**")]
    [DataRow(@"/usr/local/**/*.h")]
    public void IsDriveRootRecursion_UnixSubdirectoryRecursion_ReturnFalse(string spec)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            Assert.Inconclusive("Unix-style absolute roots are not the canonical root on Windows.");
        }

        MSBuildSpecification parsed = new MSBuildSpecification(spec).FullyQualify(Environment.CurrentDirectory);
        parsed.IsDriveRootRecursion.Should().BeFalse();
    }

    [TestMethod]
    public void CreateResult_DriveRootRecursionDefault_StopsSearching()
    {
        MSBuildRejectedResult result = MSBuildEnumerator.CreateResult(
            new(DriveRootRecursiveSpec)).Should().BeOfType<MSBuildRejectedResult>().Which;

        result.Reason.Should().Be(MSBuildRejectionReason.DriveEnumerationForbidden);
        result.Message.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void CreateResult_DriveRootRecursionOptIn_RunsSearch()
    {
        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new(
                DriveRootRecursiveSpec,
                allowDriveEnumeration: true)).Should().BeOfType<MSBuildSearchResult>().Which;
        using MSBuildEnumerator enumerator = result.Enumerator;

        // Don't materialize - that would actually walk the drive. Just confirm the enumerator was built.
        enumerator.Should().NotBeNull();
    }

    [TestMethod]
    public void CreateResult_NormalInclude_RunsSearch()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "a.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "b.txt"), string.Empty);

        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new("*.txt", tempFolder.TempPath)).Should().BeOfType<MSBuildSearchResult>().Which;

        using MSBuildEnumerator enumerator = result.Enumerator;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEquivalentTo("a.txt", "b.txt");
    }

    [TestMethod]
    public void CreateResult_WithExcludes_RunsSearchExcludingFiltered()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "keep.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "skip.txt"), string.Empty);

        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new(
                "*.txt",
                tempFolder.TempPath,
                excludes: "skip.txt")).Should().BeOfType<MSBuildSearchResult>().Which;

        using MSBuildEnumerator enumerator = result.Enumerator;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().BeEquivalentTo("keep.txt");
    }

    [TestMethod]
    public void CreateResult_ProjectDirectoryWithRelativeSegments_ReturnsCanonicalRelativeResults()
    {
        using TempFolder tempFolder = new();
        Directory.CreateDirectory(Path.Combine(tempFolder.TempPath, "sub"));
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "file.txt"), string.Empty);
        string projectDirectory = Path.Combine(tempFolder.TempPath, "sub", "..");
        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new("**/*.txt", projectDirectory)).Should().BeOfType<MSBuildSearchResult>().Which;
        using MSBuildEnumerator enumerator = result.Enumerator;

        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().Equal("file.txt");
    }

    [TestMethod]
    public void CreateResult_RelativeProjectDirectory_ResolvesAgainstCurrentDirectory()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "file.txt"), string.Empty);
        string projectDirectory = Path.GetRelativePath(Environment.CurrentDirectory, tempFolder.TempPath);
        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new("**/*.txt", projectDirectory)).Should().BeOfType<MSBuildSearchResult>().Which;
        using MSBuildEnumerator enumerator = result.Enumerator;

        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().Equal("file.txt");
    }

    [TestMethod]
    public void Create_CallerMutatesEnumerationOptions_TraversalUsesSnapshot()
    {
        using TempFolder tempFolder = new();
        string nested = Path.Combine(tempFolder.TempPath, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "file.txt"), string.Empty);
        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };
        using MSBuildEnumerator enumerator = MSBuildEnumerator.Create(
            new("**/*.txt", tempFolder.TempPath, enumerationOptions: options));

        options.RecurseSubdirectories = false;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().Equal(Path.Combine("nested", "file.txt"));
    }

    [TestMethod]
    public void CreateResult_CallerMutatesEnumerationOptions_TraversalUsesSnapshot()
    {
        using TempFolder tempFolder = new();
        string nested = Path.Combine(tempFolder.TempPath, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "file.txt"), string.Empty);
        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true
        };
        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new(
                "**/*.txt",
                tempFolder.TempPath,
                enumerationOptions: options)).Should().BeOfType<MSBuildSearchResult>().Which;
        using MSBuildEnumerator enumerator = result.Enumerator;

        options.RecurseSubdirectories = false;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().Equal(Path.Combine("nested", "file.txt"));
    }

    [TestMethod]
    public void CreateResult_ValidAndInvalidExcludes_AppliesFiltersAndRetainsInvalidInSourceOrder()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "keep.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "skip.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "bad...txt"), string.Empty);

        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new(
                "**/*.txt",
                tempFolder.TempPath,
                excludes: "bad...txt;**/skip.txt;a**b")).Should().BeOfType<MSBuildSearchResult>().Which;

        result.InvalidExcludeSpecifications.Should().Equal("bad...txt", "a**b");

        using MSBuildEnumerator enumerator = result.Enumerator;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        string[] expected = FileMatcherWrapper.GetFilesSimple(
            tempFolder.TempPath,
            "**/*.txt",
            ["bad...txt", "**/skip.txt", "a**b"]);

        files.Should().BeEquivalentTo(expected);
        files.Should().ContainSingle().Which.Should().Be("keep.txt");
    }

    [TestMethod]
    public void CreateResult_InvalidExcludeSpecs_CannotMutateLiveEnumeratorFilters()
    {
        using TempFolder tempFolder = new();
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "keep.txt"), string.Empty);
        File.WriteAllText(Path.Combine(tempFolder.TempPath, "bad...txt"), string.Empty);

        MSBuildSearchResult result = MSBuildEnumerator.CreateResult(
            new(
                "**/*.txt",
                tempFolder.TempPath,
                excludes: "bad...txt")).Should().BeOfType<MSBuildSearchResult>().Which;
        IList<string> exposedExcludes = (IList<string>)result.InvalidExcludeSpecifications;

        Action mutate = () => exposedExcludes[0] = "keep.txt";
        mutate.Should().Throw<NotSupportedException>();

        using MSBuildEnumerator enumerator = result.Enumerator;
        List<string> files = [];
        while (enumerator.MoveNext())
        {
            files.Add(enumerator.Current);
        }

        files.Should().ContainSingle().Which.Should().Be("keep.txt");
    }

    [TestMethod, Ignore("This test is expensive to run, should be checked manually")]
    public void CreateResult_DriveRootRecursion_MatchesFileMatcherOracle()
    {
        // Parity check against MSBuild's FileMatcher. MSBuild's default behavior depends on
        // Traits.Instance.ThrowOnDriveEnumeratingWildcard; in this test environment that trait is
        // off, so the oracle returns LogDriveEnumeratingWildcard (the search proceeds but a
        // warning is logged). Touki diverges intentionally: MSBuildEnumerationRequest.AllowDriveEnumeration
        // defaults to false, so CreateResult returns a rejected result. Both
        // outcomes share the property that the search does NOT run normally.
        FileMatcherWrapper.GetFilesResult oracle = FileMatcherWrapper.GetFiles(
            Environment.CurrentDirectory,
            DriveRootRecursiveSpec);

        MSBuildEnumerationResult result = MSBuildEnumerator.CreateResult(new(DriveRootRecursiveSpec));

        oracle.Action.Should().BeOneOf(
            FileMatcherWrapper.SearchAction.FailOnDriveEnumeratingWildcard,
            FileMatcherWrapper.SearchAction.LogDriveEnumeratingWildcard);
        oracle.FileList.Should().BeEmpty();
        result.Should().BeOfType<MSBuildRejectedResult>();
    }

    [TestMethod]
    [DataRow("foo\0bar")]                 // embedded null character
    [DataRow("foo.../bar.cs")]            // triple-dot sequence
    [DataRow("a**b")]                     // misplaced ** between non-separator chars
    [DataRow("*.cs**")]                   // misplaced ** glued to filename
    public void CreateResult_IllegalIncludeSpec_ReturnsLiteral(string spec)
    {
        MSBuildReturnLiteralResult result = MSBuildEnumerator.CreateResult(
            new(spec)).Should().BeOfType<MSBuildReturnLiteralResult>().Which;

        result.Specification.Should().Be(spec);
        result.Reason.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    public void CreateResult_WhitespaceOnlyIncludeSpec_ReturnsLiteral()
    {
        // A whitespace-only include normalizes to empty. CreateResult treats that as a
        // ReturnFileSpec error (mirroring MSBuild's "return the spec verbatim" for illegal specs)
        // rather than letting the MSBuildSpecification constructor throw.
        MSBuildReturnLiteralResult result = MSBuildEnumerator.CreateResult(
            new("   ")).Should().BeOfType<MSBuildReturnLiteralResult>().Which;

        result.Specification.Should().Be("   ");
        result.Reason.Should().NotBeNullOrEmpty();
    }
}
