// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;
using Touki.Text;

namespace Touki.Io;

[TestClass]
public class MSBuildSpecificationTests
{
    private static string Sep(string path) => Paths.ChangeAlternateDirectorySeparators(path);

    [TestMethod]
    [DataRow("file.txt", "", "", "file.txt", false)]
    [DataRow("**", "", "**", "*", true)]
    [DataRow("*.txt", "", "", "*.txt", true)]
    [DataRow("a/b/file.txt", "a/b", "", "file.txt", false)]
    [DataRow("a/b/*.txt", "a/b", "", "*.txt", true)]
    [DataRow("a/*/file.txt", "a", "*", "file.txt", true)]
    [DataRow("a/**/file.txt", "a", "**", "file.txt", true)]
    [DataRow("a/?/file.txt", "a", "?", "file.txt", true)]
    [DataRow("*/file.txt", "", "*", "file.txt", true)]
    [DataRow("**/file.txt", "", "**", "file.txt", true)]
    [DataRow("a/b/**", "a/b", "**", "*", true)]
    [DataRow("**/b/**", "", "**/b/**", "*", true)]
    // Rooted-with-only-leading-separator cases. FixedPath is the root separator (e.g. "/" on
    // Unix, "\" on Windows) instead of an empty slice, and WildPath/FileName line up with the
    // suffix the same way they would for non-rooted specs.
    [DataRow("/", "/", "", "", false)]
    [DataRow("/foo.txt", "/", "", "foo.txt", false)]
    [DataRow("/*.cs", "/", "", "*.cs", true)]
    [DataRow("/**", "/", "**", "*", true)]
    [DataRow("/**/file.txt", "/", "**", "file.txt", true)]
    [DataRow("/**/*.cs", "/", "**", "*.cs", true)]
    [DataRow("/**/foo/*.cs", "/", "**/foo", "*.cs", true)]
    [DataRow("/**/foo/**", "/", "**/foo/**", "*", true)]
    public void MSBuildSpecification_ParsesCorrectly(
        string original,
        string expectedFixed,
        string expectedWildCard,
        string expectedFileName,
        bool expectedAnyWildCards)
    {
        MSBuildSpecification spec = new(original);

        expectedFixed = expectedFixed.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        expectedWildCard = expectedWildCard.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        expectedFileName = expectedFileName.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        spec.Original.ToString().Should().Be(original);
        spec.Normalized.ToString().Should().Be(original.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        spec.FixedPath.ToString().Should().Be(expectedFixed);
        spec.WildPath.ToString().Should().Be(expectedWildCard);
        spec.FileName.ToString().Should().Be(expectedFileName);
        spec.HasAnyWildCards.Should().Be(expectedAnyWildCards);
    }

    [TestMethod]
    [DataRow("a\\b\\file.txt", "a/b/file.txt")]
    [DataRow("a/b\\file.txt", "a/b/file.txt")]
    [DataRow("\\a\\b\\file.txt", "/a/b/file.txt")]
    public void MSBuildSpecification_NormalizesPaths(string original, string expected)
    {
        expected = expected.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        MSBuildSpecification spec = new(original);
        spec.Normalized.ToString().Should().Be(expected);
    }

    [TestMethod]
    [DataRow("a/**/**/b", "a/**/b")]
    [DataRow("a/**/**/**/b", "a/**/b")]
    [DataRow("**/**", "**")]
    [DataRow("**/**/", "**/")]
    [DataRow("a/**/**", "a/**")]
    [DataRow("a/**/**/", "a/**/")]
    [DataRow("/a/**/**/b", "/a/**/b")]
    [DataRow("/a/**/**/b/", "/a/**/b/")]
    [DataRow("a\\**\\**\\b", "a/**/b")]
    [DataRow("a/**\\**/b", "a/**/b")]
    [DataRow("a/**/c/**/b", "a/**/c/**/b")]
    public void MSBuildSpecification_NormalizesDuplicateRecursiveWildcards(string original, string expected)
    {
        expected = expected.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        MSBuildSpecification spec = new(original);
        spec.Normalized.ToString().Should().Be(expected);
    }

    [TestMethod]
    [DataRow("file.txt", "file.txt")]
    [DataRow("a/b/c.txt", "a/b/c.txt")]
    public void MSBuildSpecification_ToString_ReturnsNormalized(string original, string expected)
    {
        expected = expected.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        MSBuildSpecification spec = new(original);
        spec.ToString().Should().Be(expected);
    }

    [TestMethod]
    [DataRow("./file.txt", "", "", "file.txt", false)]
    [DataRow("../file.txt", "..", "", "file.txt", false)]
    [DataRow("a/./file.txt", "a", "", "file.txt", false)]
    [DataRow("a/../file.txt", "", "", "file.txt", false)]
    [DataRow("a/b/../../file.txt", "", "", "file.txt", false)]
    [DataRow("a/./b/c/file.txt", "a/b/c", "", "file.txt", false)]
    [DataRow("a/b/c/./../file.txt", "a/b", "", "file.txt", false)]
    [DataRow("a//b///file.txt", "a/b", "", "file.txt", false)]
    // Trailing separator on a path with no wildcards: FixedPath becomes the directory portion and
    // FileName is empty. End-to-end this matches MSBuild's FileMatcher behavior at the parsing layer
    // (an empty filenamePart matches nothing on disk). See MSBuildSpecificationTrailingSeparatorTests
    // for the full discussion, including MSBuild's no-wildcard GetFiles shortcut.
    [DataRow("a/b/file.txt/", "a/b/file.txt", "", "", false)]
    [DataRow("a/b/./", "a/b", "", "", false)]
    [DataRow("./*/file.txt", "", "*", "file.txt", true)]
    [DataRow("../*/file.txt", "..", "*", "file.txt", true)]
    [DataRow("a/./**/file.txt", "a", "**", "file.txt", true)]
    [DataRow("a/../**/file.txt", "", "**", "file.txt", true)]
    [DataRow("a/b/../../*/file.txt", "", "*", "file.txt", true)]
    [DataRow("a/./b/*/c/*.txt", "a/b", "*/c", "*.txt", true)]
    [DataRow("a//b//*/file.txt", "a/b", "*", "file.txt", true)]
    [DataRow("a/b/**/", "a/b", "**", "*", true)]
    [DataRow("./a/../**", "", "**", "*", true)]
    public void MSBuildSpecification_ParsesNonNormalizedPaths(
        string original,
        string expectedFixed,
        string expectedWildCard,
        string expectedFileName,
        bool expectedAnyWildCards)
    {
        MSBuildSpecification spec = new(original);

        expectedFixed = Sep(expectedFixed);
        expectedWildCard = Sep(expectedWildCard);
        expectedFileName = Sep(expectedFileName);

        spec.Original.ToString().Should().Be(original);
        spec.FixedPath.ToString().Should().Be(expectedFixed);
        spec.WildPath.ToString().Should().Be(expectedWildCard);
        spec.FileName.ToString().Should().Be(expectedFileName);
        spec.HasAnyWildCards.Should().Be(expectedAnyWildCards);
    }

    [TestMethod]
    [DataRow("C:/path/file.txt", true, false)]
    [DataRow("/absolute/path/file.txt", false, false)]
    [DataRow("//server/share/file.txt", true, false)]
    [DataRow("relative/path/file.txt", false, true)]
    [DataRow("./relative/path/file.txt", false, true)]
    [DataRow("../parent/path/file.txt", false, false)]
    [DataRow("C:relative/file.txt", false, false)] // Drive-relative path
    public void MSBuildSpecification_SetsPathQualificationPropertiesCorrectly(
        string path,
        bool expectedIsFullyQualified,
        bool expectedIsNestedRelative)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        MSBuildSpecification spec = new(path);

        spec.IsFullyQualified.Should().Be(expectedIsFullyQualified);
        spec.IsNestedRelative.Should().Be(expectedIsNestedRelative);
    }

