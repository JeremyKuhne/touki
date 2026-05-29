// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Pattern-level scenarios ported from the GNU bash <c>tests/extglob.tests</c>
///  case-statement corpus.
/// </summary>
/// <remarks>
///  <para>
///   Source:
///   <see href="https://cgit.git.savannah.gnu.org/cgit/bash.git/plain/tests/extglob.tests"><c>bash/tests/extglob.tests</c></see>.
///   The upstream rows are written as shell <c>case</c> statements; this port
///   distills each branch into a <c>(pattern, input, expected)</c> tuple
///   asserted against <see cref="GlobSpecification.IsMatch"/> on
///   <see cref="GlobDialect.Bash"/> with
///   <see cref="GlobOptions.AllowExtGlob"/> on (matches
///   <c>shopt -s extglob</c>). Rows attributed to "Korn book" / "pdksh"
///   in the upstream file are preserved here in their original groupings.
///  </para>
///  <para>
///   bash is licensed under GPL-3.0+; pattern shapes are uncopyrightable
///   facts about the bash extglob grammar, and the inputs are short ASCII
///   tokens. The intent here is interoperability with the upstream test
///   plan, not redistribution of bash source.
///  </para>
///  <para>
///   Touki already has a live bash oracle suite
///   (<see href="https://github.com/JeremyKuhne/touki/blob/main/touki.tests/Touki/Io/Globbing/BashInterop.cs"><c>BashInterop</c></see>)
///   that runs <c>bash -O extglob -O globstar -c '[[ ... ]]'</c> on
///   Linux/macOS/Git Bash; this port captures the same scenarios as
///   compile-time-portable <c>[InlineData]</c> rows that run on every CI
///   runner including Windows hosts where bash is unavailable.
///  </para>
/// </remarks>
public class PortedTests_Bash
{
    // From extglob.tests: case statements built around
    //   case X in 0|[1-9]*([0-9])) ...
    //
    // The bare `|` at upstream case-statement level is shell alternation, not
    // part of the extglob pattern. Wrap with `@(...)` to fold both alternatives
    // into a single matchable pattern.
    [Theory]
    [InlineData("@(0|[1-9]*([0-9]))", "12", true)]
    [InlineData("@(0|[1-9]*([0-9]))", "12abc", false)]
    [InlineData("@(0|[1-9]*([0-9]))", "1", true)]
    [InlineData("@(0|[1-9]*([0-9]))", "0", true)]

