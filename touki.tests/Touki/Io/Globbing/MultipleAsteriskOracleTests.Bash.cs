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
    public static IEnumerable<(string, string)> Rows() => MultipleAsteriskRows.Rows();

    [Test]
    [MethodDataSource(nameof(Rows))]
    public void IsMatch_BashDialect_MultipleAsterisks_AgreesWithBash(string pattern, string input)
    {
        string? bashPath = BashInterop.ResolveBashPath();
        if (bashPath is null)
        {
            Skip.Test("bash oracle requires bash on PATH (or Git for Windows installed).");
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
            Skip.Test("Documented Bash shell-glob vs `[[ == ]]` string-match divergence.");
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
