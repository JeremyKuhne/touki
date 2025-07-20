// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Collections;

namespace Touki.Io;

public class MSBuildSpecificationTests
{
    private static string Sep(string path) => Paths.ChangeAlternateDirectorySeparators(path);

    [Theory]
    [InlineData("file.txt", "", "", "file.txt", false)]
    [InlineData("**", "", "**", "*", true)]
    [InlineData("*.txt", "", "", "*.txt", true)]
    [InlineData("a/b/file.txt", "a/b", "", "file.txt", false)]
    [InlineData("a/b/*.txt", "a/b", "", "*.txt", true)]
    [InlineData("a/*/file.txt", "a", "*", "file.txt", true)]
    [InlineData("a/**/file.txt", "a", "**", "file.txt", true)]
    [InlineData("a/?/file.txt", "a", "?", "file.txt", true)]
    [InlineData("*/file.txt", "", "*", "file.txt", true)]
    [InlineData("**/file.txt", "", "**", "file.txt", true)]
    [InlineData("a/b/**", "a/b", "**", "*", true)]
    [InlineData("**/b/**", "", "**/b/**", "*", true)]
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

    [Theory]
    [InlineData("a\\b\\file.txt", "a/b/file.txt")]
    [InlineData("a/b\\file.txt", "a/b/file.txt")]
    [InlineData("\\a\\b\\file.txt", "/a/b/file.txt")]
    public void MSBuildSpecification_NormalizesPaths(string original, string expected)
    {
        expected = expected.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        MSBuildSpecification spec = new(original);
        spec.Normalized.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("a/**/**/b", "a/**/b")]
    [InlineData("a/**/**/**/b", "a/**/b")]
    [InlineData("**/**", "**")]
    [InlineData("**/**/", "**/")]
    [InlineData("a/**/**", "a/**")]
    [InlineData("a/**/**/", "a/**/")]
    [InlineData("/a/**/**/b", "/a/**/b")]
    [InlineData("/a/**/**/b/", "/a/**/b/")]
    [InlineData("a\\**\\**\\b", "a/**/b")]
    [InlineData("a/**\\**/b", "a/**/b")]
    [InlineData("a/**/c/**/b", "a/**/c/**/b")]
    public void MSBuildSpecification_NormalizesDuplicateRecursiveWildcards(string original, string expected)
    {
        expected = expected.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        MSBuildSpecification spec = new(original);
        spec.Normalized.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("file.txt", "file.txt")]
    [InlineData("a/b/c.txt", "a/b/c.txt")]
    public void MSBuildSpecification_ToString_ReturnsNormalized(string original, string expected)
    {
        expected = expected.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        MSBuildSpecification spec = new(original);
        spec.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("./file.txt", "", "", "file.txt", false)]
    [InlineData("../file.txt", "..", "", "file.txt", false)]
    [InlineData("a/./file.txt", "a", "", "file.txt", false)]
    [InlineData("a/../file.txt", "", "", "file.txt", false)]
    [InlineData("a/b/../../file.txt", "", "", "file.txt", false)]
    [InlineData("a/./b/c/file.txt", "a/b/c", "", "file.txt", false)]
    [InlineData("a/b/c/./../file.txt", "a/b", "", "file.txt", false)]
    [InlineData("a//b///file.txt", "a/b", "", "file.txt", false)]
    // It is unclear what to really do in the next case
    [InlineData("a/b/file.txt/", "a/b/file.txt", "", "", false)]
    [InlineData("a/b/./", "a/b", "", "", false)]
    [InlineData("./*/file.txt", "", "*", "file.txt", true)]
    [InlineData("../*/file.txt", "..", "*", "file.txt", true)]
    [InlineData("a/./**/file.txt", "a", "**", "file.txt", true)]
    [InlineData("a/../**/file.txt", "", "**", "file.txt", true)]
    [InlineData("a/b/../../*/file.txt", "", "*", "file.txt", true)]
    [InlineData("a/./b/*/c/*.txt", "a/b", "*/c", "*.txt", true)]
    [InlineData("a//b//*/file.txt", "a/b", "*", "file.txt", true)]
    [InlineData("a/b/**/", "a/b", "**", "*", true)]
    [InlineData("./a/../**", "", "**", "*", true)]
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

    [Theory]
    [InlineData("C:/path/file.txt", true, false)]
    [InlineData("/absolute/path/file.txt", false, false)]
    [InlineData("//server/share/file.txt", true, false)]
    [InlineData("relative/path/file.txt", false, true)]
    [InlineData("./relative/path/file.txt", false, true)]
    [InlineData("../parent/path/file.txt", false, false)]
    [InlineData("C:relative/file.txt", false, false)] // Drive-relative path
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

