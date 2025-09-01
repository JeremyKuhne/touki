// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki.Io;

public class PathsTests
{
    [Theory]
    [InlineData("foo", "foo", MatchCasing.CaseSensitive, false)]
    [InlineData("foo", "FOO", MatchCasing.CaseSensitive, true)]
    [InlineData("foo", "FOO", MatchCasing.CaseInsensitive, false)]
    [InlineData("", "", MatchCasing.CaseSensitive, false)]
    [InlineData("a", "b", MatchCasing.CaseSensitive, true)]
    public void ArePatternsExclusive_LiteralVsLiteral_RespectsCasing(string p1, string p2, MatchCasing casing, bool expected)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, casing).Should().Be(expected);
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, casing).Should().Be(expected);
    }

    [Theory]
    [InlineData("foo", "f*", false)]
    [InlineData("foo", "b*", true)]
    [InlineData("bar", "?ar", false)]
    [InlineData("bar", "?az", true)]
    public void ArePatternsExclusive_LiteralVsWildcard_Basic(string literal, string wildcard, bool expected)
    {
        // Simple matching, explicit case-insensitive and case-sensitive should give same exclusivity for ASCII inputs
        Paths.AreExpressionsExclusive(literal, wildcard, MatchType.Simple, MatchCasing.CaseSensitive).Should().Be(expected);
        Paths.AreExpressionsExclusive(literal, wildcard, MatchType.Simple, MatchCasing.CaseInsensitive).Should().Be(expected);

        // Swap order to exercise the alternate branch
        Paths.AreExpressionsExclusive(wildcard, literal, MatchType.Simple, MatchCasing.CaseSensitive).Should().Be(expected);
    }

    [Fact]
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

    [Theory]
    [InlineData("ab*cd", "ab*ef")] // incompatible fixed suffixes
    [InlineData("foo*", "bar*")]   // incompatible fixed prefixes
    [InlineData("*foo", "*bar")]   // incompatible fixed suffixes
    [InlineData("foo?", "bar?")]   // incompatible fixed prefixes with '?'
    public void ArePatternsExclusive_BothWildcards_ObviousExclusiveCases(string p1, string p2)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeTrue();
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeTrue();
    }

    [Theory]
    [InlineData("*abc*", "*def*")] // could overlap (e.g., "abcdef")
    [InlineData("*a*c*", "*b*d*")] // uncertain overlap; should not claim exclusive
    [InlineData("pre*mid*suf", "pre*X*suf")] // same fixed prefix/suffix; not provably exclusive
    public void ArePatternsExclusive_BothWildcards_OnlyProveObviousCases(string p1, string p2)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeFalse();
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, MatchCasing.CaseSensitive).Should().BeFalse();
    }

    [Theory]
    [InlineData("*.CS", "*.cs", MatchCasing.CaseSensitive, true)]
    [InlineData("*.CS", "*.cs", MatchCasing.CaseInsensitive, false)]
    public void ArePatternsExclusive_BothWildcards_RespectsCasing(string p1, string p2, MatchCasing casing, bool expected)
    {
        Paths.AreExpressionsExclusive(p1, p2, MatchType.Simple, casing).Should().Be(expected);
        Paths.AreExpressionsExclusive(p2, p1, MatchType.Simple, casing).Should().Be(expected);
    }

    public static TheoryData<string, string> RemoveRelativeSegmentsNotFullyQualifiedData => new TheoryData<string, string>
    {
        { @"git\runtime",               @"git\runtime"},
        { @"git\\runtime",              @"git\runtime"},
        { @"git\\\runtime",             @"git\runtime"},
        { @"git\.\runtime\.\\",         @"git\runtime\"},
        { @"git\..\runtime",            @"runtime"},
        { @"git\runtime\..\",           @"git\"},
        { @"git\runtime\..\..\..\",     @"..\"},
        { @"git\runtime\..\..\.\",      @""},
        { @"git\..\.\runtime\temp\..",  @"runtime\"},
        { @"git\..\\\.\..\runtime",     @"..\runtime"},
        { @"git\runtime\",              @"git\runtime\"},
        { @"git\temp\..\runtime\",      @"git\runtime\"},
        { @".\runtime",                 @"runtime"},
        { @".\\runtime",                @"runtime"},
        { @".\\\runtime",               @"runtime"},
        { @".\.\runtime\.\\",           @"runtime\"},
        { @".\..\runtime",              @"..\runtime"},
        { @".\runtime\..\",             @""},
        { @".\runtime\..\..\..",        @"..\.."},
        { @".\runtime\..\..\.\",        @"..\"},
        { @".\..\.\runtime\temp\..",    @"..\runtime\"},
        { @".\..\\\.\..\runtime",       @"..\..\runtime"},
        { @".\runtime\",                @"runtime\"},
        { @".\temp\..\runtime\",        @"runtime\"},
        { @"C:A\.",                     @"C:A\"},
        { @"C:A\..",                    @"C:"},
        { @"C:A\..\..",                 @"C:.."},
        { @"C:A\..\..\..",              @"C:..\.."}
    };

    [Theory,
        MemberData(nameof(RemoveRelativeSegmentsNotFullyQualifiedData))]
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

    public static TheoryData<string, string> RemoveRelativeSegmentsData => new TheoryData<string, string>
    {
        { @"C:\git\runtime",                @"C:\git\runtime"},
        { @"C:\\git\runtime",               @"C:\git\runtime"},
        { @"C:\git\\runtime",               @"C:\git\runtime"},
        { @"C:\git\.\runtime\.\\",          @"C:\git\runtime\"},
        { @"C:\git\..\runtime",             @"C:\runtime"},
        { @"C:\git\runtime\..\",            @"C:\git\"},
        { @"C:\git\runtime\..\..\..\",      @"C:\"},
        { @"C:\git\runtime\..\..\.\",       @"C:\"},
        { @"C:\git\..\.\runtime\temp\..",   @"C:\runtime\"},
        { @"C:\git\..\\\.\..\runtime",      @"C:\runtime"},
        { @"C:\git\runtime\",               @"C:\git\runtime\"},
        { @"C:\git\temp\..\runtime\",       @"C:\git\runtime\"},

        { @"C:\.",                          @"C:\"},
        { @"C:\..",                         @"C:\"},
        { @"C:\..\..",                      @"C:\"},

        { @"C:\tmp\home",                   @"C:\tmp\home" },
        { @"C:\tmp\..",                     @"C:\" },
        { @"C:\tmp\home\..\.\.\",           @"C:\tmp\" },
        { @"C:\tmp\..\..\..\",              @"C:\" },
        { @"C:\tmp\\home",                  @"C:\tmp\home" },
        { @"C:\.\tmp\\home",                @"C:\tmp\home" },
        { @"C:\..\tmp\home",                @"C:\tmp\home" },
        { @"C:\..\..\..\tmp\.\home",        @"C:\tmp\home" },
        { @"C:\\tmp\\\home",                @"C:\tmp\home" },
        { @"C:\tmp\home\git\.\..\.\git\runtime\..\", @"C:\tmp\home\git\" },
        { @"C:\.\tmp\home",                 @"C:\tmp\home" },

        { @"C:\tmp\home\..\..\.\",          @"C:\" },
        { @"C:\tmp\..\..\",                 @"C:\" },
        { @"C:\tmp\\home\..\.\\",           @"C:\tmp\" },
        { @"C:\.\tmp\\home\git\git",        @"C:\tmp\home\git\git" },
        { @"C:\..\tmp\.\home",              @"C:\tmp\home" },
        { @"C:\\tmp\\\home\..",             @"C:\tmp\" },
        { @"C:\.\tmp\home\.\.\",            @"C:\tmp\home\" },
    };

    public static TheoryData<string, string> RemoveRelativeSegmentsFirstRelativeSegment => new TheoryData<string, string>
    {
        { @"C:\.\git\runtime",              @"C:\git\runtime"},
        { @"C:\\.\git\.\runtime",           @"C:\git\runtime"},
        { @"C:\..\git\runtime",             @"C:\git\runtime"},
        { @"C:\.\git\..\runtime",           @"C:\runtime"},
        { @"C:\.\git\runtime\..\",          @"C:\git\"},
        { @"C:\.\git\runtime\..\..\..\",    @"C:\"},
        { @"C:\.\git\runtime\..\..\.\",     @"C:\"},
        { @"C:\.\git\..\.\runtime\temp\..", @"C:\runtime\"},
        { @"C:\.\git\..\\\.\..\runtime",    @"C:\runtime"},
        { @"C:\.\git\runtime\",             @"C:\git\runtime\"},
        { @"C:\.\git\temp\..\runtime\",     @"C:\git\runtime\"},
        { @"C:\\..\..",                     @"C:\"}
    };

    [Theory,
        MemberData(nameof(RemoveRelativeSegmentsData)),
        MemberData(nameof(RemoveRelativeSegmentsFirstRelativeSegment))]
    public void RemoveRelativeSegments(string path, string expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        Assert.Equal(expected, Paths.RemoveRelativeSegments(path));
        Assert.Equal(@"\\.\" + expected, Paths.RemoveRelativeSegments(@"\\.\" + path));
        Assert.Equal(@"\\?\" + expected, Paths.RemoveRelativeSegments(@"\\?\" + path));
    }

    public static TheoryData<string, string> RemoveRelativeSegmentsUncData => new TheoryData<string, string>
    {
        { @"Server\Share\git\runtime",             @"Server\Share\git\runtime"},
        { @"Server\Share\\git\runtime",            @"Server\Share\git\runtime"},
        { @"Server\Share\git\\runtime",            @"Server\Share\git\runtime"},
        { @"Server\Share\git\.\runtime\.\\",       @"Server\Share\git\runtime\"},
        { @"Server\Share\git\..\runtime",          @"Server\Share\runtime"},
        { @"Server\Share\git\runtime\..\",         @"Server\Share\git\"},
        { @"Server\Share\git\runtime\..\..\..\",   @"Server\Share\"},
        { @"Server\Share\git\runtime\..\..\.\",    @"Server\Share\"},
        { @"Server\Share\git\..\.\runtime\temp\..", @"Server\Share\runtime\"},
        { @"Server\Share\git\..\\\.\..\runtime",   @"Server\Share\runtime"},
        { @"Server\Share\git\runtime\",            @"Server\Share\git\runtime\"},
        { @"Server\Share\git\temp\..\runtime\",    @"Server\Share\git\runtime\"},
    };

    [Theory,
        MemberData(nameof(RemoveRelativeSegmentsUncData))]
    public void RemoveRelativeSegments_Unc(string path, string expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        Assert.Equal(@"\\" + expected, Paths.RemoveRelativeSegments(@"\\" + path));
        Assert.Equal(@"\\.\UNC\" + expected, Paths.RemoveRelativeSegments(@"\\.\UNC\" + path));
        Assert.Equal(@"\\?\UNC\" + expected, Paths.RemoveRelativeSegments(@"\\?\UNC\" + path));
    }

    public static TheoryData<string, string> RemoveRelativeSegmentsDeviceData => new TheoryData<string, string>
    {
        { @"\\.\git\runtime",                @"\\.\git\runtime"},
        { @"\\.\git\\runtime",               @"\\.\git\runtime"},
        { @"\\.\git\.\runtime\.\\",          @"\\.\git\runtime\"},
        { @"\\.\git\..\runtime",             @"\\.\git\runtime"},
        { @"\\.\git\runtime\..\",            @"\\.\git\"},
        { @"\\.\git\runtime\..\..\..\",      @"\\.\git\"},
        { @"\\.\git\runtime\..\..\.\",       @"\\.\git\"},
        { @"\\.\git\..\.\runtime\temp\..",   @"\\.\git\runtime\"},
        { @"\\.\git\..\\\.\..\runtime",      @"\\.\git\runtime"},
        { @"\\.\git\runtime\",               @"\\.\git\runtime\"},
        { @"\\.\git\temp\..\runtime\",       @"\\.\git\runtime\"},

        { @"\\.\.\runtime",                  @"\\.\.\runtime"},
        { @"\\.\.\\runtime",                 @"\\.\.\runtime"},
        { @"\\.\.\.\runtime\.\\",            @"\\.\.\runtime\"},
        { @"\\.\.\..\runtime",               @"\\.\.\runtime"},
        { @"\\.\.\runtime\..\",              @"\\.\.\"},
        { @"\\.\.\runtime\..\..\..\",        @"\\.\.\"},
        { @"\\.\.\runtime\..\..\.\",         @"\\.\.\"},
        { @"\\.\.\..\.\runtime\temp\..",     @"\\.\.\runtime\"},
        { @"\\.\.\..\\\.\..\runtime",        @"\\.\.\runtime"},
        { @"\\.\.\runtime\",                 @"\\.\.\runtime\"},
        { @"\\.\.\temp\..\runtime\",         @"\\.\.\runtime\"},

        { @"\\.\..\runtime",                 @"\\.\..\runtime"},
        { @"\\.\..\\runtime",                @"\\.\..\runtime"},
        { @"\\.\..\.\runtime\.\\",           @"\\.\..\runtime\"},
        { @"\\.\..\..\runtime",              @"\\.\..\runtime"},
        { @"\\.\..\runtime\..\",             @"\\.\..\"},
        { @"\\.\..\runtime\..\..\..\",       @"\\.\..\"},
        { @"\\.\..\runtime\..\..\.\",        @"\\.\..\"},
        { @"\\.\..\..\.\runtime\temp\..",    @"\\.\..\runtime\"},
        { @"\\.\..\..\\\.\..\runtime",       @"\\.\..\runtime"},
        { @"\\.\..\runtime\",                @"\\.\..\runtime\"},
        { @"\\.\..\temp\..\runtime\",        @"\\.\..\runtime\"},

        { @"\\.\\runtime",                   @"\\.\runtime"},
        { @"\\.\\\runtime",                  @"\\.\runtime"},
        { @"\\.\\.\runtime\.\\",             @"\\.\runtime\"},
        { @"\\.\\..\runtime",                @"\\.\runtime"},
        { @"\\.\\runtime\..\",               @"\\.\"},
        { @"\\.\\runtime\..\..\..\",         @"\\.\"},
        { @"\\.\\runtime\..\..\.\",          @"\\.\"},
        { @"\\.\\..\.\runtime\temp\..",      @"\\.\runtime\"},
        { @"\\.\\..\\\.\..\runtime",         @"\\.\runtime"},
        { @"\\.\\runtime\",                  @"\\.\runtime\"},
        { @"\\.\\temp\..\runtime\",          @"\\.\runtime\"},
    };

    [Theory,
        MemberData(nameof(RemoveRelativeSegmentsDeviceData))]
    public void RemoveRelativeSegments_Device(string path, string expected)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            return;
        }
#endif

        Assert.Equal(expected, Paths.RemoveRelativeSegments(path));
        StringBuilder sb = new StringBuilder(expected);
        sb.Replace('.', '?', 0, 4);
        expected = sb.ToString();

        sb = new StringBuilder(path);
        sb.Replace('.', '?', 0, 4);
        path = sb.ToString();
        Assert.Equal(expected, Paths.RemoveRelativeSegments(path));
    }

    public static TheoryData<string, string> RemoveRelativeSegmentUnixData => new TheoryData<string, string>
    {
        { "/tmp/home",                          "/tmp/home" },
        { "/tmp/..",                            "/" },
        { "/tmp/home/../././",                  "/tmp/" },
        { "/tmp/../../../",                     "/" },
        { "/tmp//home",                         "/tmp/home" },
        { "/./tmp//home",                       "/tmp/home" },
        { "/../tmp/home",                       "/tmp/home" },
        { "/../../../tmp/./home",               "/tmp/home" },
        { "//tmp///home",                       "/tmp/home" },
        { "/tmp/home/git/./.././git/runtime/../", "/tmp/home/git/" },
        { "/./tmp/home",                        "/tmp/home" },

        { "/tmp/..",                            "/tmp" },
        { "/tmp/../../../",                     "/tmp/" },
        { "/./tmp//home",                       "/./tmp/home" },
        { "/../tmp/home",                       "/../tmp/home" },
        { "/../../../tmp/./home",               "/../tmp/home" },
        { "//tmp///home",                       "//tmp/home" },
        { "/./tmp/home",                        "/./tmp/home" },

        { "/tmp/../../",                        "/tmp/../" },
        { "/tmp/home/../././",                  "/tmp/home/" },
        { "/tmp/../../../",                     "/tmp/../" },
        { "/tmp//home/.././/",                  "/tmp//home/" },
        { "/./tmp//home/git/git",               "/./tmp/home/git/git" },
        { "/../tmp/./home",                     "/../tmp/home" },
        { "/../../../tmp/./home",               "/../../../tmp/home" },
        { "//tmp///home/..",                    "//tmp/" },
        { "/tmp/home/git/./.././git/runtime/../", "/tmp/home/git/./git/" },
        { "/./tmp/home/././",                   "/./tmp/home/" },
    };

#if NET
    [Theory,
        MemberData(nameof(RemoveRelativeSegmentUnixData))]
    public void RemoveRelativeSegments_Unix(string path, string expected)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(expected, Paths.RemoveRelativeSegments(path));
    }
#endif

}
