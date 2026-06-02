// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Regression tests pinning down the encoder's handling of literal
///  characters that collide with the Unicode private-use noncharacter
///  block (<c>U+FDD0..U+FDD8</c>) that <see cref="GlobOpCodes"/> reserves
///  for opcodes. These chars are private-use noncharacters per the
///  Unicode standard but are nonetheless valid in arbitrary
///  <see cref="string"/> input, so the encoder and matcher must treat
///  them as ordinary chars when they appear in a pattern literal or
///  class body.
/// </summary>
/// <remarks>
///  <para>
///   The original trigger was PR #164 review feedback: the
///   <c>**/**</c>-collapse optimization had been peeking at the
///   encoder's buffer tail for <see cref="GlobOpCodes.GlobStar"/>
///   (<c>U+FDD5</c>), which a literal ending in that char could
///   accidentally satisfy. The fix uses an explicit
///   <c>LiteralCursor.GlobStar</c> sentinel instead. These tests
///   enumerate every adjacent collision shape we can construct against
///   the other opcode chars too, so any future optimization that
///   re-introduces a buffer peek will trip one of them.
///  </para>
/// </remarks>
public class GlobSpecificationOpcodeCharCollisionTests
{
    // GlobOpCodes from touki/Touki/Io/Globbing/GlobOpCodes.cs. Repeated
    // here as locals so a hypothetical reassignment in production code
    // breaks these tests instead of silently shifting the foot-gun.
    private const char Any = '\uFDD0';
    private const char AnyRun = '\uFDD1';
    private const char Literal = '\uFDD2';
    private const char Class = '\uFDD3';
    private const char NegClass = '\uFDD4';
    private const char GlobStar = '\uFDD5';
    private const char AltStart = '\uFDD6';
    private const char AltSep = '\uFDD7';
    private const char AltEnd = '\uFDD8';

    // (1) Reviewer's case: literal ending in GlobStar char, followed by '/**'.
    [Test]
    public void Compile_LiteralEndsInGlobStar_FollowedByDoubleStar_EmitsGlobStar()
    {
        GlobSpecification matcher = PosixPath($"foo{GlobStar}/**/bar");
        matcher.IsMatch($"foo{GlobStar}/bar").Should().BeTrue();
        matcher.IsMatch($"foo{GlobStar}/x/bar").Should().BeTrue();
        matcher.IsMatch($"foo{GlobStar}/x/y/bar").Should().BeTrue();
        matcher.IsMatch("foo/bar").Should().BeFalse();
    }

    // (2) Literal ending in every other opcode char, followed by '/**'.
    // Defensive coverage in case a future optimization peeks for any of
    // these.
    [Test]
    [Arguments(Any)]
    [Arguments(AnyRun)]
    [Arguments(Literal)]
    [Arguments(Class)]
    [Arguments(NegClass)]
    [Arguments(GlobStar)]
    [Arguments(AltStart)]
    [Arguments(AltSep)]
    [Arguments(AltEnd)]
    public void Compile_LiteralEndsInOpcodeChar_FollowedByDoubleStar_StillMatches(char opcodeChar)
    {
        GlobSpecification matcher = PosixPath($"foo{opcodeChar}/**/bar");
        matcher.IsMatch($"foo{opcodeChar}/bar").Should().BeTrue();
        matcher.IsMatch($"foo{opcodeChar}/x/bar").Should().BeTrue();
    }

    // (3) Pattern that is a bare literal opcode char (or a leading one
    // followed by other literals) round-trips through the encoder.
    [Test]
    [Arguments(Any)]
    [Arguments(AnyRun)]
    [Arguments(Literal)]
    [Arguments(Class)]
    [Arguments(NegClass)]
    [Arguments(GlobStar)]
    [Arguments(AltStart)]
    [Arguments(AltSep)]
    [Arguments(AltEnd)]
    public void Compile_BareLiteralOpcodeChar_RoundTrips(char opcodeChar)
    {
        GlobSpecification matcher = Posix(opcodeChar.ToString());
        matcher.IsMatch(opcodeChar.ToString()).Should().BeTrue();
        matcher.IsMatch("x").Should().BeFalse();
    }