    [TestMethod]
    [DataRow("file.txt", false, true)]
    [DataRow("folder/file.txt", false, true)]
    [DataRow("../file.txt", false, false)]
    [DataRow("../../file.txt", false, false)]
    [DataRow("folder/../file.txt", false, true)] // Normalized doesn't contain '..'
    [DataRow("C:/folder/file.txt", true, false)]
    public void MSBuildSpecification_HandlesSpecialPathsCorrectly(
        string path,
        bool expectedIsFullyQualified,
        bool expectedIsNestedRelative)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        MSBuildSpecification spec = new(path);

        spec.IsFullyQualified.Should().Be(expectedIsFullyQualified);
        spec.IsNestedRelative.Should().Be(expectedIsNestedRelative);
    }

    [TestMethod]
    public void IsNestedRelative_ReturnsTrue_ForPathsBelow_CurrentDirectory()
    {
        // These paths are guaranteed to be below the current directory
        new MSBuildSpecification("file.txt").IsNestedRelative.Should().BeTrue();
        new MSBuildSpecification("./file.txt").IsNestedRelative.Should().BeTrue();
        new MSBuildSpecification("folder/file.txt").IsNestedRelative.Should().BeTrue();
    }

    [TestMethod]
    public void IsNestedRelative_ReturnsFalse_ForPathsNotGuaranteedToBeBelow_CurrentDirectory()
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        // These paths may navigate outside the current directory
        new MSBuildSpecification("../file.txt").IsNestedRelative.Should().BeFalse();
        new MSBuildSpecification("C:/file.txt").IsNestedRelative.Should().BeFalse();
        new MSBuildSpecification("C:file.txt").IsNestedRelative.Should().BeFalse(); // Drive-relative
        new MSBuildSpecification("/root/file.txt").IsNestedRelative.Should().BeFalse();
    }

    [TestMethod]
    [DataRow("C:/file.txt", true)]
    [DataRow("C:\\file.txt", true)]
    [DataRow("\\\\server\\share\\file.txt", true)]
    public void IsFullyQualified_WindowsSpecificPaths(string path, bool expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        MSBuildSpecification spec = new(path);
        spec.IsFullyQualified.Should().Be(expected);
    }

