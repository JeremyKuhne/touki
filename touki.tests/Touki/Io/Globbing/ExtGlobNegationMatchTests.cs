// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Match tests for the negation extended-glob construct <c>!(...)</c>: the
///  input is accepted iff there exists a consumed length <c>L</c> such that no
///  alternative matches the input slice <c>input[0..L]</c> exactly, AND the
///  surrounding pattern matches the remainder <c>input[L..]</c>.
/// </summary>
/// <remarks>
///  <para>
///   Bash diverges on a handful of edge cases (notably <c>!(*)</c> against the
///   empty string - bash accepts, touki rejects because <c>*</c> matches
///   the empty alternative slice). Those divergences will be tracked as
///   documented oracle deltas in step 5 when the bash oracle goes live.
///  </para>
/// </remarks>
public class ExtGlobNegationMatchTests
{
    private static bool Match(string pattern, string input, GlobDialect dialect = GlobDialect.Bash) =>
        GlobSpecification.Compile(
            pattern,
            dialect,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob).IsMatch(input);

    // -- !(literal) ------------------------------------------------------------------

    [Theory]
    [InlineData("!(foo)", "bar", true)]
    [InlineData("!(foo)", "foo", false)]
    [InlineData("!(foo)", "", true)]              // empty isn't "foo"
    [InlineData("!(foo)", "foobar", true)]        // foobar isn't "foo"
    [InlineData("!(a)", "b", true)]
    [InlineData("!(a)", "a", false)]
    [InlineData("!(a)", "", true)]
    public void Match_NegationOfLiteral(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- !(alt1|alt2|...) -----------------------------------------------------------

    [Theory]
    [InlineData("!(foo|bar)", "baz", true)]
    [InlineData("!(foo|bar)", "foo", false)]
    [InlineData("!(foo|bar)", "bar", false)]
    [InlineData("!(a|b|c)", "x", true)]
    [InlineData("!(a|b|c)", "a", false)]
    [InlineData("!(a|b|c)", "b", false)]
    [InlineData("!(a|b|c)", "c", false)]
    public void Match_NegationMultipleAlternatives(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- !(...) with surrounding literals --------------------------------------------

    [Theory]
    [InlineData("!(foo)bar", "xbar", true)]
    [InlineData("!(foo)bar", "foobar", false)]    // "" doesn't match foo, but rest "bar" needs "foobar" → no
    [InlineData("!(foo)bar", "bar", true)]        // L=0: empty isn't foo; rest "bar" matches "bar"
    [InlineData("!(foo)bar", "xyzbar", true)]
    [InlineData("foo!(x|y)bar", "foobar", true)]  // L=0: empty isn't x or y; rest "bar" matches
    [InlineData("foo!(x|y)bar", "fooxbar", false)] // L=1: "x" matches x → rejected. L=0,2,3,4 all fail rest match
    [InlineData("foo!(x|y)bar", "foozbar", true)]  // L=1: "z" isn't x or y; rest "bar" matches "bar"
    public void Match_NegationWithSurroundingLiterals(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- !(...) with inner wildcards -------------------------------------------------

    [Theory]
    [InlineData("!(a*)", "b", true)]
    [InlineData("!(a*)", "abc", false)]           // "abc" matches "a*"
    [InlineData("!(*.cs)", "foo.txt", true)]
    [InlineData("!(*.cs)", "foo.cs", false)]
    [InlineData("!(?)", "abc", true)]             // "abc" isn't a single char
    [InlineData("!(?)", "a", false)]              // "a" matches "?"
    public void Match_NegationWithInnerWildcards(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- Nested negation --------------------------------------------------------------

    [Theory]
    [InlineData("!(!(foo))", "foo", true)]        // double negation
    [InlineData("!(!(foo))", "bar", false)]
    [InlineData("@(!(foo))", "bar", true)]
    [InlineData("@(!(foo))", "foo", false)]
    public void Match_NegationNested(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    [Fact]
    public void Match_MaxDepthNegationNesting_DoesNotTripRecursionGuard()
    {
        // The encoder accepts up to GlobSpecification.MaxExtGlobDepth nested
        // extglob constructs. Negation re-entry is the engine's only native
        // recursion, so the deepest legal pattern - that many nested !(...) -
        // drives the recursion to depth MaxExtGlobDepth (the innermost !(x)
        // body is a literal, matched inline without re-entry). That is the
        // deepest level any valid input can reach; it sits one frame below the
        // guard's MaxExtGlobDepth + 1 budget by design. This pins that the
        // budget accommodates the real maximum and never trips on valid input.
        // (MatchCore_OverBudgetNegationNesting_TripsRecursionGuard covers the
        // over-budget trip with hand-crafted bytecode.)
        int depth = GlobSpecification.MaxExtGlobDepth;
        string pattern = string.Concat(Enumerable.Repeat("!(", depth)) + "x" + new string(')', depth);

        // !(P) matches s exactly when P does not match s, so wrapping x in an
        // even number of negations is equivalent to matching "x", and an odd
        // number is equivalent to "not x".
        bool matchesLiteral = (depth & 1) == 0;

        Func<bool> act = () => Match(pattern, "x");
        act.Should().NotThrow();
        act().Should().Be(matchesLiteral);
        Match(pattern, "abcd").Should().Be(!matchesLiteral);
    }

    [Fact]
    public void MatchCore_OverBudgetNegationNesting_TripsRecursionGuard()
    {
        // Companion to Match_MaxDepthNegationNesting_DoesNotTripRecursionGuard:
        // that test proves the guard never trips at the deepest LEGAL nesting;
        // this one proves the guard actually fires when nesting exceeds the
        // budget. The encoder (GlobSpecification.Factory) rejects patterns nested
        // deeper than MaxExtGlobDepth, so a normally compiled pattern can never
        // reach the guard. To exercise the safety net we hand-craft an over-deep
        // bytecode program directly - the shape an encoder bug would have to
        // produce for the guard to matter - and run it through the engine.
        //
        // Each !(...) wraps a body in the AltStart bytecode block. The innermost
        // body is the literal "x". A literal alternative is matched without an
        // engine re-entry, so only the outer negations (whose body is itself a
        // negation) recurse: N nested negations drive the native recursion to
        // depth N. MaxExtGlobDepth + 2 is comfortably past the guard's budget.
        int depth = GlobSpecification.MaxExtGlobDepth + 2;
        string program = BuildNestedNegationProgram(depth);

        CompiledGlobStrategy strategy = new(
            program,
            nfaProgramLength: program.Length,
            tailStart: -1,
            tailLength: 0,
            GlobTraits.ExtGlob | GlobTraits.Negation,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Action act = () => _ = strategy.MatchCore(default, "x".AsSpan());
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*recursion exceeded*");
    }

    // Builds a hand-crafted bytecode program of `depth` nested !(...) constructs
    // wrapping the literal "x". The AltStart header layout (see
    // CompiledGlobStrategy.ExtGlob) is
    // [AltStart][kind][blockLength][altCount][off_0] followed by the single
    // alternative body and a closing AltEnd. blockLength is the inclusive char
    // count from AltStart through AltEnd; off_0 is the body start relative to the
    // AltStart (always 5 for a single-alternative block: the 5-char header).
    private static string BuildNestedNegationProgram(int depth)
    {
        // Innermost body: a Literal opcode carrying the single character 'x'.
        string program = $"{GlobOpCodes.Literal}{(char)1}x";

        for (int i = 0; i < depth; i++)
        {
            int blockLength = 5 + program.Length + 1;
            program =
                $"{GlobOpCodes.AltStart}!{(char)blockLength}{(char)1}{(char)5}"
                + program
                + GlobOpCodes.AltEnd;
        }

        return program;
    }

    // -- Path-aware: negation can't cross the separator ------------------------------

    [Theory]
    // Bash dialect is path-aware. `!(foo)` operates within a single segment; the
    // construct's consumed slice can't cross '/'. An input containing a separator
    // doesn't match a separator-less pattern.
    [InlineData("!(foo)", "foo/bar", false)]
    [InlineData("!(foo)", "bar/baz", false)]
    [InlineData("dir/!(foo)", "dir/bar", true)]
    [InlineData("dir/!(foo)", "dir/foo", false)]
    public void Match_NegationPathAware(string pattern, string input, bool expected) =>
        Match(pattern, input).Should().Be(expected);

    // -- IgnoreCase ------------------------------------------------------------------

    [Theory]
    [InlineData("!(FOO)", "foo", false)]          // case-insensitive: foo matches FOO → reject
    [InlineData("!(FOO)", "bar", true)]
    [InlineData("!(FOO|BAR)", "baz", true)]
    public void Match_NegationIgnoreCase(string pattern, string input, bool expected) =>
        GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.IgnoreCase)
            .IsMatch(input).Should().Be(expected);
}
