// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io.Globbing;

/// <summary>
///  Tests for the scanner- and encoder-side handling of extended-glob constructs
///  (<c>?(…)</c>, <c>*(…)</c>, <c>+(…)</c>, <c>@(…)</c>, <c>!(…)</c>). The
///  scanner validates balanced parens, depth, and alternative count; the encoder
///  emits <see cref="GlobOpCodes.AltStart"/> / <see cref="GlobOpCodes.AltSep"/> /
///  <see cref="GlobOpCodes.AltEnd"/> for each construct.
/// </summary>
/// <remarks>
///  <para>
///   The interpreter does not yet recognize the new opcodes - that comes in
///   step 3 of the F1.3 rollout. Until then runtime matching against an extglob
///   pattern silently returns <see langword="false"/>, which the compile-shape
///   tests in this file <i>don't</i> exercise (they inspect the emitted bytecode
///   directly via <c>TestAccessor</c>).
///  </para>
/// </remarks>
public class ExtGlobScannerTests
{
    // -- AllowExtGlob off: '(' / ')' are literals --------------------------------

    [Theory]
    [InlineData("(foo)")]
    [InlineData("?(foo)")]
    [InlineData("*(a|b)")]
    [InlineData("@(only|alt)")]
    public void Compile_AllowExtGlobOff_ParensAreLiteral(string pattern)
    {
        // Without AllowExtGlob the scanner never inspects '('. The pattern
        // compiles to whatever the rest of the surface produces (here the
        // simple/PowerShell dialects treat the parens as literal characters).
        Action act = () => GlobSpecification.Compile(pattern, GlobDialect.Simple);
        act.Should().NotThrow();
    }

    // -- AllowExtGlob on, well-formed: encoder emits Alt* opcodes ----------------

    [Theory]
    [InlineData("?(foo)")]
    [InlineData("*(a)")]
    [InlineData("+(a)")]
    [InlineData("@(a|b)")]
    [InlineData("!(a)")]
    [InlineData("@(foo|bar|baz)")]
    [InlineData("foo@(a|b)bar")]
    [InlineData("*(a|@(b|c))d")]
    [InlineData("@(|)")]              // single explicitly-empty alternative
    [InlineData("@(a|)")]             // trailing empty alternative
    [InlineData("@(|a)")]             // leading empty alternative
    [InlineData("@(a|b|c|d|e|f|g|h)")] // 8 alternatives, under cap
    public void Compile_AllowExtGlobOn_WellFormed_CompilesToCompiledStrategy(string pattern)
    {
        GlobSpecification spec = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        object strategy = spec.TestAccessor.Dynamic._strategy;
        strategy.Should().BeOfType<CompiledGlobStrategy>();
    }

    [Theory]
    [InlineData("@(a|b)")]
    [InlineData("*(a|b|c)")]
    [InlineData("foo@(x|y)bar")]
    public void Compile_AllowExtGlobOn_EmitsAltOpcodes(string pattern)
    {
        GlobSpecification spec = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        object strategy = spec.TestAccessor.Dynamic._strategy;
        string program = (string)strategy.TestAccessor.Dynamic._program;
        program.Should().Contain(GlobOpCodes.AltStart.ToString());
        program.Should().Contain(GlobOpCodes.AltSep.ToString());
        program.Should().Contain(GlobOpCodes.AltEnd.ToString());
    }

    [Theory]
    // Single alternative -> AltStart ... AltEnd with no AltSep.
    [InlineData("@(only)", '@')]
    [InlineData("?(x)", '?')]
    [InlineData("*(y)", '*')]
    [InlineData("+(z)", '+')]
    [InlineData("!(n)", '!')]
    public void Compile_AllowExtGlobOn_SingleAlternative_EmitsKindAndNoSeparator(string pattern, char kind)
    {
        GlobSpecification spec = GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        object strategy = spec.TestAccessor.Dynamic._strategy;
        string program = (string)strategy.TestAccessor.Dynamic._program;
        int altStartIndex = program.IndexOf(GlobOpCodes.AltStart);
        altStartIndex.Should().BeGreaterThanOrEqualTo(0);

        // AltStart payload layout: [opcode][kind][blockLen].
        program[altStartIndex + 1].Should().Be(kind);

        // No AltSep for single-alternative constructs.
        program.Should().NotContain(GlobOpCodes.AltSep.ToString());
        program.Should().Contain(GlobOpCodes.AltEnd.ToString());
    }

