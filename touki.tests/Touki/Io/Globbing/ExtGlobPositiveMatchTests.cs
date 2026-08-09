// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Match tests for the four positive extended-glob constructs -
///  <c>?(...)</c>, <c>*(...)</c>, <c>+(...)</c>, and <c>@(...)</c>. Negation
///  (<c>!(...)</c>) is covered in a later step and intentionally still
///  reports no match here.
/// </summary>
[TestClass]
public class ExtGlobPositiveMatchTests
{
    private static bool Match(string pattern, string input, GlobDialect dialect = GlobDialect.Bash) =>
        GlobSpecification.Compile(
            pattern,
            dialect,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);

    // -- @(...) : exactly one alternative must match --------------------------------

    [TestMethod]
    [DataRow("@(foo)", "foo", true)]
    [DataRow("@(foo)", "bar", false)]
    [DataRow("@(foo|bar)", "foo", true)]
    [DataRow("@(foo|bar)", "bar", true)]
    [DataRow("@(foo|bar)", "baz", false)]
    [DataRow("@(a|b|c)", "a", true)]
    [DataRow("@(a|b|c)", "c", true)]
    [DataRow("@(a|b|c)", "d", false)]
    [DataRow("@(a|b)", "", false)]
    [DataRow("@(a|b)", "ab", false)]
    public void Match_At_ExactlyOneAlternative(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    [TestMethod]
    // @(...) embedded in surrounding literals.
    [DataRow("foo@(x|y)bar", "fooxbar", true)]
    [DataRow("foo@(x|y)bar", "fooybar", true)]
    [DataRow("foo@(x|y)bar", "foozbar", false)]
    [DataRow("foo@(x|y)bar", "fooxybar", false)]
    [DataRow("foo@(x|y)bar", "foobar", false)]
    public void Match_At_WithSurroundingLiterals(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- ?(...) : zero or one alternative ---------------------------------------------

    [TestMethod]
    [DataRow("?(foo)", "", true)]
    [DataRow("?(foo)", "foo", true)]
    [DataRow("?(foo)", "bar", false)]
    [DataRow("?(foo)", "foofoo", false)]
    [DataRow("?(a|b)", "", true)]
    [DataRow("?(a|b)", "a", true)]
    [DataRow("?(a|b)", "b", true)]
    [DataRow("?(a|b)", "c", false)]
    [DataRow("foo?(x|y)bar", "fooxbar", true)]
    [DataRow("foo?(x|y)bar", "foobar", true)]
    [DataRow("foo?(x|y)bar", "fooxxbar", false)]
    public void Match_Question_ZeroOrOne(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- +(...) : one or more alternatives --------------------------------------------

    [TestMethod]
    [DataRow("+(a)", "", false)]
    [DataRow("+(a)", "a", true)]
    [DataRow("+(a)", "aa", true)]
    [DataRow("+(a)", "aaaa", true)]
    [DataRow("+(a)", "ab", false)]
    [DataRow("+(a|b)", "ab", true)]
    [DataRow("+(a|b)", "aabb", true)]
    [DataRow("+(a|b)", "abab", true)]
    [DataRow("+(a|b)", "abc", false)]
    [DataRow("+(a|b)", "", false)]
    [DataRow("foo+(x|y)bar", "fooxbar", true)]
    [DataRow("foo+(x|y)bar", "fooxxbar", true)]
    [DataRow("foo+(x|y)bar", "fooxyxbar", true)]
    [DataRow("foo+(x|y)bar", "foobar", false)]
    public void Match_Plus_OneOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- *(...) : zero or more alternatives -------------------------------------------

    [TestMethod]
    [DataRow("*(a)", "", true)]
    [DataRow("*(a)", "a", true)]
    [DataRow("*(a)", "aa", true)]
    [DataRow("*(a)", "aaaa", true)]
    [DataRow("*(a)", "ab", false)]
    [DataRow("*(a|b)", "", true)]
    [DataRow("*(a|b)", "abab", true)]
    [DataRow("*(a|b)", "c", false)]
    [DataRow("foo*(x|y)bar", "foobar", true)]
    [DataRow("foo*(x|y)bar", "fooxbar", true)]
    [DataRow("foo*(x|y)bar", "fooxxxxxbar", true)]
    [DataRow("foo*(x|y)bar", "fooxybar", true)]
    [DataRow("foo*(x|y)bar", "foozbar", false)]
    public void Match_Star_ZeroOrMore(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Multiple alternatives --------------------------------------------------------

    [TestMethod]
    [DataRow("@(foo|bar|baz)", "foo", true)]
    [DataRow("@(foo|bar|baz)", "bar", true)]
    [DataRow("@(foo|bar|baz)", "baz", true)]
    [DataRow("@(foo|bar|baz)", "qux", false)]
    public void Match_MultipleAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Empty alternatives -----------------------------------------------------------

    [TestMethod]
    [DataRow("@(|)", "", true)]
    [DataRow("@(|)", "x", false)]
    [DataRow("@(|a)", "", true)]
    [DataRow("@(|a)", "a", true)]
    [DataRow("@(a|)", "", true)]
    [DataRow("@(a|)", "a", true)]
    [DataRow("foo@(|x)bar", "foobar", true)]
    [DataRow("foo@(|x)bar", "fooxbar", true)]
    [DataRow("foo@(|x)bar", "fooybar", false)]
    public void Match_EmptyAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Inner wildcards inside alternatives ------------------------------------------

    [TestMethod]
    [DataRow("@(*.cs|*.txt)", "foo.cs", true)]
    [DataRow("@(*.cs|*.txt)", "foo.txt", true)]
    [DataRow("@(*.cs|*.txt)", "foo.json", false)]
    [DataRow("@(a?b)", "axb", true)]
    [DataRow("@(a?b)", "ab", false)]
    [DataRow("@(a?b)", "axyb", false)]
    public void Match_InnerWildcards(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Nested extglob ---------------------------------------------------------------

    [TestMethod]
    [DataRow("*(a|@(b|c))d", "d", true)]
    [DataRow("*(a|@(b|c))d", "ad", true)]
    [DataRow("*(a|@(b|c))d", "bd", true)]
    [DataRow("*(a|@(b|c))d", "cd", true)]
    [DataRow("*(a|@(b|c))d", "abcd", true)]
    [DataRow("*(a|@(b|c))d", "abxd", false)]
    [DataRow("?(@(foo|bar))", "foo", true)]
    [DataRow("?(@(foo|bar))", "bar", true)]
    [DataRow("?(@(foo|bar))", "", true)]
    [DataRow("?(@(foo|bar))", "baz", false)]
    public void Match_Nested(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Path-aware: inner wildcards don't cross the separator ------------------------

    [TestMethod]
    [DataRow("@(*.cs|*.txt)", "foo/bar.cs", false)]
    [DataRow("@(*.cs|*.txt)", "foo.cs", true)]
    [DataRow("dir/@(a|b)", "dir/a", true)]
    [DataRow("dir/@(a|b)", "dir/b", true)]
    [DataRow("dir/@(a|b)", "dir/c", false)]
    public void Match_PathAware(string pattern, string input, bool expected) =>
        Match(pattern, input, GlobDialect.Bash).Should().Be(expected);

    [TestMethod]
    [DataRow("@(a|b).", "a.", "b.")]
    [DataRow("@(a?|b).", "a?.", "b.")]
    [DataRow("@(*|a).", "*.", "a.")]
    [DataRow("@(|a).", ".", "a.")]
    [DataRow("@(*|a**b).", "*.", "a**b.")]
    public void Match_MSBuildTrailingDotExtGlob_EqualsAlternativeUnion(
        string combinedPattern,
        string firstPattern,
        string secondPattern)
    {
        string[] candidates = ["", "a", "b", "c", "ab", "abc", "a.", "b.", "a..", "...", "a.b", "a.xb."];
        GlobSpecification combined = GlobSpecification.Compile(
            combinedPattern,
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile(firstPattern, GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile(secondPattern, GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            bool expected = first.IsMatch(candidate) || second.IsMatch(candidate);
            combined.IsMatch(candidate).Should().Be(
                expected,
                because: $"the extglob alternatives must preserve trailing-dot semantics for '{candidate}'");
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotExtGlobOpenerAfterStar_EqualsPlainStar()
    {
        string[] candidates = ["", "x", "xa", "xbbb", "xaaaa", "x.", "x..", "x.b"];
        GlobSpecification combined = GlobSpecification.Compile(
            "x**(a).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification expected = GlobSpecification.Compile("x*.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                expected.IsMatch(candidate),
                because: $"the optional repeated extglob can be absorbed by the preceding star for '{candidate}'");
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotExtGlobNegation_AllDotInputUsesCompositionRule()
    {
        GlobSpecification negation = GlobSpecification.Compile(
            "!(a).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification negationThenStar = GlobSpecification.Compile(
            "!(a)*.",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification alternative = GlobSpecification.Compile("a.", GlobDialect.MSBuild);
        GlobSpecification star = GlobSpecification.Compile("*.", GlobDialect.MSBuild);

        negation.IsMatch("...").Should().Be(!alternative.IsMatch("..."));
        negationThenStar.IsMatch("...").Should().Be(star.IsMatch("..."));
    }

    [TestMethod]
    public void Match_MSBuildStarDotStarExtGlob_PreservesLiteralDotAndOperator()
    {
        GlobSpecification specification = GlobSpecification.Compile(
            "*.*(a)",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        specification.IsMatch(".").Should().BeTrue();
        specification.IsMatch(".a").Should().BeTrue();
        specification.IsMatch(string.Empty).Should().BeFalse();
    }

    [TestMethod]
    public void Match_MSBuildStarExtGlobBeforeStarDotStar_RewritesOnlyOrdinarySequence()
    {
        string[] candidates = ["x", "xa", "xb", "xab", "x.a", "x.b"];
        GlobSpecification combined = GlobSpecification.Compile(
            "x**(a)*.*",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification expected = GlobSpecification.Compile(
            "x**(a)*",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(expected.IsMatch(candidate));
        }
    }

    [TestMethod]
    public void Match_MSBuildNestedDoubleStarDoesNotSuppressOuterStarDotStarRewrite()
    {
        string[] candidates = ["x", "xa", "xb", "xab", "x.a", "x.b"];
        GlobSpecification combined = GlobSpecification.Compile(
            "x**(a**b)*.*",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification expected = GlobSpecification.Compile(
            "x**(a**b)*",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(expected.IsMatch(candidate));
        }
    }

    [TestMethod]
    [DataRow('@', 1, 1)]
    [DataRow('?', 0, 1)]
    [DataRow('+', 1, 3)]
    [DataRow('*', 0, 3)]
    public void Match_MSBuildMixedTrailingDotExtGlob_EqualsBoundedExpansions(
        char kind,
        int minimumRepetitions,
        int maximumRepetitions)
    {
        string[] candidates =
        [
            "", "x", "xx", "xxx", "a.xb.", "xa.xb.", "a.xb.x.", "xxa.xb.", "a.xb.a.xb."
        ];
        GlobSpecification combined = GlobSpecification.Compile(
            $"{kind}(x|a**b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        List<GlobSpecification> expansions = [];
        for (int repetitions = minimumRepetitions; repetitions <= maximumRepetitions; repetitions++)
        {
            int sequenceCount = 1 << repetitions;
            for (int sequence = 0; sequence < sequenceCount; sequence++)
            {
                string[] parts = new string[repetitions + 1];
                for (int index = 0; index < repetitions; index++)
                {
                    parts[index] = (sequence & (1 << index)) == 0 ? "x" : "a**b";
                }

                parts[^1] = ".";
                expansions.Add(GlobSpecification.Compile(string.Concat(parts), GlobDialect.MSBuild));
            }
        }

        foreach (string candidate in candidates)
        {
            bool expected = expansions.Any(expansion => expansion.IsMatch(candidate));
            combined.IsMatch(candidate).Should().Be(
                expected,
                because: $"{kind}(x|a**b). must preserve per-expansion MSBuild semantics for '{candidate}'");
        }
    }

    [TestMethod]
    public void Match_MSBuildMixedTrailingDotNegation_ComplementsAlternativeUnion()
    {
        string[] candidates = ["", "x", "y", "ab", "a.xb.", "a.yb.", "c.y.", "x."];
        GlobSpecification combined = GlobSpecification.Compile(
            "!(x|a**b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("x.", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile("a**b.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            bool expected = !first.IsMatch(candidate) && !second.IsMatch(candidate);
            combined.IsMatch(candidate).Should().Be(
                expected,
                because: $"the negation must complement both policy-specific alternatives for '{candidate}'");
        }
    }

    [TestMethod]
    public void Match_MSBuildMixedTrailingDotNegation_NestedAlternativeComplementsUnion()
    {
        string[] candidates = ["x", "y", "z", "ab", "a.xb.", "other"];
        GlobSpecification combined = GlobSpecification.Compile(
            "!(x|@(y|z)|a**b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("x.", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile(
            "@(y|z).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification third = GlobSpecification.Compile("a**b.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            bool expected = !first.IsMatch(candidate)
                && !second.IsMatch(candidate)
                && !third.IsMatch(candidate);
            combined.IsMatch(candidate).Should().Be(expected);
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotNegationAtWrapper_EqualsDirectNegation()
    {
        string[] candidates = ["ab", "aZZb.", "a.yb.", "other"];
        GlobSpecification wrapped = GlobSpecification.Compile(
            "@(@(!(a**b))).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification direct = GlobSpecification.Compile(
            "!(a**b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        foreach (string candidate in candidates)
        {
            wrapped.IsMatch(candidate).Should().Be(direct.IsMatch(candidate));
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotNegationInMultiAlternativeWrapper_RetainsUnionSemantics()
    {
        string[] candidates = ["aZZb.", "keep", "other"];
        GlobSpecification combined = GlobSpecification.Compile(
            "@(!(a**b)|keep).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification negation = GlobSpecification.Compile(
            "!(a**b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification keep = GlobSpecification.Compile("keep.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                negation.IsMatch(candidate) || keep.IsMatch(candidate));
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotEmbeddedNegation_PreservesAlternativeMembership()
    {
        GlobSpecification specification = GlobSpecification.Compile(
            "x!(a**b)y.",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        specification.IsMatch("xaby").Should().BeFalse();
        specification.IsMatch("xother-y").Should().BeTrue();
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotExactUnion_DeadArmDoesNotPoisonLiveArm()
    {
        GlobSpecification specification = GlobSpecification.Compile(
            "@(***|a).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        specification.IsMatch("a").Should().BeTrue();
        specification.IsMatch("other").Should().BeFalse();
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotNegation_DeadArmDoesNotPoisonComplement()
    {
        GlobSpecification specification = GlobSpecification.Compile(
            "!(***|a).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        specification.IsMatch("a").Should().BeFalse();
        specification.IsMatch("other").Should().BeTrue();
    }

    [TestMethod]
    [DataRow('@')]
    [DataRow('?')]
    [DataRow('+')]
    [DataRow('*')]
    public void Match_MSBuildTrailingDotPositiveExtGlob_DeadArmDoesNotPoisonLiveArm(char kind)
    {
        GlobSpecification specification = GlobSpecification.Compile(
            $"{kind}(***|a).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        specification.IsMatch("a").Should().BeTrue();
    }

    [TestMethod]
    public void Match_MSBuildExtGlob_DeadArmDoesNotPoisonLiveArmOrNegation()
    {
        GlobSpecification positive = GlobSpecification.Compile(
            "+(***|a)",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification negative = GlobSpecification.Compile(
            "!(***|a)",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        positive.IsMatch("a").Should().BeTrue();
        negative.IsMatch("a").Should().BeFalse();
        negative.IsMatch("other").Should().BeTrue();
    }

    [TestMethod]
    public void Match_MSBuildExtGlobSeparators_DoNotChangeFilenameRewriteScope()
    {
        string[] candidates = ["README", "dir/README", "dir/file.txt", "other/file.txt"];
        GlobSpecification combined = GlobSpecification.Compile(
            "@(*.*|dir/*.*)",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("*.*", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile("dir/*.*", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                first.IsMatch(candidate) || second.IsMatch(candidate),
                because: $"the embedded separator alternatives must agree for '{candidate}'");
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotExtGlobSeparators_EqualsAlternativeUnion()
    {
        string[] candidates = ["a/b", "a/b.", "c", "c.", "other"];
        GlobSpecification combined = GlobSpecification.Compile(
            "@(a/b|c).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("a/b.", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile("c.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                first.IsMatch(candidate) || second.IsMatch(candidate));
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotNegation_RequiresOuterPathDomain()
    {
        GlobSpecification specification = GlobSpecification.Compile(
            "dir/!(a|b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        specification.IsMatch("dir/a").Should().BeFalse();
        specification.IsMatch("dir/c").Should().BeTrue();
        specification.IsMatch("other/c").Should().BeFalse();
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotEmbeddedExtGlobSeparator_EqualsAlternativeUnion()
    {
        string[] candidates = ["xa/b", "xc", "xa/c", "other"];
        GlobSpecification combined = GlobSpecification.Compile(
            "x@(a/b|c).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("xa/b.", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile("xc.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                first.IsMatch(candidate) || second.IsMatch(candidate),
                because: $"the embedded separator alternatives must agree for '{candidate}'");
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotFullPrefix_PreservesDirectoryQuestionMarkSemantics()
    {
        string[] candidates = ["q/xa", "q/xb", "qq/xa", "q/xc"];
        GlobSpecification combined = GlobSpecification.Compile(
            "?/x@(a|b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("?/xa.", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile("?/xb.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                first.IsMatch(candidate) || second.IsMatch(candidate));
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotFullPrefix_PreservesDirectoryGlobStarSemantics()
    {
        string[] candidates = ["xa", "xb", "d1/d2/xa", "d1/d2/xb", "d1/d2/xc"];
        GlobSpecification combined = GlobSpecification.Compile(
            "**/x@(a|b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("**/xa.", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile("**/xb.", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                first.IsMatch(candidate) || second.IsMatch(candidate));
        }
    }

    [TestMethod]
    public void Match_MSBuildExtGlobAlternative_RewritesOnlyFinalSegment()
    {
        string[] candidates = ["README/file", "README.txt/file", "other"];
        GlobSpecification combined = GlobSpecification.Compile(
            "@(*.*/file|other)",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);
        GlobSpecification first = GlobSpecification.Compile("*.*/file", GlobDialect.MSBuild);
        GlobSpecification second = GlobSpecification.Compile("other", GlobDialect.MSBuild);

        foreach (string candidate in candidates)
        {
            combined.IsMatch(candidate).Should().Be(
                first.IsMatch(candidate) || second.IsMatch(candidate));
        }
    }

    [TestMethod]
    public void Match_MSBuildMixedTrailingDotExtGlob_HonorsBackslashSeparator()
    {
        string[] candidates = ["x", "a/yb.", "a\\yb."];
        GlobSpecification combined = GlobSpecification.Compile(
            "@(x|a**b).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob,
            GlobPathSeparator.Backslash);
        GlobSpecification first = GlobSpecification.Compile(
            "x.",
            GlobDialect.MSBuild,
            separator: GlobPathSeparator.Backslash);
        GlobSpecification second = GlobSpecification.Compile(
            "a**b.",
            GlobDialect.MSBuild,
            separator: GlobPathSeparator.Backslash);

        foreach (string candidate in candidates)
        {
            bool expected = first.IsMatch(candidate) || second.IsMatch(candidate);
            combined.IsMatch(candidate).Should().Be(expected);
        }
    }

    [TestMethod]
    public void Match_MSBuildTrailingDotNeverMatchAlternative_CompilesAndNeverMatches()
    {
        GlobSpecification specification = GlobSpecification.Compile(
            "@(***).",
            GlobDialect.MSBuild,
            GlobOptions.AllowExtGlob);

        specification.IsMatch(string.Empty).Should().BeFalse();
        specification.IsMatch("anything").Should().BeFalse();
        specification.IsMatch("anything.").Should().BeFalse();
    }

    // -- IgnoreCase ------------------------------------------------------------------

    [TestMethod]
    [DataRow("@(FOO|BAR)", "foo", true)]
    [DataRow("@(FOO|BAR)", "bar", true)]
    [DataRow("@(FOO|BAR)", "baz", false)]
    public void Match_IgnoreCase(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
