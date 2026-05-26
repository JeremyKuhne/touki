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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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
}