    // (4) Class body containing every opcode char.
    [Test]
    [Arguments(Any)]
    [Arguments(AnyRun)]
    [Arguments(Literal)]
    [Arguments(Class)]
    [Arguments(NegClass)]
    [Arguments(GlobStar)]
    [Arguments(AltStart)]
    [Arguments(AltSep)]
    [Arguments(AltEnd)]
    public void Compile_ClassWithOpcodeChar_MatchesAndExcludes(char opcodeChar)
    {
        GlobSpecification matcher = Posix($"[a{opcodeChar}b]");
        matcher.IsMatch(opcodeChar.ToString()).Should().BeTrue();
        matcher.IsMatch("a").Should().BeTrue();
        matcher.IsMatch("b").Should().BeTrue();
        matcher.IsMatch("c").Should().BeFalse();
    }

    // (5) Negated class with opcode char.
    [Test]
    [Arguments(GlobStar)]
    [Arguments(AltStart)]
    [Arguments(Literal)]
    public void Compile_NegatedClassWithOpcodeChar_ExcludesIt(char opcodeChar)
    {
        GlobSpecification matcher = Posix($"[!{opcodeChar}]");
        matcher.IsMatch(opcodeChar.ToString()).Should().BeFalse();
        matcher.IsMatch("a").Should().BeTrue();
        matcher.IsMatch("z").Should().BeTrue();
    }

    // (6) Class whose body ends in GlobStar, followed by '/**'. Mirrors
    // the reviewer's case but for the Class opcode (whose body tail is
    // the position a buffer-peek collapse would see).
    [Test]
    public void Compile_ClassEndingInGlobStarChar_FollowedByDoubleStar_EmitsGlobStar()
    {
        GlobSpecification matcher = PosixPath($"[a{GlobStar}]/**/bar");
        matcher.IsMatch("a/bar").Should().BeTrue();
        matcher.IsMatch($"{GlobStar}/bar").Should().BeTrue();
        matcher.IsMatch("a/x/y/bar").Should().BeTrue();
        matcher.IsMatch("c/bar").Should().BeFalse();
    }

    // (7) Class body ending in two adjacent GlobStar chars (covers a
    // hypothetical [^2] peek even when [^1] is also the opcode char).
    [Test]
    public void Compile_ClassEndingInTwoGlobStarChars_FollowedByDoubleStar_EmitsGlobStar()
    {
        GlobSpecification matcher = PosixPath($"[a{GlobStar}{GlobStar}]/**/bar");
        matcher.IsMatch("a/bar").Should().BeTrue();
        matcher.IsMatch($"{GlobStar}/bar").Should().BeTrue();
        matcher.IsMatch($"{GlobStar}/x/bar").Should().BeTrue();
        matcher.IsMatch("z/bar").Should().BeFalse();
    }

    // (8) Any / AnyRun immediately before a '/**'. The encoder emits
    // these as single-char opcodes; a hypothetical optimization that
    // peeks at [^1] for a GlobStar must distinguish them.
    [Test]
    public void Compile_AnyRunFollowedByDoubleStar_BothEmitted()
    {
        GlobSpecification matcher = PosixPath("foo*/**/bar");
        matcher.IsMatch("fooX/bar").Should().BeTrue();
        matcher.IsMatch("fooX/x/y/bar").Should().BeTrue();
        // `*` matches the empty string, so "foo/" satisfies "foo*/".
        matcher.IsMatch("foo/bar").Should().BeTrue();
        matcher.IsMatch("xfoo/bar").Should().BeFalse();    // pattern starts at "foo"
    }

    [Test]
    public void Compile_AnyFollowedByDoubleStar_BothEmitted()
    {
        GlobSpecification matcher = PosixPath("foo?/**/bar");
        matcher.IsMatch("fooX/bar").Should().BeTrue();
        matcher.IsMatch("fooX/x/y/bar").Should().BeTrue();
        matcher.IsMatch("foo/bar").Should().BeFalse();
    }

