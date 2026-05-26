// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests that pin down how the <see cref="GlobDialect.Bash"/> dialect handles
///  runs of three or more consecutive <c>*</c> characters in the pattern, by shelling out
///  to <c>bash</c> via <see cref="BashInterop"/>.
/// </summary>
public class MultipleAsteriskBashOracleTests
{
    public static TheoryData<string, string> Rows => MultipleAsteriskRows.Rows;

    [Theory]
    [MemberData(nameof(Rows))]
    public void IsMatch_BashDialect_MultipleAsterisks_AgreesWithBash(string pattern, string input)
    {
        string? bashPath = BashInterop.ResolveBashPath();
        if (bashPath is null)
        {
            Assert.Skip("bash oracle requires bash on PATH (or Git for Windows installed).");
            return;
        }

        // Documented Bash dialect divergence. The touki Bash dialect models bash's
        // *shell-glob* `**` semantics (segment-bounded globstar, `*` does not cross
        // `/`). The `[[ str == pat ]]` oracle instead uses bash's *string-match*
        // semantics, where `*` matches any string including `/`. These four rows are
        // where the two diverge after the `***`+ &rarr; `**` normalization. Tracked
        // in docs/globbing-feature-plan.md "Multiple-asterisk-run behavior" findings.
        if ((pattern, input) is ("***/foo", "foo")
            or ("***.cs", "a/foo.cs")
            or ("a/***/b", "a/b")
            or ("a***b", "a/b"))
        {
            Assert.Skip("Documented Bash shell-glob vs `[[ == ]]` string-match divergence.");
            return;
        }

        // macOS ships bash 3.2 (Apple's licensing freeze). Bash 3.2's `[[ == ]]`
        // collapses `***`+ to a single `*` in string-match mode &mdash; bash 4+ and
        // touki's Bash dialect both treat it as `**` (any-segment match). The
        // following rows exercise that divergence and are skipped on macOS so the
        // oracle stays Linux/Git-Bash-anchored. Tracked in
        // docs/globbing-feature-plan.md.
        if (OperatingSystem.IsMacOS()
            && (pattern, input) is ("***", "a/b")
                or ("a***b", "ab")
                or ("a***b", "axb"))
        {
            Assert.Skip("macOS bash 3.2 collapses `***` to `*` in `[[ == ]]`; oracle uses bash 4+ semantics.");
            return;
        }

        bool oracle = BashInterop.Matches(bashPath, pattern, input);
        bool actual = GlobSpecification.Compile(pattern, GlobDialect.Bash, GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);
        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Bash) and bash [[ == ]] must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
