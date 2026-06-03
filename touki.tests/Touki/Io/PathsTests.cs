// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki.Io;

[TestClass]
public class PathsTests
{
    private static readonly char s_separator = Path.DirectorySeparatorChar;

    [TestMethod]
    [DataRow("foo", "foo", MatchCasing.CaseSensitive, false)]
    [DataRow("foo", "FOO", MatchCasing.CaseSensitive, true)]
    [DataRow("foo", "FOO", MatchCasing.CaseInsensitive, false)]
    [DataRow("", "", MatchCasing.CaseSensitive, false)]
    [DataRow("a", "b", MatchCasing.CaseSensitive, true)]
    public void ArePatternsExclusive_LiteralVsLiteral_RespectsCasing(string p1, string p2, MatchCasing casing, bool expected)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, casing).Should().Be(expected);
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, casing).Should().Be(expected);
    }

    [TestMethod]
    [DataRow("foo", "f*", false)]
    [DataRow("foo", "b*", true)]
    [DataRow("bar", "?ar", false)]
    [DataRow("bar", "?az", true)]
    public void ArePatternsExclusive_LiteralVsWildcard_Basic(string literal, string wildcard, bool expected)
    {
        // Simple matching, explicit case-insensitive and case-sensitive should give same exclusivity for ASCII inputs
        Paths.AreExpressionsExclusive(literal, wildcard, MatchType.Simple, MatchCasing.CaseSensitive).Should().Be(expected);
        Paths.AreExpressionsExclusive(literal, wildcard, MatchType.Simple, MatchCasing.CaseInsensitive).Should().Be(expected);

        // Swap order to exercise the alternate branch
        Paths.AreExpressionsExclusive(wildcard, literal, MatchType.Simple, MatchCasing.CaseSensitive).Should().Be(expected);
    }

    [TestMethod]
    public void ArePatternsExclusive_StarAndWin32DotStarDotStar_NeverExclusive()
    {
        // '*' against anything is never exclusive
        Paths.AreExpressionsExclusive("*", "anything", MatchType.Simple, MatchCasing.CaseSensitive).Should().BeFalse();
        Paths.AreExpressionsExclusive("anything", "*", MatchType.Simple, MatchCasing.CaseSensitive).Should().BeFalse();

        // In Win32 semantics, '*.*' behaves like match-anything for exclusivity purposes
        Paths.AreExpressionsExclusive("*.cs", "*.*", MatchType.Win32, MatchCasing.CaseSensitive).Should().BeFalse();
        Paths.AreExpressionsExclusive("*.*", "*.cs", MatchType.Win32, MatchCasing.CaseSensitive).Should().BeFalse();
        Paths.AreExpressionsExclusive("*.*", "*.*", MatchType.Win32, MatchCasing.CaseSensitive).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("ab*cd", "ab*ef")] // incompatible fixed suffixes
    [DataRow("foo*", "bar*")]   // incompatible fixed prefixes
    [DataRow("*foo", "*bar")]   // incompatible fixed suffixes
    [DataRow("foo?", "bar?")]   // incompatible fixed prefixes with '?'
    public void ArePatternsExclusive_BothWildcards_ObviousExclusiveCases(string p1, string p2)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeTrue();
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("*abc*", "*def*")] // could overlap (e.g., "abcdef")
    [DataRow("*a*c*", "*b*d*")] // uncertain overlap; should not claim exclusive
    [DataRow("pre*mid*suf", "pre*X*suf")] // same fixed prefix/suffix; not provably exclusive
    public void ArePatternsExclusive_BothWildcards_OnlyProveObviousCases(string p1, string p2)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeFalse();
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeFalse();
    }

    [TestMethod]
    [DataRow("*.CS", "*.cs", MatchCasing.CaseSensitive, true)]
    [DataRow("*.CS", "*.cs", MatchCasing.CaseInsensitive, false)]
    public void ArePatternsExclusive_BothWildcards_RespectsCasing(string p1, string p2, MatchCasing casing, bool expected)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, casing).Should().Be(expected);
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, casing).Should().Be(expected);
    }

    [TestMethod]
    public void IsSameOrSubdirectory_SameDirectoryNoTrailingSeparator_ReturnsTrue()
    {
        Paths.IsSameOrSubdirectory("foo".AsSpan(), "foo".AsSpan(), ignoreCase: false).Should().BeTrue();
    }

    [TestMethod]
    public void IsSameOrSubdirectory_SameDirectoryFirstHasTrailingSeparator_ReturnsTrue()
    {
        string first = $"foo{s_separator}";
        Paths.IsSameOrSubdirectory(first.AsSpan(), "foo".AsSpan(), ignoreCase: false).Should().BeTrue();
    }

    [TestMethod]
    public void IsSameOrSubdirectory_Subdirectory_ReturnsTrue()
    {
        string second = $"foo{s_separator}bar";
        Paths.IsSameOrSubdirectory("foo".AsSpan(), second.AsSpan(), ignoreCase: false).Should().BeTrue();
    }

    [TestMethod]
    public void IsSameOrSubdirectory_PrefixNotDirectoryBoundary_ReturnsFalse()
    {
        Paths.IsSameOrSubdirectory("foo".AsSpan(), "foobar".AsSpan(), ignoreCase: false).Should().BeFalse();
    }

    [TestMethod]
    public void IsSameOrSubdirectory_CaseDifferenceIgnoredWhenIgnoringCase_ReturnsTrue()
    {
        string second = $"FOO{s_separator}BAR";
        Paths.IsSameOrSubdirectory("foo".AsSpan(), second.AsSpan(), ignoreCase: true).Should().BeTrue();
    }

    [TestMethod]
    public void IsSameOrSubdirectory_CaseDifferenceHonoredWhenNotIgnoringCase_ReturnsFalse()
    {
        string second = $"FOO{s_separator}BAR";
        Paths.IsSameOrSubdirectory("foo".AsSpan(), second.AsSpan(), ignoreCase: false).Should().BeFalse();
    }

    [TestMethod]
    public void IsSameOrSubdirectory_SecondHasTrailingSeparatorNotNormalized_ReturnsTrue()
    {
        string second = $"foo{s_separator}";
        Paths.IsSameOrSubdirectory("foo".AsSpan(), second.AsSpan(), ignoreCase: false).Should().BeTrue();
    }

    public static IEnumerable<(string, string, bool)> IsSameOrSubdirectoryEdgeCasesData()
    {
        yield return ("", "", true);
        yield return ("", "a", false);
        yield return ("", $"{s_separator}a", true);
        yield return ($"{s_separator}", $"{s_separator}", true);
        yield return ($"{s_separator}", $"{s_separator}child", true);
        yield return ($"{s_separator}", "a", false);
        yield return ($"{s_separator}", $"{s_separator}a", true);
        yield return ($"{s_separator}{s_separator}",$"{s_separator}", true);
        yield return (Paths.ChangeAlternateDirectorySeparators("/foo/bar/"),
            Paths.ChangeAlternateDirectorySeparators("/foo/barista"),
            false);
        yield return (Paths.ChangeAlternateDirectorySeparators("/foo/bar"),
            Paths.ChangeAlternateDirectorySeparators("/foo/barista"),
            false);
        yield return (Paths.ChangeAlternateDirectorySeparators("/foo/bar/"),
            Paths.ChangeAlternateDirectorySeparators("/foo/bar/ista"),
            true);
        yield return (Paths.ChangeAlternateDirectorySeparators("/foo/bar"),
            Paths.ChangeAlternateDirectorySeparators("/foo/bar/ista"),
            true);
    }

    [TestMethod]
    [DynamicData(nameof(IsSameOrSubdirectoryEdgeCasesData))]
    public void IsSameOrSubdirectory_EdgeCases_ReturnsExpected(string first, string second, bool expected)
    {
        Paths.IsSameOrSubdirectory(first.AsSpan(), second.AsSpan(), ignoreCase: true).Should().Be(expected);
    }

    public static IEnumerable<(string, string)> RemoveRelativeSegmentsNotFullyQualifiedData()
    {
        yield return (@"git\runtime",               @"git\runtime");
        yield return (@"git\\runtime",              @"git\runtime");
        yield return (@"git\\\runtime",             @"git\runtime");
        yield return (@"git\.\runtime\.\\",         @"git\runtime\");
        yield return (@"git\..\runtime",            @"runtime");
        yield return (@"git\runtime\..\",           @"git\");
        yield return (@"git\runtime\..\..\..\",     @"..\");
        yield return (@"git\runtime\..\..\.\",      @"");
        yield return (@"git\..\.\runtime\temp\..",  @"runtime\");
        yield return (@"git\..\\\.\..\runtime",     @"..\runtime");
        yield return (@"git\runtime\",              @"git\runtime\");
        yield return (@"git\temp\..\runtime\",      @"git\runtime\");
        yield return (@".\runtime",                 @"runtime");
        yield return (@".\\runtime",                @"runtime");
        yield return (@".\\\runtime",               @"runtime");
        yield return (@".\.\runtime\.\\",           @"runtime\");
        yield return (@".\..\runtime",              @"..\runtime");
        yield return (@".\runtime\..\",             @"");
        yield return (@".\runtime\..\..\..",        @"..\..");
        yield return (@".\runtime\..\..\.\",        @"..\");
        yield return (@".\..\.\runtime\temp\..",    @"..\runtime\");
        yield return (@".\..\\\.\..\runtime",       @"..\..\runtime");
        yield return (@".\runtime\",                @"runtime\");
        yield return (@".\temp\..\runtime\",        @"runtime\");
        yield return (@"C:A\.",                     @"C:A\");
        yield return (@"C:A\..",                    @"C:");
        yield return (@"C:A\..\..",                 @"C:..");
        yield return (@"C:A\..\..\..",              @"C:..\..");
    }

    [TestMethod,
        DynamicData(nameof(RemoveRelativeSegmentsNotFullyQualifiedData))]
    public void RemoveRelativeSegments_NotFullyQualified(string path, string expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        Paths.RemoveRelativeSegments(path).Should().Be(expected);

        // Validate that our assertions are correct.
        string currentDirectory = Environment.CurrentDirectory;
        string firstNormalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string secondNormalized = expected.Length == 0
            ? currentDirectory
            : Path.TrimEndingDirectorySeparator(Path.GetFullPath(expected));

        firstNormalized.Should().Be(secondNormalized);
    }

    public static IEnumerable<(string, string)> RemoveRelativeSegmentsData()
    {
        yield return (@"C:\git\runtime",                @"C:\git\runtime");
        yield return (@"C:\\git\runtime",               @"C:\git\runtime");
        yield return (@"C:\git\\runtime",               @"C:\git\runtime");
        yield return (@"C:\git\.\runtime\.\\",          @"C:\git\runtime\");
        yield return (@"C:\git\..\runtime",             @"C:\runtime");
        yield return (@"C:\git\runtime\..\",            @"C:\git\");
        yield return (@"C:\git\runtime\..\..\..\",      @"C:\");
        yield return (@"C:\git\runtime\..\..\.\",       @"C:\");
        yield return (@"C:\git\..\.\runtime\temp\..",   @"C:\runtime\");
        yield return (@"C:\git\..\\\.\..\runtime",      @"C:\runtime");
        yield return (@"C:\git\runtime\",               @"C:\git\runtime\");
        yield return (@"C:\git\temp\..\runtime\",       @"C:\git\runtime\");
        yield return (@"C:\.",                          @"C:\");
        yield return (@"C:\..",                         @"C:\");
        yield return (@"C:\..\..",                      @"C:\");
        yield return (@"C:\tmp\home",                   @"C:\tmp\home");
        yield return (@"C:\tmp\..",                     @"C:\");
        yield return (@"C:\tmp\home\..\.\.\",           @"C:\tmp\");
        yield return (@"C:\tmp\..\..\..\",              @"C:\");
        yield return (@"C:\tmp\\home",                  @"C:\tmp\home");
        yield return (@"C:\.\tmp\\home",                @"C:\tmp\home");
        yield return (@"C:\..\tmp\home",                @"C:\tmp\home");
        yield return (@"C:\..\..\..\tmp\.\home",        @"C:\tmp\home");
        yield return (@"C:\\tmp\\\home",                @"C:\tmp\home");
        yield return (@"C:\tmp\home\git\.\..\.\git\runtime\..\", @"C:\tmp\home\git\");
        yield return (@"C:\.\tmp\home",                 @"C:\tmp\home");
        yield return (@"C:\tmp\home\..\..\.\",          @"C:\");
        yield return (@"C:\tmp\..\..\",                 @"C:\");
        yield return (@"C:\tmp\\home\..\.\\",           @"C:\tmp\");
        yield return (@"C:\.\tmp\\home\git\git",        @"C:\tmp\home\git\git");
        yield return (@"C:\..\tmp\.\home",              @"C:\tmp\home");
        yield return (@"C:\\tmp\\\home\..",             @"C:\tmp\");
        yield return (@"C:\.\tmp\home\.\.\",            @"C:\tmp\home\");
    }

    public static IEnumerable<(string, string)> RemoveRelativeSegmentsFirstRelativeSegment()
    {
        yield return (@"C:\.\git\runtime",              @"C:\git\runtime");
        yield return (@"C:\\.\git\.\runtime",           @"C:\git\runtime");
        yield return (@"C:\..\git\runtime",             @"C:\git\runtime");
        yield return (@"C:\.\git\..\runtime",           @"C:\runtime");
        yield return (@"C:\.\git\runtime\..\",          @"C:\git\");
        yield return (@"C:\.\git\runtime\..\..\..\",    @"C:\");
        yield return (@"C:\.\git\runtime\..\..\.\",     @"C:\");
        yield return (@"C:\.\git\..\.\runtime\temp\..", @"C:\runtime\");
        yield return (@"C:\.\git\..\\\.\..\runtime",    @"C:\runtime");
        yield return (@"C:\.\git\runtime\",             @"C:\git\runtime\");
        yield return (@"C:\.\git\temp\..\runtime\",     @"C:\git\runtime\");
        yield return (@"C:\\..\..",                     @"C:\");
    }

    [TestMethod,
        DynamicData(nameof(RemoveRelativeSegmentsData)),
        DynamicData(nameof(RemoveRelativeSegmentsFirstRelativeSegment))]
    public void RemoveRelativeSegments(string path, string expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        Paths.RemoveRelativeSegments(path).ToString().Should().Be(expected);
        Paths.RemoveRelativeSegments(@"\\.\" + path).ToString().Should().Be(@"\\.\" + expected);
        Paths.RemoveRelativeSegments(@"\\?\" + path).ToString().Should().Be(@"\\?\" + expected);
    }

    public static IEnumerable<(string, string)> RemoveRelativeSegmentsUncData()
    {
        yield return (@"Server\Share\git\runtime",             @"Server\Share\git\runtime");
        yield return (@"Server\Share\\git\runtime",            @"Server\Share\git\runtime");
        yield return (@"Server\Share\git\\runtime",            @"Server\Share\git\runtime");
        yield return (@"Server\Share\git\.\runtime\.\\",       @"Server\Share\git\runtime\");
        yield return (@"Server\Share\git\..\runtime",          @"Server\Share\runtime");
        yield return (@"Server\Share\git\runtime\..\",         @"Server\Share\git\");
        yield return (@"Server\Share\git\runtime\..\..\..\",   @"Server\Share\");
        yield return (@"Server\Share\git\runtime\..\..\.\",    @"Server\Share\");
        yield return (@"Server\Share\git\..\.\runtime\temp\..", @"Server\Share\runtime\");
        yield return (@"Server\Share\git\..\\\.\..\runtime",   @"Server\Share\runtime");
        yield return (@"Server\Share\git\runtime\",            @"Server\Share\git\runtime\");
        yield return (@"Server\Share\git\temp\..\runtime\",    @"Server\Share\git\runtime\");
    }

    [TestMethod,
        DynamicData(nameof(RemoveRelativeSegmentsUncData))]
    public void RemoveRelativeSegments_Unc(string path, string expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        Paths.RemoveRelativeSegments(@"\\" + path).ToString().Should().Be(@"\\" + expected);
        Paths.RemoveRelativeSegments(@"\\.\UNC\" + path).ToString().Should().Be(@"\\.\UNC\" + expected);
        Paths.RemoveRelativeSegments(@"\\?\UNC\" + path).ToString().Should().Be(@"\\?\UNC\" + expected);
    }

    public static IEnumerable<(string, string)> RemoveRelativeSegmentsDeviceData()
    {
        yield return (@"\\.\git\runtime",                @"\\.\git\runtime");
        yield return (@"\\.\git\\runtime",               @"\\.\git\runtime");
        yield return (@"\\.\git\.\runtime\.\\",          @"\\.\git\runtime\");
        yield return (@"\\.\git\..\runtime",             @"\\.\git\runtime");
        yield return (@"\\.\git\runtime\..\",            @"\\.\git\");
        yield return (@"\\.\git\runtime\..\..\..\",      @"\\.\git\");
        yield return (@"\\.\git\runtime\..\..\.\",       @"\\.\git\");
        yield return (@"\\.\git\..\.\runtime\temp\..",   @"\\.\git\runtime\");
        yield return (@"\\.\git\..\\\.\..\runtime",      @"\\.\git\runtime");
        yield return (@"\\.\git\runtime\",               @"\\.\git\runtime\");
        yield return (@"\\.\git\temp\..\runtime\",       @"\\.\git\runtime\");
        yield return (@"\\.\.\runtime",                  @"\\.\.\runtime");
        yield return (@"\\.\.\\runtime",                 @"\\.\.\runtime");
        yield return (@"\\.\.\.\runtime\.\\",            @"\\.\.\runtime\");
        yield return (@"\\.\.\..\runtime",               @"\\.\.\runtime");
        yield return (@"\\.\.\runtime\..\",              @"\\.\.\");
        yield return (@"\\.\.\runtime\..\..\..\",        @"\\.\.\");
        yield return (@"\\.\.\runtime\..\..\.\",         @"\\.\.\");
        yield return (@"\\.\.\..\.\runtime\temp\..",     @"\\.\.\runtime\");
        yield return (@"\\.\.\..\\\.\..\runtime",        @"\\.\.\runtime");
        yield return (@"\\.\.\runtime\",                 @"\\.\.\runtime\");
        yield return (@"\\.\.\temp\..\runtime\",         @"\\.\.\runtime\");
        yield return (@"\\.\..\runtime",                 @"\\.\..\runtime");
        yield return (@"\\.\..\\runtime",                @"\\.\..\runtime");
        yield return (@"\\.\..\.\runtime\.\\",           @"\\.\..\runtime\");
        yield return (@"\\.\..\..\runtime",              @"\\.\..\runtime");
        yield return (@"\\.\..\runtime\..\",             @"\\.\..\");
        yield return (@"\\.\..\runtime\..\..\..\",       @"\\.\..\");
        yield return (@"\\.\..\runtime\..\..\.\",        @"\\.\..\");
        yield return (@"\\.\..\..\.\runtime\temp\..",    @"\\.\..\runtime\");
        yield return (@"\\.\..\..\\\.\..\runtime",       @"\\.\..\runtime");
        yield return (@"\\.\..\runtime\",                @"\\.\..\runtime\");
        yield return (@"\\.\..\temp\..\runtime\",        @"\\.\..\runtime\");
        yield return (@"\\.\\runtime",                   @"\\.\runtime");
        yield return (@"\\.\\\runtime",                  @"\\.\runtime");
        yield return (@"\\.\\.\runtime\.\\",             @"\\.\runtime\");
        yield return (@"\\.\\..\runtime",                @"\\.\runtime");
        yield return (@"\\.\\runtime\..\",               @"\\.\");
        yield return (@"\\.\\runtime\..\..\..\",         @"\\.\");
        yield return (@"\\.\\runtime\..\..\.\",          @"\\.\");
        yield return (@"\\.\\..\.\runtime\temp\..",      @"\\.\runtime\");
        yield return (@"\\.\\..\\\.\..\runtime",         @"\\.\runtime");
        yield return (@"\\.\\runtime\",                  @"\\.\runtime\");
        yield return (@"\\.\\temp\..\runtime\",          @"\\.\runtime\");
    }

    [TestMethod,
        DynamicData(nameof(RemoveRelativeSegmentsDeviceData))]
    public void RemoveRelativeSegments_Device(string path, string expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        Paths.RemoveRelativeSegments(path).ToString().Should().Be(expected);
        StringBuilder sb = new StringBuilder(expected);
        sb.Replace('.', '?', 0, 4);
        expected = sb.ToString();

        sb = new StringBuilder(path);
        sb.Replace('.', '?', 0, 4);
        path = sb.ToString();
        Paths.RemoveRelativeSegments(path).ToString().Should().Be(expected);
    }

    public static IEnumerable<(string, string)> RemoveRelativeSegmentUnixData()
    {
        yield return ("/tmp/home",                          "/tmp/home");
        yield return ("/tmp/..",                            "/");
        yield return ("/tmp/home/../././",                  "/tmp/");
        yield return ("/tmp/../../../",                     "/");
        yield return ("/tmp//home",                         "/tmp/home");
        yield return ("/./tmp//home",                       "/tmp/home");
        yield return ("/../tmp/home",                       "/tmp/home");
        yield return ("/../../../tmp/./home",               "/tmp/home");
        yield return ("//tmp///home",                       "/tmp/home");
        yield return ("/tmp/home/git/./.././git/runtime/../", "/tmp/home/git/");
        yield return ("/./tmp/home",                        "/tmp/home");
    }

#if NET
    [TestMethod,
        DynamicData(nameof(RemoveRelativeSegmentUnixData))]
    public void RemoveRelativeSegments_Unix(string path, string expected)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Paths.RemoveRelativeSegments(path).ToString().Should().Be(expected);
    }
#endif

    [TestMethod]
    public void MatchesExpression_Win32_UsesWin32Semantics()
    {
        // Win32 treats '*.*' as matching anything that has a dot in it (and more).
        Paths.MatchesExpression("file.txt".AsSpan(), "*.*".AsSpan(), MatchCasing.CaseSensitive, MatchType.Win32)
            .Should().BeTrue();
        // Exercising MatchType.Win32 path; behavior follows FileSystemName.MatchesWin32Expression.
        Paths.MatchesExpression("README".AsSpan(), "READ*".AsSpan(), MatchCasing.CaseInsensitive, MatchType.Win32)
            .Should().BeTrue();
    }

    [TestMethod]
    public void MatchesExpression_Simple_RequiresLiteralDot()
    {
        // Simple semantics treat '*.*' as needing a literal '.'.
        Paths.MatchesExpression("file.txt".AsSpan(), "*.*".AsSpan(), MatchCasing.CaseSensitive, MatchType.Simple)
            .Should().BeTrue();
        Paths.MatchesExpression("filename".AsSpan(), "*.*".AsSpan(), MatchCasing.CaseSensitive, MatchType.Simple)
            .Should().BeFalse();
    }

}