    // (9) Multi-segment pattern with literal opcode chars mid-segment
    // -- after a '**'.
    [Test]
    public void Compile_DoubleStarFollowedByLiteralOpcodeChar_RoundTrips()
    {
        GlobSpecification matcher = PosixPath($"**/foo{GlobStar}bar");
        matcher.IsMatch($"foo{GlobStar}bar").Should().BeTrue();
        matcher.IsMatch($"x/foo{GlobStar}bar").Should().BeTrue();
        matcher.IsMatch($"x/y/foo{GlobStar}bar").Should().BeTrue();
        matcher.IsMatch("foobar").Should().BeFalse();
    }

    // (10) Extglob alternative whose body ends in GlobStar char.
    [Test]
    public void Compile_ExtGlobAlternativeEndsInGlobStarChar_FollowedByDoubleStar_EmitsGlobStar()
    {
        GlobSpecification matcher = GlobSpecification.Compile(
            $"@(foo|bar{GlobStar})/**/baz",
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob | GlobOptions.MatchLeadingDot);

        matcher.IsMatch("foo/baz").Should().BeTrue();
        matcher.IsMatch($"bar{GlobStar}/baz").Should().BeTrue();
        matcher.IsMatch("foo/x/y/baz").Should().BeTrue();
        matcher.IsMatch("nope/baz").Should().BeFalse();
    }

    // (11) Class header (`[...]`) immediately followed by '/**'. The
    // class' bytecode header is `Class + length` where `length` is a
    // single char; in the worst case a 0xFDD5-length class body could
    // accidentally produce the byte pattern `Class + GlobStar-char`.
    // 0xFDD5 = 64981 chars is a large class body but well within the
    // MaxOpcodeBodyLength (0xFFFF) limit. Building a literal pattern
    // with 64981 distinct chars is large but constructable; here we
    // exercise the easier case of any class followed by '/**' to lock
    // in correct behavior, and let the tail-of-class collision tests
    // above cover the malicious shape.
    [Test]
    public void Compile_ClassFollowedByDoubleStar_BothEmitted()
    {
        GlobSpecification matcher = PosixPath("[abc]/**/x");
        matcher.IsMatch("a/x").Should().BeTrue();
        matcher.IsMatch("b/y/x").Should().BeTrue();
        matcher.IsMatch("c/y/z/x").Should().BeTrue();
        matcher.IsMatch("d/x").Should().BeFalse();
    }

    // (12) Three adjacent globstars where the first absorbs a `/` from a
    // prior literal containing GlobStar char. Regression for the
    // interaction between the GlobStar-cursor collapse and the
    // strip-trailing-separator-from-prior-Literal logic.
    [Test]
    public void Compile_LiteralWithGlobStarChar_ThenTripleGlobStarChain_AllCollapse()
    {
        GlobSpecification matcher = PosixPath($"foo{GlobStar}/**/**/**/bar");
        matcher.IsMatch($"foo{GlobStar}/bar").Should().BeTrue();
        matcher.IsMatch($"foo{GlobStar}/x/bar").Should().BeTrue();
        matcher.IsMatch($"foo{GlobStar}/x/y/z/bar").Should().BeTrue();
        matcher.IsMatch("foo/bar").Should().BeFalse();
    }

    // (13) Single GlobStar where the input also contains the GlobStar
    // opcode char. Validates that the runtime walker doesn't accidentally
    // confuse the input.
    [Test]
    public void IsMatch_GlobStarPattern_InputContainsGlobStarChar_Matches()
    {
        GlobSpecification matcher = PosixPath("**/bar");
        matcher.IsMatch($"foo{GlobStar}/bar").Should().BeTrue();
        matcher.IsMatch($"{GlobStar}/bar").Should().BeTrue();
        matcher.IsMatch($"foo/{GlobStar}/bar").Should().BeTrue();
        matcher.IsMatch($"foo{GlobStar}bar").Should().BeFalse();
    }

    private static GlobSpecification Posix(string pattern) =>
        GlobSpecification.Compile(pattern, GlobDialect.Posix, GlobOptions.MatchLeadingDot);

    private static GlobSpecification PosixPath(string pattern) =>
        GlobSpecification.Compile(
            pattern,
            GlobDialect.PosixPath,
            GlobOptions.AllowGlobStar | GlobOptions.MatchLeadingDot);
}
