// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

public partial class GlobSpecificationTests
{
    // --- Security / denial-of-service regression tests ---
    //
    // The matcher uses a two-pointer algorithm with a single AnyRun savepoint and a
    // single GlobStar savepoint (CompiledGlobStrategy.MatchOrdinal / MatchIgnoreCase /
    // Backtrack). That bounds the worst case to O(n * m) instead of the exponential
    // catastrophic backtracking that classical regex engines exhibit on patterns like
    // /(a+)+$/. These tests pin that property so a future refactor can't silently
    // reintroduce a ReDoS surface.

    [TestMethod]
    public void IsMatch_PathologicalAnyRun_TerminatesPromptly()
    {
        // 16 `*a` repetitions then a literal that doesn't exist in the input would be
        // exponential under a naive backtracker. With the two-pointer savepoint this
        // is bounded by O(input.Length * pattern.Length).
        string pattern = string.Concat(Enumerable.Repeat("*a", 16)) + "*b";
        string input = new('a', 4096);

        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.Posix);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_PathologicalGlobStar_TerminatesPromptly()
    {
        // Many `**/` segments against an input that ultimately can't match must not
        // blow up. PosixPath supports `**` natively; the GlobStar savepoint slot is
        // shared so each new `**` overwrites the prior one, keeping the search linear.
        string pattern = string.Concat(Enumerable.Repeat("**/", 16)) + "missing";
        string input = string.Concat(Enumerable.Repeat("dir/", 64)) + "actual.cs";

        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.PosixPath);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_AlternatingAnyRunAndGlobStar_TerminatesPromptly()
    {
        // Mixing `*` (AnyRun) and `**` (GlobStar) exercises both savepoint slots.
        // The non-matching tail forces the matcher to exhaust both before returning
        // false; verify it does so in linear-bounded time.
        string pattern = "**/*/**/*/**/*/**/*/missing.cs";
        string input = string.Concat(Enumerable.Repeat("seg/", 128)) + "real.cs";

        GlobSpecification matcher = GlobSpecification.Compile(pattern, GlobDialect.PosixPath, GlobOptions.AllowGlobStar);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_PathologicalExtGlobRepetition_TerminatesPromptly()
    {
        // Catastrophic backtracking in the recursive extglob walker (fuzz finding
        // timeout-glob-001). The repeating `+(...)` block contains two alternatives
        // that both match empty (`*`), and a trailing run of plain `*` strips the
        // literal tail-anchor prefilter so the walker actually runs. Without failure
        // memoization this explodes to billions of TryMatchRanges calls against empty
        // input; the lazily-engaged failure memo bounds it to polynomial time.
        string pattern = "+(*|}}+|*)a]" + new string('*', 26);
        GlobSpecification matcher = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch("");
        sw.Stop();

        result.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_MinimalExtGlobRepetition_TerminatesPromptly()
    {
        // Smallest pattern that reproduces the extglob catastrophic backtracking:
        // a repeating `+(...)` with two empty-matchable alternatives (`*|*`), a
        // literal `y` that can never match the empty input (forcing exhaustive
        // backtracking), and a trailing `*` that defeats the literal tail-anchor
        // prefilter so the walker actually runs. Without the failure memo this
        // explodes to tens of millions of TryMatchRanges calls; with it the
        // distinct entry states (~constant) are each explored once.
        string pattern = "+(*|*)y*";
        GlobSpecification matcher = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch("");
        sw.Stop();

        result.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_DeepExtGlobRepetition_DoesNotStackOverflow()
    {
        // StackOverflow DOS regression. The previous recursive extglob walker
        // descended one native frame per consumed `+(...)` iteration, so a long
        // separator-free run made recursion depth O(input length) and overflowed
        // the (uncatchable) call stack well before this size. The iterative engine
        // keeps every choice point on an explicit pooled stack, so depth is a heap
        // concern and this matches in linear time without touching the call stack.
        string pattern = "+(a)";
        string input = new string('a', 200_000);

        GlobSpecification matcher = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_DeepExtGlobRepetitionNonMatch_DoesNotStackOverflow()
    {
        // Companion to the matching case: the same deep repetition against an input
        // whose final character defeats the match forces the engine to walk the full
        // depth and then fail, again without native recursion.
        string pattern = "+(a)*";
        string input = new string('a', 200_000) + "b";

        GlobSpecification matcher = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        // `+(a)*` matches: the trailing `*` absorbs the 'b'.
        result.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_PathologicalExtGlobNegation_TerminatesPromptly()
    {
        // Companion to the `+(...)` repetition cases above, for the `!(...)` negation
        // operator (fuzz finding: a FileSystemGlobbing pattern of the shape
        // `//!(**@**?*...)*` against a long run of metacharacters took ~0.5s). The
        // negated body packs several empty-matchable `**`/`*` runs around a literal so
        // the engine must explore many interior split points, and the trailing `*`
        // defeats the literal tail-anchor prefilter so the walker actually runs. The
        // iterative engine with failure memoization bounds this to polynomial time.
        string pattern = "!(**@**?*)" + new string('*', 24);
        string input = new string('@', 128);

        GlobSpecification matcher = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_LongInputAgainstWildcard_IsLinear()
    {
        // Bare `*` against a long input must walk the input once. Anything worse
        // would indicate the matcher is doing unnecessary re-scans.
        GlobSpecification matcher = GlobSpecification.Compile("*", GlobDialect.Posix);
        string input = new('x', 1_000_000);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeTrue();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_EmptyInput_DoesNotDereferenceNullSpan()
    {
        // MatchOrdinal walks the input via ReadOnlySpan indexer; empty input must
        // short-circuit before any indexed read. Regression guard for the
        // MemoryMarshal.GetReference-on-empty-span foot-gun documented in
        // pre-pr-self-review.
        GlobSpecification anyRun = GlobSpecification.Compile("*", GlobDialect.Posix);
        GlobSpecification literal = GlobSpecification.Compile("abc", GlobDialect.Posix);
        GlobSpecification klass = GlobSpecification.Compile("[abc]", GlobDialect.Posix);

        anyRun.IsMatch([]).Should().BeTrue();
        literal.IsMatch([]).Should().BeFalse();
        klass.IsMatch([]).Should().BeFalse();
    }

    [TestMethod]
    public void Compile_LiteralBodyOverflow_ReturnsPatternTooLarge()
    {
        // 4A guard: a Literal opcode body stores its length in a single char header.
        // Patterns that would require a literal run > char.MaxValue characters must be
        // rejected with PatternTooLarge instead of silently truncating the length.
        // Sandwich the giant literal between two '?'s to force the bytecode-encoder
        // path; pure-literal and simple prefix/suffix/contains shapes take specialized
        // matchers that store the literal as a plain string and never emit a Literal
        // opcode header.
        string giant = new string('a', char.MaxValue + 1);
        string pattern = "?" + giant + "?";

        bool ok = GlobSpecification.TryCompile(
            pattern,
            GlobDialect.Posix,
            GlobOptions.None,
            out GlobSpecification? result,
            out GlobCompileError error);

        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.PatternTooLarge);

        // Boundary: a literal exactly at the limit compiles cleanly.
        string atLimit = "?" + new string('a', char.MaxValue) + "?";
        GlobSpecification.TryCompile(atLimit, GlobDialect.Posix, GlobOptions.None, out _, out _)
            .Should().BeTrue();
    }

    [TestMethod]
    public void Compile_ClassBodyOverflow_ReturnsPatternTooLarge()
    {
        // 4A guard: same length-header constraint applies to Class/NegClass bodies.
        // Stuffing a bracket with more than char.MaxValue characters must error out.
        string body = new('a', char.MaxValue + 1);
        string pattern = $"[{body}]";

        bool ok = GlobSpecification.TryCompile(
            pattern,
            GlobDialect.Posix,
            GlobOptions.None,
            out GlobSpecification? result,
            out GlobCompileError error);

        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.PatternTooLarge);
    }

    [TestMethod]
    public void Compile_MaxPatternLengthExceeded_ReturnsPatternTooLarge()
    {
        // 5B: caller-supplied upper bound on pattern length. Patterns longer than the
        // limit are rejected before any encoding work happens.
        string pattern = new('a', 2048);

        bool ok = GlobSpecification.TryCompile(
            pattern,
            GlobDialect.Posix,
            GlobOptions.None,
            GlobPathSeparator.DialectDefault,
            maxPatternLength: 1024,
            out GlobSpecification? result,
            out GlobCompileError error);

        ok.Should().BeFalse();
        result.Should().BeNull();
        error.Code.Should().Be(GlobCompileErrorCode.PatternTooLarge);
    }

    [TestMethod]
    public void Compile_MaxPatternLengthAtLimit_Succeeds()
    {
        // 5B boundary: pattern length equal to the limit must be accepted.
        string pattern = new('a', 1024);

        bool ok = GlobSpecification.TryCompile(
            pattern,
            GlobDialect.Posix,
            GlobOptions.None,
            GlobPathSeparator.DialectDefault,
            maxPatternLength: 1024,
            out GlobSpecification? result,
            out _);

        ok.Should().BeTrue();
        result.Should().NotBeNull();
    }

    [TestMethod]
    public void Compile_MaxPatternLengthNegative_DisablesCheck()
    {
        // 5B opt-in: -1 (the default) leaves the cap disabled.
        string pattern = new('a', 8192);

        GlobSpecification.TryCompile(
            pattern,
            GlobDialect.Posix,
            GlobOptions.None,
            GlobPathSeparator.DialectDefault,
            maxPatternLength: -1,
            out GlobSpecification? result,
            out _).Should().BeTrue();

        result.Should().NotBeNull();
    }

    [TestMethod]
    public void Compile_MaxPatternLengthExceeded_ThrowsGlobFormatException()
    {
        // 5B via the throwing Compile overload: oversized pattern surfaces as
        // GlobFormatException carrying GlobCompileErrorCode.PatternTooLarge.
        string pattern = new('a', 2048);

        Action act = () => GlobSpecification.Compile(
            pattern,
            GlobDialect.Posix,
            GlobOptions.None,
            GlobPathSeparator.DialectDefault,
            maxPatternLength: 1024);

        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.PatternTooLarge);
    }

    [TestMethod]
    public void IsMatch_NoncharacterOpcodesInInput_AreTreatedAsLiteralData()
    {
        // GlobOpCodes uses U+FDD0..U+FDD5 (Unicode noncharacters) as bytecode markers
        // inside the compiled program. If those code points appear in *input* they
        // must compare as ordinary characters and never be confused with opcodes.
        // Regression guard: a literal pattern containing these code points must
        // match an input that has them and reject one that doesn't.
        string opcodeChars = "\uFDD0\uFDD1\uFDD2\uFDD3\uFDD4\uFDD5";
        GlobSpecification matcher = GlobSpecification.Compile(opcodeChars, GlobDialect.Posix);

        matcher.IsMatch(opcodeChars).Should().BeTrue();
        matcher.IsMatch("anything-else").Should().BeFalse();
    }

    [TestMethod]
    public void IsMatch_ManyLiveExtGlobChoicePoints_GrowsPooledStacks()
    {
        // The iterative engine seeds its frame and arena (range-snapshot) stacks
        // with fixed stack buffers (32 frames / 128 arena entries); overflowing
        // either must transparently rent a larger backing array from the pool and
        // copy the existing contents. A long run of independent two-way `@(a|b)`
        // constructs keeps one live choice-point frame (plus its snapshot) per
        // construct simultaneously, so 200 of them forces both stacks well past
        // their seeds. The required `z` then fails on the all-'a' input; the
        // trailing `*` defeats the literal tail-anchor prefilter so the walker
        // actually runs instead of being rejected up front. Asserts the growth
        // path runs and still returns the correct answer.
        string pattern = string.Concat(Enumerable.Repeat("@(a|b)", 200)) + "z*";
        string input = new('a', 200);

        GlobSpecification matcher = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public void IsMatch_AmbiguousConsumingRepetition_MemoizesRecurringFailures()
    {
        // `+(a|aa)` can tile a run of 'a's in exponentially many ways (each
        // position may consume one or two characters), and unlike the empty-
        // matchable `*|*` cases the alternatives genuinely consume input, so the
        // progress guard cannot collapse them. A required `y` that the all-'a'
        // input can never satisfy forces the engine to disprove every reachable
        // (position, continuation) state, and the trailing `*` defeats the literal
        // tail-anchor prefilter so the walker actually runs. Without the failure
        // memo this is exponential; the memo records each distinct failed state
        // once and re-hits it on every later path that reaches the same state,
        // bounding the work to linear. Prompt termination on a 64-char input pins
        // that the record-and-re-hit cycle actually engages.
        string pattern = "+(a|aa)y*";
        string input = new('a', 64);

        GlobSpecification matcher = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        Stopwatch sw = Stopwatch.StartNew();
        bool result = matcher.IsMatch(input);
        sw.Stop();

        result.Should().BeFalse();
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
    }
}