    [Theory]
    [InlineData("file.txt", false, true)]
    [InlineData("folder/file.txt", false, true)]
    [InlineData("../file.txt", false, false)]
    [InlineData("../../file.txt", false, false)]
    [InlineData("folder/../file.txt", false, true)] // Normalized doesn't contain '..'
    [InlineData("C:/folder/file.txt", true, false)]
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

    [Fact]
    public void IsNestedRelative_ReturnsTrue_ForPathsBelow_CurrentDirectory()
    {
        // These paths are guaranteed to be below the current directory
        new MSBuildSpecification("file.txt").IsNestedRelative.Should().BeTrue();
        new MSBuildSpecification("./file.txt").IsNestedRelative.Should().BeTrue();
        new MSBuildSpecification("folder/file.txt").IsNestedRelative.Should().BeTrue();
    }

    [Fact]
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

    [Theory]
    [InlineData("C:/file.txt", true)]
    [InlineData("C:\\file.txt", true)]
    [InlineData("\\\\server\\share\\file.txt", true)]
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
    [Theory]
    [InlineData("/root/file.txt", true)]
    [InlineData("~/file.txt", false)] // Not fully qualified, needs expansion
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

    [Theory]
    [InlineData("**", true)]
    [InlineData("**/", true)]
    [InlineData("/**/", true)]
    [InlineData("a/**", true)]
    [InlineData("a/**/", true)]
    [InlineData("a/b/**", true)]
    [InlineData("**/file.txt", true)]
    [InlineData("a/**/file.txt", true)]
    [InlineData("a/*/file.txt", false)]
    [InlineData("a/?/file.txt", false)]
    [InlineData("a/?/**/file.txt", false)]
    [InlineData("a/b/*.txt", false)]
    [InlineData("a/b/**/c", true)]
    public void SimpleRecursiveMatch_IsSetCorrectly(string pattern, bool expected)
    {
        MSBuildSpecification spec = new(pattern);
        spec.IsSimpleRecursiveMatch.Should().Be(expected);
    }

    [Fact]
    public void Unescape_ReturnsOriginalSegment_WhenNoEscapeCharacters()
    {
        StringSegment original = "file.txt";
        StringSegment result = MSBuildSpecification.Unescape(original);
        result.Should().Be(original);
    }

    [Theory]
    [InlineData("file%2A.txt", "file*.txt")]
    [InlineData("file%3F.txt", "file?.txt")]
    [InlineData("file%25.txt", "file%.txt")]
    [InlineData("file%20name.txt", "file name.txt")]
    [InlineData("file%invalidhex.txt", "file%invalidhex.txt")]
    [InlineData("file%2", "file%2")]
    [InlineData("file%", "file%")]
    public void Unescape_HandlesEscapeSequences(string escaped, string expected)
    {
        StringSegment result = MSBuildSpecification.Unescape(escaped);
        result.ToString().Should().Be(expected);
    }

    [Fact]
    public void Normalize_NoSeparators_ReturnsOriginalSegment()
    {
        StringSegment segment = new("HelloWorld");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.Should().Be(segment);
    }