    // octal numbers via +([0-7]).
    [InlineData("+([0-7])", "07", true)]
    [InlineData("+([0-7])", "0377", true)]
    [InlineData("+([0-7])", "09", false)]
    public void IsMatch_Bash_NumberAlternations(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: stuff from Korn's book.
    // para@(chute|graph), para?([345]|99)1, para*([0-9]), para+([0-9]),
    // para!(*.[0-9]).
    [Theory]
    [InlineData("para@(chute|graph)", "paragraph", true)]
    [InlineData("para@(chute|graph)", "paramour", false)]
    [InlineData("para?([345]|99)1", "para991", true)]
    [InlineData("para?([345]|99)1", "para381", false)]
    [InlineData("para*([0-9])", "paragraph", false)]
    [InlineData("para*([0-9])", "para", true)]
    [InlineData("para*([0-9])", "para13829383746592", true)]
    [InlineData("para*([0-9])", "paragraph2", false)]
    [InlineData("para+([0-9])", "para", false)]
    [InlineData("para+([0-9])", "para987346523", true)]
    [InlineData("para!(*.[0-9])", "paragraph", true)]
    [InlineData("para!(*.[0-9])", "para.38", true)]
    [InlineData("para!(*.[0-9])", "para.graph", true)]
    [InlineData("para!(*.[0-9])", "para39", true)]
    public void IsMatch_Bash_KornBookShapes(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: derived from Rosenblatt's korn shell book.
    // Empty / digit-set / extension shapes.
    [Theory]
    [InlineData("*(0|1|3|5|7|9)", "", true)]
    [InlineData("*(0|1|3|5|7|9)", "137577991", true)]
    [InlineData("*(0|1|3|5|7|9)", "2468", false)]
    [InlineData("*.c?(c)", "file.c", true)]
    [InlineData("*.c?(c)", "file.cc", true)]
    [InlineData("*.c?(c)", "file.ccc", false)]
    public void IsMatch_Bash_RosenblattShapes(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: !(*.c|*.h|Makefile.in|config*|README) negation set.
    [Theory]
    [InlineData("!(*.c|*.h|Makefile.in|config*|README)", "parse.y", true)]
    [InlineData("!(*.c|*.h|Makefile.in|config*|README)", "shell.c", false)]
    [InlineData("!(*.c|*.h|Makefile.in|config*|README)", "Makefile", true)]
    public void IsMatch_Bash_NegationSet(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: VMS-style filename versions. Upstream uses
    // *\;[1-9]*([0-9]) where the backslash escapes the ';' for bash's
    // case-statement parser. Bash's pattern matcher never sees the
    // backslash; the equivalent touki pattern is the literal *;[1-9]*([0-9]).
    [Theory]
    [InlineData("*;[1-9]*([0-9])", "VMS.FILE;1", true)]
    [InlineData("*;[1-9]*([0-9])", "VMS.FILE;0", false)]
    [InlineData("*;[1-9]*([0-9])", "VMS.FILE;", false)]
    [InlineData("*;[1-9]*([0-9])", "VMS.FILE;139", true)]
    [InlineData("*;[1-9]*([0-9])", "VMS.FILE;1N", false)]
    public void IsMatch_Bash_VmsVersionShape(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: pdksh-derived "stuck" alternations and Kleene-star
    // shapes against the four-file fixture (ab, abcdef, abef, abcfef).
    [Theory]
    [InlineData("ab*(e|f)", "ab", true)]
    [InlineData("ab*(e|f)", "abef", true)]
    [InlineData("ab*(e|f)", "abcdef", false)]
    [InlineData("ab*(e|f)", "abcfef", false)]
    [InlineData("ab?*(e|f)", "abef", true)]
    [InlineData("ab?*(e|f)", "abcfef", true)]
    [InlineData("ab?*(e|f)", "ab", false)]
    [InlineData("ab*d+(e|f)", "abcdef", true)]
    [InlineData("ab*d+(e|f)", "ab", false)]
    [InlineData("ab*+(e|f)", "abcdef", true)]
    [InlineData("ab*+(e|f)", "abcfef", true)]
    [InlineData("ab*+(e|f)", "abef", true)]
    [InlineData("ab*+(e|f)", "ab", false)]
    public void IsMatch_Bash_PdkshKleeneShapes(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: pdksh case-statement rows 37-42.
    [Theory]
    [InlineData("ab**(e|f)g", "abcfefg", true)]
    [InlineData("ab*+(e|f)", "ab", false)]
    [InlineData("ab**", "abef", true)]
    // Bug-fix regression rows (originally "bug in all versions up to and
    // including bash-2.05b"). *?(a)bc vs 123abc.
    [InlineData("*?(a)bc", "123abc", true)]
    public void IsMatch_Bash_PdkshRegressionShapes(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // Regression rows pinning the `**(` -> `*` + `*(` extglob carve-out added
    // to the encoder for this PR. Each row exercises a different shape of
    // star-run-followed-by-extglob-opener:
    //   - ab**(e|f)g    : `**` + `*(...)` followed by a literal tail (the
    //                     case-statement row 37 above, kept here for
    //                     completeness).
    //   - ab***(e|f)g   : three stars => `**` AnyRun + `*(e|f)` extglob.
    //   - @(a|c**(b))   : the carve-out also fires inside an extglob body.
    //   - ab**(e|f)     : trailing extglob with no literal tail.
    [Theory]
    [InlineData("ab**(e|f)g", "abcfefg", true)]
    [InlineData("ab**(e|f)g", "abg", true)]          // `*` + `*(e|f)` both match empty
    [InlineData("ab**(e|f)g", "abgh", false)]        // trailing literal `g` must be last
    [InlineData("ab***(e|f)g", "abcfefg", true)]
    [InlineData("ab***(e|f)g", "abcXfefg", true)]   // `***` -> `**` AnyRun crosses the X
    [InlineData("ab**(e|f)", "ab", true)]            // `*(e|f)` accepts zero alts
    [InlineData("ab**(e|f)", "abef", true)]
    [InlineData("@(a|c**(b))", "a", true)]
    [InlineData("@(a|c**(b))", "cbb", true)]
    [InlineData("@(a|c**(b))", "cXbb", true)]
    public void IsMatch_Bash_DoubleStarExtGlobCarveOut(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: /dev/@(tcp|udp)/*/* path-like alternation.
    [Theory]
    [InlineData("/dev/@(tcp|udp)/*/*", "/dev/udp/129.22.8.102/45", true)]
    [InlineData("/dev/@(tcp|udp)/*/*", "/dev/tcp/host/22", true)]
    [InlineData("/dev/@(tcp|udp)/*/*", "/dev/scp/host/22", false)]
    public void IsMatch_Bash_PathLikeAlternation(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: a!(@(b|B))d - exactly one char after 'a' that's
    // neither 'b' nor 'B', before 'd'. Bash output: 'acd'.
    [Theory]
    [InlineData("a!(@(b|B))d", "acd", true)]
    [InlineData("a!(@(b|B))d", "abd", false)]
    [InlineData("a!(@(b|B))d", "aBd", false)]
    public void IsMatch_Bash_NegatedAlternationGuard(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: a+(b|c)d matches both abd and acd.
    [Theory]
    [InlineData("a+(b|c)d", "abd", true)]
    [InlineData("a+(b|c)d", "acd", true)]
    [InlineData("a+(b|c)d", "ad", false)]
    [InlineData("a+(b|c)d", "abcd", true)]
    public void IsMatch_Bash_PlusAlternation(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // From extglob.tests: no-file+(a|b)stuff, no-file+(a*(c)|b)stuff -
    // verifies that nested extglobs match correctly when present.
    [Theory]
    [InlineData("no-file+(a|b)stuff", "no-fileastuff", true)]
    [InlineData("no-file+(a|b)stuff", "no-filebstuff", true)]
    [InlineData("no-file+(a|b)stuff", "no-filestuff", false)]
    [InlineData("no-file+(a*(c)|b)stuff", "no-fileastuff", true)]
    [InlineData("no-file+(a*(c)|b)stuff", "no-fileaccstuff", true)]
    [InlineData("no-file+(a*(c)|b)stuff", "no-filebstuff", true)]
    public void IsMatch_Bash_NestedExtGlob(string pattern, string input, bool expected) =>
        Compile(pattern).IsMatch(input).Should().Be(expected);

    // Helper: bash patterns compile with extglob enabled. The Bash dialect's
    // path-aware globstar (`**`) resolution is implicit and is not exercised
    // by extglob.tests; the `**` runs in this file (e.g. `ab**` and
    // `ab**(e|f)g`) are not segment-bounded, so they degrade to AnyRun (or to
    // the `**(` -> `*` + `*(` carve-out the Factory now handles) rather than
    // triggering the globstar opcode.
    private static GlobSpecification Compile(string pattern) =>
        GlobSpecification.Compile(pattern, GlobDialect.Bash, GlobOptions.AllowExtGlob);
}