#if NET
    [TestMethod]
    [DataRow("/root/file.txt", true)]
    [DataRow("~/file.txt", false)] // Not fully qualified, needs expansion
    public void IsFullyQualified_UnixSpecificPaths(string path, bool expected)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        MSBuildSpecification spec = new(path);
        spec.IsFullyQualified.Should().Be(expected);
    }
#endif

    // TODO: Add Windows specific tests for UNCs.

    [TestMethod]
    [DataRow("**", true)]
    [DataRow("**/", true)]
    [DataRow("/**/", true)]
    [DataRow("a/**", true)]
    [DataRow("a/**/", true)]
    [DataRow("a/b/**", true)]
    [DataRow("**/file.txt", true)]
    [DataRow("a/**/file.txt", true)]
    [DataRow("a/*/file.txt", false)]
    [DataRow("a/?/file.txt", false)]
    [DataRow("a/?/**/file.txt", false)]
    [DataRow("a/b/*.txt", false)]
    [DataRow("a/b/**/c", true)]
    public void SimpleRecursiveMatch_IsSetCorrectly(string pattern, bool expected)
    {
        MSBuildSpecification spec = new(pattern);
        spec.IsSimpleRecursiveMatch.Should().Be(expected);
    }

    [TestMethod]
    public void Unescape_ReturnsOriginalSegment_WhenNoEscapeCharacters()
    {
        StringSegment original = "file.txt";
        StringSegment result = MSBuildSpecification.Unescape(original);
        result.Should().Be(original);
    }

    [TestMethod]
    [DataRow("file%2A.txt", "file*.txt")]
    [DataRow("file%3F.txt", "file?.txt")]
    [DataRow("file%25.txt", "file%.txt")]
    [DataRow("file%20name.txt", "file name.txt")]
    [DataRow("file%invalidhex.txt", "file%invalidhex.txt")]
    [DataRow("file%2", "file%2")]
    [DataRow("file%", "file%")]
    public void Unescape_HandlesEscapeSequences(string escaped, string expected)
    {
        StringSegment result = MSBuildSpecification.Unescape(escaped);
        result.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void Normalize_NoSeparators_ReturnsOriginalSegment()
    {
        StringSegment segment = new("HelloWorld");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.Should().Be(segment);
    }

    [TestMethod]
    public void Normalize_SingleForwardSlash_NormalizesToPlatformSeparator()
    {
        StringSegment segment = new("Hello/World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_SingleBackslash_NormalizesToPlatformSeparator()
    {
        StringSegment segment = new(@"Hello\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_ConsecutiveForwardSlashes_CollapsesToSingle()
    {
        StringSegment segment = new("Hello////World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_ConsecutiveBackslashes_CollapsesToSingle()
    {
        StringSegment segment = new(@"Hello\\\\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_MixedSeparators_NormalizesCorrectly()
    {
        StringSegment segment = new(@"Hello\/\/\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_LeadingSeparator_Preserved()
    {
        StringSegment segment = new("/Hello/World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("/Hello/World"));
    }

    [TestMethod]
    public void Normalize_TrailingSeparator_Preserved()
    {
        StringSegment segment = new("Hello/World/");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World/"));
    }

    [TestMethod]
    public void Normalize_MultipleSeparatorsInPath_AllNormalized()
    {
        StringSegment segment = new("path/to/some/file.txt");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("path/to/some/file.txt"));
    }

    [TestMethod]
    public void Normalize_EmptySegment_ReturnsEmptySegment()
    {
        StringSegment segment = new("");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.IsEmpty.Should().BeTrue();
    }

    [TestMethod]
    public void Normalize_MixedConsecutiveSeparators()
    {
        StringSegment segment = new("Hello/\\//\\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);
        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_LeadingWhitespace_IsTrimmed()
    {
        StringSegment segment = new("  Hello/World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_TrailingWhitespace_IsTrimmed()
    {
        StringSegment segment = new("Hello/World  ");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_LeadingAndTrailingWhitespace_IsTrimmed()
    {
        StringSegment segment = new("  Hello/World  ");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [TestMethod]
    public void Normalize_WhitespaceBetweenSeparators_IsPreserved()
    {
        StringSegment segment = new("Hello/ World/Test");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/ World/Test"));
    }

    [TestMethod]
    public void Normalize_OnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   ");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.IsEmpty.Should().BeTrue();
    }

    [TestMethod]
    public void Normalize_WhitespaceAroundSeparators_IsPreserved()
    {
        StringSegment segment = new("Hello / World \\ Test");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello / World / Test"));
    }

    [TestMethod]
    public void Split_EmptyString_ReturnsEmptyList()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("", ignoreCase: false);
        specs.Count.Should().Be(0);
    }

    [TestMethod]
    public void Split_SingleSpec_ReturnsSingleItem()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt", ignoreCase: false);
        specs.Count.Should().Be(1);
        specs[0].Normalized.ToString().Should().Be("file.txt");
    }

    [TestMethod]
    public void Split_MultipleSpecs_ReturnsMultipleItems()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split(
            Paths.ChangeAlternateDirectorySeparators("file.txt;*.cs;docs/**"),
            ignoreCase: false);

        specs.Count.Should().Be(3);
        specs[0].Normalized.ToString().Should().Be("file.txt");
        specs[1].Normalized.ToString().Should().Be("*.cs");
        specs[2].Normalized.ToString().Should().Be(Paths.ChangeAlternateDirectorySeparators("docs/**"));
    }

    [TestMethod]
    public void Split_WithEmptySegments_IgnoresEmptySegments()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt;;*.cs;", ignoreCase: false);
        specs.Count.Should().Be(2);
        specs[0].Normalized.ToString().Should().Be("file.txt");
        specs[1].Normalized.ToString().Should().Be("*.cs");
    }

    [TestMethod]
    public void Split_WithDuplicates_SkipsDuplicates()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt;file.txt;*.cs", ignoreCase: false);
        specs.Count.Should().Be(2);
        specs[0].Normalized.ToString().Should().Be("file.txt");
        specs[1].Normalized.ToString().Should().Be("*.cs");
    }

    [TestMethod]
    public void Split_CaseInsensitive_SkipsCaseInsensitiveDuplicates()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt;FILE.txt;*.cs", ignoreCase: true);
        specs.Count.Should().Be(2);
        specs[0].Normalized.ToString().Should().Be("file.txt");
        specs[1].Normalized.ToString().Should().Be("*.cs");
    }

    [TestMethod]
    public void Split_EmptyInput_ReturnsEmptySegments()
    {
        StringSegment specs = new("");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);
        splitSpecs.Should().BeEmpty();
    }

    [TestMethod]
    public void Split_SingleWildcardSpec_ReturnsWildcardSpec()
    {
        StringSegment specs = new("*.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["*.txt"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_SingleLiteralSpec_ReturnsLiteralSpec()
    {
        StringSegment specs = new("file.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file.txt"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_MultipleWildcardSpecs_ReturnsFirstAndList()
    {
        StringSegment specs = new("*.txt;*.cs;*.md");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["*.txt", "*.cs", "*.md"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_MultipleLiteralSpecs_ReturnsFirstAndList()
    {
        StringSegment specs = new("file1.txt;file2.txt;file3.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file1.txt", "file2.txt", "file3.txt"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_MixedSpecs_SeparatesCorrectly()
    {
        StringSegment specs = new("file1.txt;*.cs;file2.txt;*.md");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file1.txt", "*.cs", "file2.txt", "*.md"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_DuplicateSpecs_DeduplicatesSpecs()
    {
        StringSegment specs = new("file.txt;file.txt;*.cs;*.cs");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file.txt", "*.cs"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_CaseSensitiveDuplicates_WithIgnoreCaseFalse_KeepsBoth()
    {
        StringSegment specs = new("FILE.TXT;file.txt;*.CS;*.cs");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["FILE.TXT", "file.txt", "*.CS", "*.cs"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_CaseSensitiveDuplicates_WithIgnoreCaseTrue_DeduplicatesSpecs()
    {
        StringSegment specs = new("FILE.TXT;file.txt;*.CS;*.cs");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: true);

        splitSpecs.Should().Equal(["FILE.TXT", "*.CS"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_DifferentSeparators_NormalizesSeparators()
    {
        StringSegment specs = new($"path/to/file.txt;path{Path.DirectorySeparatorChar}to{Path.DirectorySeparatorChar}other.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Select(s => s.ToString()).Should().Equal(
            [
                $"path{Path.DirectorySeparatorChar}to{Path.DirectorySeparatorChar}file.txt",
                $"path{Path.DirectorySeparatorChar}to{Path.DirectorySeparatorChar}other.txt"
            ]);

        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_ConsecutiveSeparators_CollapsesToSingle()
    {
        StringSegment specs = new("path//to////file.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Select(s => s.ToString()).Should().Equal([Sep("path/to/file.txt")]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_EmptySegments_SkipsEmptySegments()
    {
        StringSegment specs = new(";file.txt;;*.cs;");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file.txt", "*.cs"]);
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_OnlySemicolons_ReturnsEmptySegments()
    {
        StringSegment specs = new(";;;;");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().BeEmpty();
        splitSpecs.Dispose();
    }

    [TestMethod]
    public void Split_MultipleWildcardTypes()
    {
        StringSegment specs = new("file?.txt;dir*/file.txt;**/*.cs");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Select(s => s.ToString()).Should().Equal(
            [
                "file?.txt",
                $"dir*{Path.DirectorySeparatorChar}file.txt",
                $"**{Path.DirectorySeparatorChar}*.cs"
            ]);

        splitSpecs.Dispose();
    }


    public static IEnumerable<(string, bool, string[])> SplitData()
    {
        yield return ("file.txt;*.cs;docs/**",
            true,
            new[] { "file.txt", "*.cs", Sep("docs/**") });
        yield return ("file.txt;;*.cs;", true, new[] { "file.txt", "*.cs" });
        yield return ("file.txt;file.txt;*.cs", true, new[] { "file.txt", "*.cs" });
        yield return ("file.txt;FILE.txt;*.cs", true, new[] { "file.txt", "*.cs" });
        yield return ("", true, Array.Empty<string>());
        yield return (// This is the actual exclude spec for "**/*.cs" currently
            @"bin\Debug\/**;obj\Debug\/**;bin\/**;obj\/**;**/*.user;**/*.*proj;**/*.sln;**/*.slnx;**/*.vssscc;**/.DS_Store",
            true,
            new[]
            {
                Sep(@"bin/**"),
                Sep(@"obj/**"),
                Sep("**/*.user"),
                Sep("**/*.*proj"),
                Sep("**/*.sln"),
                Sep("**/*.slnx"),
                Sep("**/*.vssscc"),
                Sep("**/.DS_Store")
            });
        yield return (@"bin\/**;obj\/**;bin\Debug\/**;obj\Debug\/**;",
            true,
            new[]
            {
                Sep(@"bin/**"),
                Sep(@"obj/**"),
            });
    }

    [TestMethod]
    [DynamicData(nameof(SplitData))]
    public void Split_ReturnsExpectedResults(string input, bool ignoreCase, string[] expected)
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split(input, ignoreCase: ignoreCase);
        specs.Count.Should().Be(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            specs[i].Normalized.ToString().Should().Be(expected[i]);
        }
    }

    [TestMethod]
    public void SplitWithErrors_WhitespaceOnlySegment_ReturnsErrorResult()
    {
        // A whitespace-only segment normalizes to empty; the bare Split overload silently drops these
        // after throwing inside the constructor would have crashed callers. SplitWithErrors surfaces it
        // as an error result instead.
        ListBase<MSBuildSpecificationResult> results = MSBuildSpecification.SplitWithErrors(
            "  ;file.txt;\t;*.cs",
            ignoreCase: false);

        try
        {
            results.Count.Should().Be(4);

            results[0].IsError.Should().BeTrue();
            results[0].Original.ToString().Should().Be("  ");
            results[0].ErrorReason.Should().NotBeNullOrEmpty();
            results[0].Specification.Should().BeNull();

            results[1].IsError.Should().BeTrue();
            results[1].Original.ToString().Should().Be("\t");

            results[2].IsError.Should().BeFalse();
            results[2].Specification!.Normalized.ToString().Should().Be("file.txt");

            results[3].IsError.Should().BeFalse();
            results[3].Specification!.Normalized.ToString().Should().Be("*.cs");
        }
        finally
        {
            results.Dispose();
        }
    }

    [TestMethod]
    public void SplitWithErrors_NoErrors_ReturnsParsedResults()
    {
        ListBase<MSBuildSpecificationResult> results = MSBuildSpecification.SplitWithErrors(
            "file.txt;*.cs;docs/**",
            ignoreCase: false);

        try
        {
            results.Count.Should().Be(3);
            results.Should().OnlyContain(r => !r.IsError);
            results.Select(r => r.Specification!.Normalized.ToString()).Should().Equal(
                "file.txt",
                "*.cs",
                Sep("docs/**"));
        }
        finally
        {
            results.Dispose();
        }
    }

    [TestMethod]
    public void SplitWithErrors_EmptyInput_ReturnsEmptyList()
    {
        ListBase<MSBuildSpecificationResult> results = MSBuildSpecification.SplitWithErrors("", ignoreCase: false);
        try
        {
            results.Count.Should().Be(0);
        }
        finally
        {
            results.Dispose();
        }
    }

    [TestMethod]
    public void Split_WhitespaceOnlySegment_SilentlyDropped()
    {
        // Documents back-compat: the original Split overload continues to silently drop empty-normalize
        // segments (rather than throw, which the old code path did before SplitCore was factored out).
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("  ;file.txt;\t;*.cs", ignoreCase: false);
        try
        {
            specs.Count.Should().Be(2);
            specs[0].Normalized.ToString().Should().Be("file.txt");
            specs[1].Normalized.ToString().Should().Be("*.cs");
        }
        finally
        {
            specs.Dispose();
        }
    }

    [TestMethod]
    [DataRow("foo\0bar")]                 // embedded null character
    [DataRow("foo\0/bar.cs")]
    public void SplitWithErrors_NullCharacter_ReturnsErrorResult(string spec)
    {
        ListBase<MSBuildSpecificationResult> results = MSBuildSpecification.SplitWithErrors(spec, ignoreCase: false);
        try
        {
            results.Count.Should().Be(1);
            results[0].IsError.Should().BeTrue();
            results[0].ErrorReason.Should().Contain("null");
        }
        finally
        {
            results.Dispose();
        }
    }

    [TestMethod]
    [DataRow("foo.../bar.cs")]
    [DataRow(".../bar.cs")]
    [DataRow("foo/....cs")]
    public void SplitWithErrors_TripleDotSequence_ReturnsErrorResult(string spec)
    {
        ListBase<MSBuildSpecificationResult> results = MSBuildSpecification.SplitWithErrors(spec, ignoreCase: false);
        try
        {
            results.Count.Should().Be(1);
            results[0].IsError.Should().BeTrue();
            results[0].ErrorReason.Should().Contain("...");
        }
        finally
        {
            results.Dispose();
        }
    }

    [TestMethod]
    [DataRow("a**b")]                     // ** between non-separator chars
    [DataRow("foo/a**b/baz")]             // ** between non-separator chars in a path segment
    [DataRow("foo/**bar")]                // ** with no separator to the right
    [DataRow("foo/bar**")]                // ** at end with no separator to the left
    [DataRow("*.cs**")]                   // trailing ** glued to a filename
    public void SplitWithErrors_MisplacedDoubleStar_ReturnsErrorResult(string spec)
    {
        ListBase<MSBuildSpecificationResult> results = MSBuildSpecification.SplitWithErrors(spec, ignoreCase: false);
        try
        {
            results.Count.Should().Be(1);
            results[0].IsError.Should().BeTrue();
            results[0].ErrorReason.Should().Contain("**");
        }
        finally
        {
            results.Dispose();
        }
    }

    [TestMethod]
    [DataRow("**")]                       // standalone
    [DataRow("**/foo")]                   // recursive followed by separator
    [DataRow("foo/**")]                   // recursive at end after separator
    [DataRow("foo/**/bar")]               // recursive between separators
    [DataRow("foo/**/")]                  // recursive followed by trailing separator
    public void SplitWithErrors_LegalDoubleStar_ReturnsParsedResult(string spec)
    {
        ListBase<MSBuildSpecificationResult> results = MSBuildSpecification.SplitWithErrors(spec, ignoreCase: false);
        try
        {
            results.Count.Should().Be(1);
            results[0].IsError.Should().BeFalse();
        }
        finally
        {
            results.Dispose();
        }
    }

    [TestMethod]
    [DataRow("foo\0bar")]
    [DataRow("foo.../bar.cs")]
    [DataRow("a**b")]
    [DataRow("   ")]                      // normalizes to empty
    public void NormalizeAndValidate_IllegalSpec_ReturnsErrorReason(string spec)
    {
        _ = MSBuildSpecification.NormalizeAndValidate(spec, out string? error);
        error.Should().NotBeNullOrEmpty();
    }

    [TestMethod]
    [DataRow("file.txt")]
    [DataRow("**")]
    [DataRow("**/*.cs")]
    [DataRow("foo/**/bar")]
    [DataRow("./foo/bar")]
    [DataRow("../foo/bar")]
    public void NormalizeAndValidate_LegalSpec_ReturnsNullReason(string spec)
    {
        StringSegment normalized = MSBuildSpecification.NormalizeAndValidate(spec, out string? error);
        error.Should().BeNull();
        normalized.IsEmpty.Should().BeFalse();
    }

    [TestMethod]
    public void UnescapeSegment_NoEscapeCharacters_ReturnsSameSegment()
    {
        string original = "HelloWorld";
        StringSegment segment = new(original);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.ToString().Should().BeSameAs(original);
    }

    [TestMethod]
    public void UnescapeSegment_ValidEscapeSequences_Unescapes()
    {
        StringSegment segment = new("Hello%20World%2A%3F%25");
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be("Hello World*?%");
    }

    [TestMethod]
    public void UnescapeSegment_InvalidEscapeSequences_LeftAsIs()
    {
        string original = "Hello%2World%G%";
        StringSegment segment = new(original);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.ToString().Should().BeSameAs(original);
    }

    [TestMethod]
    public void UnescapeSegment_EmptyString_ReturnsEmptySegment()
    {
        StringSegment segment = new(string.Empty);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.IsEmpty.Should().BeTrue();
    }

    [TestMethod]
    public void UnescapeSegment_IncompleteEscapeSequence_PreservesOriginal()
    {
        string original = "Hello%World";
        StringSegment segment = new(original);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.ToString().Should().BeSameAs(original);
    }

    [TestMethod]
    public void UnescapeSegment_EscapeSequencesAtBoundaries_Unescapes()
    {
        StringSegment segment = new("%20Hello%20");
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be(" Hello ");
    }

    [TestMethod]
    public void UnescapeSegment_EscapeSequenceWithControlCharacters_Unescapes()
    {
        StringSegment segment = new("Hello%0AWorld%09Tab");
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be("Hello\nWorld\tTab");
    }

    [TestMethod]
    public void UnescapeSegment_LongSegmentWithEscapes_Unescapes()
    {
        // Create a segment longer than 256 chars with escapes to test buffer handling
        string longString = new string('a', 250) + "%20%25%2A";
        StringSegment segment = new(longString);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be(new string('a', 250) + " %*");
    }


    [TestMethod]
    public void Equals_SameOriginalString_ReturnsTrue()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("file.txt");

        spec1.Equals(spec2).Should().BeTrue();
        spec1.Equals("file.txt").Should().BeTrue();
        spec1.Equals(new StringSegment("file.txt")).Should().BeTrue();
    }

    [TestMethod]
    public void Equals_DifferentCase_ReturnsTrue()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("FILE.TXT");

        spec1.Equals(spec2).Should().BeTrue();
        spec1.Equals("FILE.TXT").Should().BeTrue();
    }

    [TestMethod]
    public void Equals_DifferentPaths_ReturnsFalse()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("file.cs");

        spec1.Equals(spec2).Should().BeFalse();
        spec1.Equals("file.cs").Should().BeFalse();
    }

    [TestMethod]
    public void GetHashCode_SameOriginalString_ReturnsSameHashCode()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("file.txt");

        spec1.GetHashCode().Should().Be(spec2.GetHashCode());
    }

    [TestMethod]
    public void ImplicitConversions_WorkAsExpected()
    {
        MSBuildSpecification specFromString = "file.txt";
        specFromString.Original.ToString().Should().Be("file.txt");

        MSBuildSpecification specFromSegment = new StringSegment("file.txt");
        specFromSegment.Original.ToString().Should().Be("file.txt");

        StringSegment segmentFromSpec = new MSBuildSpecification("file.txt");
        segmentFromSpec.ToString().Should().Be("file.txt");

        string stringFromSpec = (string)new MSBuildSpecification("file.txt");
        stringFromSpec.Should().Be("file.txt");
    }

    // ---- Equals branch coverage ----

    [TestMethod]
    public void Equals_Object_MSBuildSpecification_True()
    {
        object spec = new MSBuildSpecification("file.txt");
        new MSBuildSpecification("FILE.txt").Equals(spec).Should().BeTrue();
    }

    [TestMethod]
    public void Equals_Object_String_True()
    {
        object str = "FILE.txt";
        new MSBuildSpecification("file.txt").Equals(str).Should().BeTrue();
    }

    [TestMethod]
    public void Equals_Object_Null_False()
    {
        new MSBuildSpecification("file.txt").Equals((object?)null).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_Object_OtherType_False()
    {
        new MSBuildSpecification("file.txt").Equals(42).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_String_NullString_False()
    {
        new MSBuildSpecification("file.txt").Equals((string?)null).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_String_DifferentCase_True()
    {
        new MSBuildSpecification("file.txt").Equals("FILE.TXT").Should().BeTrue();
    }

    [TestMethod]
    public void Equals_MSBuildSpecification_Null_False()
    {
        new MSBuildSpecification("file.txt").Equals((MSBuildSpecification?)null).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_MSBuildSpecification_DifferentCase_True()
    {
        new MSBuildSpecification("file.txt").Equals(new MSBuildSpecification("FILE.TXT")).Should().BeTrue();
    }
}