    [Fact]
    public void Normalize_SingleForwardSlash_NormalizesToPlatformSeparator()
    {
        StringSegment segment = new("Hello/World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_SingleBackslash_NormalizesToPlatformSeparator()
    {
        StringSegment segment = new(@"Hello\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_ConsecutiveForwardSlashes_CollapsesToSingle()
    {
        StringSegment segment = new("Hello////World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_ConsecutiveBackslashes_CollapsesToSingle()
    {
        StringSegment segment = new(@"Hello\\\\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_MixedSeparators_NormalizesCorrectly()
    {
        StringSegment segment = new(@"Hello\/\/\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_LeadingSeparator_Preserved()
    {
        StringSegment segment = new("/Hello/World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("/Hello/World"));
    }

    [Fact]
    public void Normalize_TrailingSeparator_Preserved()
    {
        StringSegment segment = new("Hello/World/");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World/"));
    }

    [Fact]
    public void Normalize_MultipleSeparatorsInPath_AllNormalized()
    {
        StringSegment segment = new("path/to/some/file.txt");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("path/to/some/file.txt"));
    }

    [Fact]
    public void Normalize_EmptySegment_ReturnsEmptySegment()
    {
        StringSegment segment = new("");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Normalize_MixedConsecutiveSeparators()
    {
        StringSegment segment = new("Hello/\\//\\World");
        StringSegment result = MSBuildSpecification.Normalize(segment);
        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_LeadingWhitespace_IsTrimmed()
    {
        StringSegment segment = new("  Hello/World");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_TrailingWhitespace_IsTrimmed()
    {
        StringSegment segment = new("Hello/World  ");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_LeadingAndTrailingWhitespace_IsTrimmed()
    {
        StringSegment segment = new("  Hello/World  ");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/World"));
    }

    [Fact]
    public void Normalize_WhitespaceBetweenSeparators_IsPreserved()
    {
        StringSegment segment = new("Hello/ World/Test");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello/ World/Test"));
    }

    [Fact]
    public void Normalize_OnlyWhitespace_ReturnsEmptySegment()
    {
        StringSegment segment = new("   ");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Normalize_WhitespaceAroundSeparators_IsPreserved()
    {
        StringSegment segment = new("Hello / World \\ Test");
        StringSegment result = MSBuildSpecification.Normalize(segment);

        result.ToString().Should().Be(Sep("Hello / World / Test"));
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmptyList()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("", ignoreCase: false);
        specs.Count.Should().Be(0);
    }

    [Fact]
    public void Split_SingleSpec_ReturnsSingleItem()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt", ignoreCase: false);
        specs.Count.Should().Be(1);
        specs[0].Normalized.ToString().Should().Be("file.txt");
    }

    [Fact]
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

    [Fact]
    public void Split_WithEmptySegments_IgnoresEmptySegments()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt;;*.cs;", ignoreCase: false);
        specs.Count.Should().Be(2);
        specs[0].Normalized.ToString().Should().Be("file.txt");
        specs[1].Normalized.ToString().Should().Be("*.cs");
    }

    [Fact]
    public void Split_WithDuplicates_SkipsDuplicates()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt;file.txt;*.cs", ignoreCase: false);
        specs.Count.Should().Be(2);
        specs[0].Normalized.ToString().Should().Be("file.txt");
        specs[1].Normalized.ToString().Should().Be("*.cs");
    }

    [Fact]
    public void Split_CaseInsensitive_SkipsCaseInsensitiveDuplicates()
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split("file.txt;FILE.txt;*.cs", ignoreCase: true);
        specs.Count.Should().Be(2);
        specs[0].Normalized.ToString().Should().Be("file.txt");
        specs[1].Normalized.ToString().Should().Be("*.cs");
    }

    [Fact]
    public void Split_EmptyInput_ReturnsEmptySegments()
    {
        StringSegment specs = new("");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);
        splitSpecs.Should().BeEmpty();
    }

    [Fact]
    public void Split_SingleWildcardSpec_ReturnsWildcardSpec()
    {
        StringSegment specs = new("*.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["*.txt"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_SingleLiteralSpec_ReturnsLiteralSpec()
    {
        StringSegment specs = new("file.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file.txt"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_MultipleWildcardSpecs_ReturnsFirstAndList()
    {
        StringSegment specs = new("*.txt;*.cs;*.md");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["*.txt", "*.cs", "*.md"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_MultipleLiteralSpecs_ReturnsFirstAndList()
    {
        StringSegment specs = new("file1.txt;file2.txt;file3.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file1.txt", "file2.txt", "file3.txt"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_MixedSpecs_SeparatesCorrectly()
    {
        StringSegment specs = new("file1.txt;*.cs;file2.txt;*.md");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file1.txt", "*.cs", "file2.txt", "*.md"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_DuplicateSpecs_DeduplicatesSpecs()
    {
        StringSegment specs = new("file.txt;file.txt;*.cs;*.cs");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file.txt", "*.cs"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_CaseSensitiveDuplicates_WithIgnoreCaseFalse_KeepsBoth()
    {
        StringSegment specs = new("FILE.TXT;file.txt;*.CS;*.cs");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["FILE.TXT", "file.txt", "*.CS", "*.cs"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_CaseSensitiveDuplicates_WithIgnoreCaseTrue_DeduplicatesSpecs()
    {
        StringSegment specs = new("FILE.TXT;file.txt;*.CS;*.cs");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: true);

        splitSpecs.Should().Equal(["FILE.TXT", "*.CS"]);
        splitSpecs.Dispose();
    }

    [Fact]
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

    [Fact]
    public void Split_ConsecutiveSeparators_CollapsesToSingle()
    {
        StringSegment specs = new("path//to////file.txt");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Select(s => s.ToString()).Should().Equal([Sep("path/to/file.txt")]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_EmptySegments_SkipsEmptySegments()
    {
        StringSegment specs = new(";file.txt;;*.cs;");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().Equal(["file.txt", "*.cs"]);
        splitSpecs.Dispose();
    }

    [Fact]
    public void Split_OnlySemicolons_ReturnsEmptySegments()
    {
        StringSegment specs = new(";;;;");
        var splitSpecs = MSBuildSpecification.Split(specs, ignoreCase: false);

        splitSpecs.Should().BeEmpty();
        splitSpecs.Dispose();
    }

    [Fact]
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


    public static TheoryData<string, bool, string[]> SplitData { get; } = new()
    {
        {
            "file.txt;*.cs;docs/**",
            true,
            new[] { "file.txt", "*.cs", Sep("docs/**") }
        },
        { "file.txt;;*.cs;", true, new[] { "file.txt", "*.cs" } },
        { "file.txt;file.txt;*.cs", true, new[] { "file.txt", "*.cs" } },
        { "file.txt;FILE.txt;*.cs", true, new[] { "file.txt", "*.cs" } },
        { "", true, Array.Empty<string>() },
        {
            // This is the actual exclude spec for "**/*.cs" currently
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
            }
        },
        {
            @"bin\/**;obj\/**;bin\Debug\/**;obj\Debug\/**;",
            true,
            new[]
            {
                Sep(@"bin/**"),
                Sep(@"obj/**"),
            }
        }
    };

    [Theory]
    [MemberData(nameof(SplitData))]
    public void Split_ReturnsExpectedResults(string input, bool ignoreCase, string[] expected)
    {
        ListBase<MSBuildSpecification> specs = MSBuildSpecification.Split(input, ignoreCase: ignoreCase);
        specs.Count.Should().Be(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            specs[i].Normalized.ToString().Should().Be(expected[i]);
        }
    }

    [Fact]
    public void UnescapeSegment_NoEscapeCharacters_ReturnsSameSegment()
    {
        string original = "HelloWorld";
        StringSegment segment = new(original);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void UnescapeSegment_ValidEscapeSequences_Unescapes()
    {
        StringSegment segment = new("Hello%20World%2A%3F%25");
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be("Hello World*?%");
    }

    [Fact]
    public void UnescapeSegment_InvalidEscapeSequences_LeftAsIs()
    {
        string original = "Hello%2World%G%";
        StringSegment segment = new(original);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void UnescapeSegment_EmptyString_ReturnsEmptySegment()
    {
        StringSegment segment = new(string.Empty);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void UnescapeSegment_IncompleteEscapeSequence_PreservesOriginal()
    {
        string original = "Hello%World";
        StringSegment segment = new(original);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.ToString().Should().BeSameAs(original);
    }

    [Fact]
    public void UnescapeSegment_EscapeSequencesAtBoundaries_Unescapes()
    {
        StringSegment segment = new("%20Hello%20");
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be(" Hello ");
    }

    [Fact]
    public void UnescapeSegment_EscapeSequenceWithControlCharacters_Unescapes()
    {
        StringSegment segment = new("Hello%0AWorld%09Tab");
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be("Hello\nWorld\tTab");
    }

    [Fact]
    public void UnescapeSegment_LongSegmentWithEscapes_Unescapes()
    {
        // Create a segment longer than 256 chars with escapes to test buffer handling
        string longString = new string('a', 250) + "%20%25%2A";
        StringSegment segment = new(longString);
        StringSegment result = MSBuildSpecification.Unescape(segment);

        result.Should().Be(new string('a', 250) + " %*");
    }


    [Fact]
    public void Equals_SameOriginalString_ReturnsTrue()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("file.txt");

        spec1.Equals(spec2).Should().BeTrue();
        spec1.Equals("file.txt").Should().BeTrue();
        spec1.Equals(new StringSegment("file.txt")).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentCase_ReturnsTrue()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("FILE.TXT");

        spec1.Equals(spec2).Should().BeTrue();
        spec1.Equals("FILE.TXT").Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentPaths_ReturnsFalse()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("file.cs");

        spec1.Equals(spec2).Should().BeFalse();
        spec1.Equals("file.cs").Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameOriginalString_ReturnsSameHashCode()
    {
        MSBuildSpecification spec1 = new("file.txt");
        MSBuildSpecification spec2 = new("file.txt");

        spec1.GetHashCode().Should().Be(spec2.GetHashCode());
    }

    [Fact]
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
}