    [Fact]
    public void Compile_AllowExtGlobOn_AltStartBlockLength_CoversThroughAltEnd()
    {
        GlobSpecification spec = GlobSpecification.Compile(
            "@(a|b)",
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        object strategy = spec.TestAccessor.Dynamic._strategy;
        string program = (string)strategy.TestAccessor.Dynamic._program;
        int altStartIndex = program.IndexOf(GlobOpCodes.AltStart);
        int altEndIndex = program.LastIndexOf(GlobOpCodes.AltEnd);
        altStartIndex.Should().BeGreaterThanOrEqualTo(0);
        altEndIndex.Should().BeGreaterThan(altStartIndex);

        // Block length payload is the third char of the AltStart header. Per
        // contract it covers the span from AltStart through AltEnd inclusive.
        int blockLength = program[altStartIndex + 2];
        blockLength.Should().Be(altEndIndex - altStartIndex + 1);
    }

    [Fact]
    public void Compile_AllowExtGlobOn_Nested_EmitsTwoAltStarts()
    {
        GlobSpecification spec = GlobSpecification.Compile(
            "*(a|@(b|c))",
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        object strategy = spec.TestAccessor.Dynamic._strategy;
        string program = (string)strategy.TestAccessor.Dynamic._program;
        int outerStart = program.IndexOf(GlobOpCodes.AltStart);
        int innerStart = program.IndexOf(GlobOpCodes.AltStart, outerStart + 1);
        outerStart.Should().BeGreaterThanOrEqualTo(0);
        innerStart.Should().BeGreaterThan(outerStart);
        program[outerStart + 1].Should().Be('*');
        program[innerStart + 1].Should().Be('@');

        // Two AltEnd markers, the inner one closing first.
        int innerEnd = program.IndexOf(GlobOpCodes.AltEnd);
        int outerEnd = program.LastIndexOf(GlobOpCodes.AltEnd);
        innerEnd.Should().BeGreaterThan(innerStart);
        outerEnd.Should().BeGreaterThan(innerEnd);

        // Outer block length wraps the inner block.
        int outerBlockLength = program[outerStart + 2];
        outerBlockLength.Should().Be(outerEnd - outerStart + 1);

        int innerBlockLength = program[innerStart + 2];
        innerBlockLength.Should().Be(innerEnd - innerStart + 1);
    }

    [Fact]
    public void Compile_AllowExtGlobOn_EmptyAlternativesEmitConsecutiveSeparators()
    {
        GlobSpecification spec = GlobSpecification.Compile(
            "@(|a|)",
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        object strategy = spec.TestAccessor.Dynamic._strategy;
        string program = (string)strategy.TestAccessor.Dynamic._program;

        // Two `|` separators in source -> two AltSep opcodes.
        int sep1 = program.IndexOf(GlobOpCodes.AltSep);
        int sep2 = program.IndexOf(GlobOpCodes.AltSep, sep1 + 1);
        sep1.Should().BeGreaterThan(0);
        sep2.Should().BeGreaterThan(sep1);
    }

    // -- AllowExtGlob on, malformed: scanner-side errors -------------------------

    [Theory]
    [InlineData("?()")]
    [InlineData("*()")]
    [InlineData("+()")]
    [InlineData("@()")]
    [InlineData("!()")]
    public void Compile_AllowExtGlobOn_EmptyBody_ReportsInvalidExtGlobBody(string pattern)
    {
        Action act = () => GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.InvalidExtGlobBody);
    }

    [Theory]
    [InlineData("?(foo")]
    [InlineData("*(a|b")]
    [InlineData("@(a|b|c")]
    [InlineData("!(a")]
    [InlineData("@(a|@(b|c)")] // outer unterminated, inner closed
    public void Compile_AllowExtGlobOn_Unterminated_ReportsUnterminatedExtGlob(string pattern)
    {
        Action act = () => GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.UnterminatedExtGlob);
    }

    [Fact]
    public void Compile_AllowExtGlobOn_NestingDepthCapExceeded_ReportsFeatureLimitExceeded()
    {
        // 9 levels of nesting; cap is 8.
        const string pattern = "@(@(@(@(@(@(@(@(@(a))))))))))";
        Action act = () => GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.FeatureLimitExceeded);
    }

    [Fact]
    public void Compile_AllowExtGlobOn_AlternativeCountCapExceeded_ReportsFeatureLimitExceeded()
    {
        // 33 alternatives; cap is 32. Build via string concat.
        System.Text.StringBuilder sb = new();
        sb.Append("@(");
        for (int j = 0; j < 33; j++)
        {
            if (j > 0)
            {
                sb.Append('|');
            }
            sb.Append((char)('a' + (j % 26)));
        }
        sb.Append(')');

        Action act = () => GlobSpecification.Compile(
            sb.ToString(),
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.FeatureLimitExceeded);
    }

    [Fact]
    public void Compile_AllowExtGlobOn_AlternativeCountAtCap_Compiles()
    {
        // Exactly 32 alternatives; cap allows this.
        System.Text.StringBuilder sb = new();
        sb.Append("@(");
        for (int j = 0; j < 32; j++)
        {
            if (j > 0)
            {
                sb.Append('|');
            }
            sb.Append((char)('a' + (j % 26)));
        }
        sb.Append(')');

        Action act = () => GlobSpecification.Compile(
            sb.ToString(),
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        act.Should().NotThrow();
    }

    [Fact]
    public void Compile_AllowExtGlobOn_NestingDepthAtCap_Compiles()
    {
        // Exactly 8 levels of nesting; cap allows this.
        const string pattern = "@(@(@(@(@(@(@(@(a))))))))";
        Action act = () => GlobSpecification.Compile(
            pattern,
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        act.Should().NotThrow();
    }

    [Fact]
    public void Compile_AllowExtGlobOn_DanglingEscapeInsideBody_ReportsDanglingEscape()
    {
        // Bash-style escape (`\`) inside an extglob body with nothing to escape.
        Action act = () => GlobSpecification.Compile(
            @"@(a|\",
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        act.Should().Throw<GlobFormatException>()
            .Which.Error.Code.Should().Be(GlobCompileErrorCode.DanglingEscape);
    }

    [Fact]
    public void Compile_AllowExtGlobOn_UnterminatedClassInsideBody_TreatedAsLiteral()
    {
        // Per fnmatch semantics: an unterminated '[' is treated as a literal
        // character. The extglob body inherits the same behavior so the pattern
        // compiles successfully and the '[bc' alternative becomes a literal run.
        GlobSpecification matcher = GlobSpecification.Compile(
            "@(a|[bc)",
            GlobDialect.Bash,
            GlobOptions.AllowGlobStar | GlobOptions.AllowExtGlob);

        matcher.IsMatch("a").Should().BeTrue();
        matcher.IsMatch("[bc").Should().BeTrue();
    }
}
