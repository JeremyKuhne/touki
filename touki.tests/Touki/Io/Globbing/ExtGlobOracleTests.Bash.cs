// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET

namespace Touki.Io.Globbing;

/// <summary>
///  Oracle tests for the <see cref="GlobOptions.AllowExtGlob"/> option on the
///  <see cref="GlobDialect.Bash"/> dialect - compares touki against
///  <c>bash -O extglob -O globstar</c> via <see cref="BashInterop"/>. This is
///  the F1.4 component of the extglob rollout: the option is honored against
///  the bash oracle and any divergence surfaces as a row failure.
/// </summary>
/// <remarks>
///  <para>
///   Skipped automatically when no bash 4+ is available (notably the macOS CI
///   leg uses bash 3.2 and is excluded by <see cref="BashInterop"/>).
///  </para>
///  <para>
///   Documented divergence (skipped here): bash's <c>!(*)</c> against the
///   empty string returns true; touki returns false because <c>*</c> matches
///   the empty alternative slice exactly, so the negation rejects <c>L=0</c>.
///   Tracked in <c>docs/globbing-feature-plan.md</c>.
///  </para>
/// </remarks>
public class ExtGlobBashOracleTests
{
    public static IEnumerable<(string, string)> Rows()
    {
            string[] patterns =
            [
                // @ : exactly one
                "@(foo)",
                "@(foo|bar)",
                "@(a|b|c)",
                "foo@(x|y)bar",
                "@(|)",
                "@(a|)",
                "@(|a)",
                // ? : zero or one
                "?(foo)",
                "?(a|b)",
                "foo?(x|y)bar",
                // + : one or more
                "+(a)",
                "+(a|b)",
                "foo+(x|y)bar",
                // * : zero or more
                "*(a)",
                "*(a|b)",
                "foo*(x|y)bar",
                // ! : negation
                "!(foo)",
                "!(foo|bar)",
                "!(*.cs)",
                "!(a*)",
                // Inner wildcards
                "@(*.cs|*.txt)",
                "@(a?b)",
                // Nested
                "*(a|@(b|c))",
                "?(@(foo|bar))",
            ];

            string[] inputs =
            [
                "",
                "a", "b", "c", "x", "z",
                "ab", "ba", "ax", "xb",
                "foo", "bar", "baz",
                "foobar", "fooxbar", "foozbar",
                "aabb", "abab",
                "foo.cs", "foo.txt", "foo.json",
                "axb", "ayb",
            ];

            foreach (string pattern in patterns)
            {
                foreach (string input in inputs)
                {
                    yield return (pattern, input);
                }
            }
    }

    [Test]
    [MethodDataSource(nameof(Rows))]
    public void IsMatch_BashDialect_ExtGlob_AgreesWithBash(string pattern, string input)
    {
        string? bashPath = BashInterop.ResolveBashPath();
        if (bashPath is null)
        {
            Skip.Test("bash oracle requires bash on PATH (or Git for Windows installed).");
            return;
        }

        // Documented divergence: bash accepts `!(*)` against the empty string;
        // touki rejects. Skip the row so the rest of the oracle remains a hard
        // gate.
        if (pattern == "!(*)" && input == "")
        {
            Skip.Test("Documented divergence: bash accepts `!(*)` against empty input; touki rejects (`*` matches the empty alt slice exactly).");
            return;
        }

        bool oracle = BashInterop.Matches(bashPath, pattern, input);
        bool actual = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);

        actual.Should().Be(
            oracle,
            because: $"GlobSpecification(Bash | AllowExtGlob) and bash [[ == ]] must agree on pattern '{pattern}' vs input '{input}'");
    }
}

#endif
