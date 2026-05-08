// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using Touki.Text;

namespace Framework.System;

/// <summary>
///  Drives the Number polyfill's floating-point and decimal formatting
///  paths broadly to exercise <c>Number.Formatting.cs</c>,
///  <c>Number.Grisu3.cs</c>, <c>Number.Dragon4Double.cs</c>,
///  <c>Number.NumberBuffer.cs</c>, <c>Number.Parsing.cs</c>, and
///  <c>Number.NumberToFloatingPointBits.cs</c>. Tests primarily assert
///  that the formatter produces non-empty output, returns the documented
///  NaN / Infinity symbols for non-finite inputs, and that the <c>R</c>
///  format roundtrips back to the original value. Direct comparison
///  against the BCL is avoided because net481's legacy double formatter
///  diverges from the modern formatter that this polyfill mirrors.
/// </summary>
public unsafe class NumberFormattingTests
{
    private static readonly NumberFormatInfo s_invariant = CultureInfo.InvariantCulture.NumberFormat;

    private static string FormatDouble(double value, string format)
    {
        ValueStringBuilder builder = new(stackalloc char[256]);
        try
        {
            Number.FormatDouble(value, ref builder, format.AsSpan(), s_invariant);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static string FormatSingle(float value, string format)
    {
        ValueStringBuilder builder = new(stackalloc char[256]);
        try
        {
            Number.FormatSingle(value, ref builder, format.AsSpan(), s_invariant);
            return builder.ToString();
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static string FormatDecimal(decimal value, string format)
    {
        Span<char> destination = stackalloc char[256];
        Number.TryFormatDecimal(value, format.AsSpan(), s_invariant, destination, out int charsWritten)
            .Should().BeTrue();
        return destination[..charsWritten].ToString();
    }

    public static IEnumerable<object[]> StandardDoubleData()
    {
        double[] values =
        [
            0.0,
            -0.0,
            1.0,
            -1.0,
            0.5,
            -0.5,
            1.5,
            2.5,
            123.456,
            -123.456,
            0.000123,
            1234567890.12345,
            1e-100,
            1e100,
            1e-300,
            1e300,
            double.Epsilon,
            -double.Epsilon,
            double.MaxValue,
            double.MinValue,
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.NaN,
        ];

        string[] formats =
        [
            "",
            "G",
            "G0",
            "G1",
            "G7",
            "G15",
            "G17",
            "g",
            "g3",
            "R",
            "r",
            "F",
            "F0",
            "F2",
            "F6",
            "f4",
            "N",
            "N0",
            "N3",
            "n2",
            "C",
            "C2",
            "c4",
            "E",
            "E0",
            "E3",
            "E10",
            "e2",
            "P",
            "P0",
            "P2",
            "p3",
        ];

        foreach (double value in values)
        {
            foreach (string format in formats)
            {
                yield return new object[] { value, format };
            }
        }
    }

    [Theory]
    [MemberData(nameof(StandardDoubleData))]
    public void FormatDouble_StandardSpecifiers_ProducesNonEmptyOutput(double value, string format)
    {
        string result = FormatDouble(value, format);
        result.Should().NotBeNull();

        if (double.IsNaN(value))
        {
            result.Should().Be(s_invariant.NaNSymbol);
        }
        else if (double.IsPositiveInfinity(value))
        {
            result.Should().Be(s_invariant.PositiveInfinitySymbol);
        }
        else if (double.IsNegativeInfinity(value))
        {
            result.Should().Be(s_invariant.NegativeInfinitySymbol);
        }
        else
        {
            result.Length.Should().BeGreaterThan(0);
        }
    }

    public static IEnumerable<object[]> StandardSingleData()
    {
        float[] values =
        [
            0f,
            -0f,
            1f,
            -1f,
            0.5f,
            123.456f,
            -123.456f,
            1e-30f,
            1e30f,
            float.Epsilon,
            float.MaxValue,
            float.MinValue,
            float.PositiveInfinity,
            float.NegativeInfinity,
            float.NaN,
        ];

        string[] formats =
        [
            "",
            "G",
            "G7",
            "G9",
            "R",
            "r",
            "F",
            "F2",
            "F6",
            "N",
            "N3",
            "C",
            "C2",
            "E",
            "E3",
            "P",
            "P2",
        ];

        foreach (float value in values)
        {
            foreach (string format in formats)
            {
                yield return new object[] { value, format };
            }
        }
    }

    [Theory]
    [MemberData(nameof(StandardSingleData))]
    public void FormatSingle_StandardSpecifiers_ProducesNonEmptyOutput(float value, string format)
    {
        string result = FormatSingle(value, format);
        result.Should().NotBeNull();

        if (float.IsNaN(value))
        {
            result.Should().Be(s_invariant.NaNSymbol);
        }
        else if (float.IsPositiveInfinity(value))
        {
            result.Should().Be(s_invariant.PositiveInfinitySymbol);
        }
        else if (float.IsNegativeInfinity(value))
        {
            result.Should().Be(s_invariant.NegativeInfinitySymbol);
        }
        else
        {
            result.Length.Should().BeGreaterThan(0);
        }
    }

    public static IEnumerable<object[]> CustomDoubleFormatData()
    {
        (double Value, string Format)[] cases =
        [
            (0.0, "0.##"),
            (1.5, "0.##"),
            (1.5, "0.00"),
            (0.001, "0.000"),
            (12345.6789, "#,##0.00"),
            (-12345.6789, "#,##0.00"),
            (12345.6789, "#,##0.0;(#,##0.0)"),
            (-12345.6789, "#,##0.0;(#,##0.0)"),
            (0.0, "+0;-0;zero"),
            (1.0, "+0;-0;zero"),
            (-1.0, "+0;-0;zero"),
            (1234.5, "0.00E+00"),
            (1234.5, "0.00e+0"),
            (1234.5, "0.00E-00"),
            (0.5, "0.0%"),
            (0.005, "0.000\u2030"),
            (12.34, @"0\.0"),
            (12.34, "'literal'0.0"),
            (12.34, "\"literal\"0.0"),
            (12.34, "0.0;-0.0;0.0"),
            (1, "00000"),
            (-1, "00000"),
            (1234567, "###,###"),
            (0.123, ".000"),
            (0.123, "0."),
            (1.0, "0,,"),
            (1234567.89, "0,,.##"),
        ];

        foreach ((double value, string format) in cases)
        {
            yield return new object[] { value, format };
        }
    }

    [Theory]
    [MemberData(nameof(CustomDoubleFormatData))]
    public void FormatDouble_CustomFormats_ProducesNonEmptyOutput(double value, string format)
    {
        string result = FormatDouble(value, format);
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    public static IEnumerable<object[]> DecimalData()
    {
        decimal[] values =
        [
            0m,
            1m,
            -1m,
            0.5m,
            -0.5m,
            123.456m,
            -123.456m,
            decimal.MaxValue,
            decimal.MinValue,
            0.0000000000000000000000000001m,
            79228162514264337593543950335m,
        ];

        string[] formats =
        [
            "",
            "G",
            "G0",
            "G10",
            "F",
            "F0",
            "F4",
            "N",
            "N2",
            "C",
            "C3",
            "E",
            "E5",
            "P",
            "P2",
            "0.##",
            "#,##0.00",
            "0.00E+0",
            "+0;-0;zero",
        ];

        foreach (decimal value in values)
        {
            foreach (string format in formats)
            {
                yield return new object[] { value, format };
            }
        }
    }

    [Theory]
    [MemberData(nameof(DecimalData))]
    public void FormatDecimal_VariousFormats_ProducesNonEmptyOutput(decimal value, string format)
    {
        string result = FormatDecimal(value, format);
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("z")]
    [InlineData("Z2")]
    public void FormatDouble_InvalidStandardSpecifier_Throws(string format)
    {
        Action action = () => FormatDouble(1.0, format);
        action.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("Z")]
    [InlineData("z2")]
    public void FormatSingle_InvalidStandardSpecifier_Throws(string format)
    {
        Action action = () => FormatSingle(1.0f, format);
        action.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("", -1, 'G')]
    [InlineData("G", -1, 'G')]
    [InlineData("g", -1, 'g')]
    [InlineData("F2", 2, 'F')]
    [InlineData("F12", 12, 'F')]
    [InlineData("F123", 123, 'F')]
    [InlineData("F0001", 1, 'F')]
    [InlineData("0.##", -1, '\0')]
    [InlineData("#,##0", -1, '\0')]
    [InlineData("\0", -1, 'G')]
    public void ParseFormatSpecifier_VariousInputs_ReturnsExpected(string format, int expectedDigits, char expectedChar)
    {
        char actual = Number.ParseFormatSpecifier(format.AsSpan(), out int digits);
        actual.Should().Be(expectedChar);
        digits.Should().Be(expectedDigits);
    }

    [Fact]
    public void ParseFormatSpecifier_OverflowDigits_Throws()
    {
        // A long run of digits whose accumulator overflows int.
        string format = "F" + new string('9', 12);
        Action action = () => Number.ParseFormatSpecifier(format.AsSpan(), out _);
        action.Should().Throw<FormatException>();
    }

    [Theory]
    [InlineData("12345", 5, false, 12345.0)]
    [InlineData("12345", 5, true, -12345.0)]
    [InlineData("1", 1, false, 1.0)]
    [InlineData("1", 0, false, 0.1)]
    [InlineData("1", -1, false, 0.01)]
    [InlineData("5", 1, false, 5.0)]
    [InlineData("123456789", 9, false, 123456789.0)]
    public void NumberToDouble_ValidBuffer_RoundtripsExactly(string digits, int scale, bool negative, double expected)
    {
        byte* pDigits = stackalloc byte[Number.DoubleNumberBufferLength];
        Number.NumberBuffer number = new(Number.NumberBufferKind.FloatingPoint, pDigits, Number.DoubleNumberBufferLength);
        FillDigits(ref number, digits, scale, negative);

        Number.NumberToDouble(ref number).Should().Be(expected);
    }

    [Theory]
    [InlineData("", 0, false, 0.0)]
    [InlineData("1", -400, false, 0.0)]
    [InlineData("1", 400, false, double.PositiveInfinity)]
    [InlineData("1", 400, true, double.NegativeInfinity)]
    public void NumberToDouble_OutOfRange_ReturnsExpected(string digits, int scale, bool negative, double expected)
    {
        byte* pDigits = stackalloc byte[Number.DoubleNumberBufferLength];
        Number.NumberBuffer number = new(Number.NumberBufferKind.FloatingPoint, pDigits, Number.DoubleNumberBufferLength);
        FillDigits(ref number, digits, scale, negative);

        double actual = Number.NumberToDouble(ref number);
        if (double.IsNaN(expected))
        {
            double.IsNaN(actual).Should().BeTrue();
        }
        else
        {
            actual.Should().Be(expected);
        }
    }

    [Theory]
    [InlineData("12345", 5, false, 12345.0f)]
    [InlineData("1", 1, false, 1.0f)]
    [InlineData("5", 1, true, -5.0f)]
    [InlineData("", 0, false, 0.0f)]
    [InlineData("1", -50, false, 0.0f)]
    [InlineData("1", 50, false, float.PositiveInfinity)]
    [InlineData("1", 50, true, float.NegativeInfinity)]
    public void NumberToSingle_ValidBuffer_RoundtripsExpected(string digits, int scale, bool negative, float expected)
    {
        byte* pDigits = stackalloc byte[Number.SingleNumberBufferLength];
        Number.NumberBuffer number = new(Number.NumberBufferKind.FloatingPoint, pDigits, Number.SingleNumberBufferLength);
        FillDigits(ref number, digits, scale, negative);

        Number.NumberToSingle(ref number).Should().Be(expected);
    }

    [Fact]
    public void NumberToDouble_LargeMantissa_RoundtripsExactly()
    {
        // Use a mantissa long enough to exercise the slow BigInteger path in
        // NumberToFloatingPointBits.cs.
        byte* pDigits = stackalloc byte[Number.DoubleNumberBufferLength];
        Number.NumberBuffer number = new(Number.NumberBufferKind.FloatingPoint, pDigits, Number.DoubleNumberBufferLength);
        FillDigits(ref number, "10000000000000000000005", 23, false);

        Number.NumberToDouble(ref number).Should().Be(1.0000000000000000000005e22);
    }

    [Fact]
    public void NumberToDouble_SubnormalRange_ProducesSubnormal()
    {
        byte* pDigits = stackalloc byte[Number.DoubleNumberBufferLength];
        Number.NumberBuffer number = new(Number.NumberBufferKind.FloatingPoint, pDigits, Number.DoubleNumberBufferLength);
        FillDigits(ref number, "5", -323, false);

        double result = Number.NumberToDouble(ref number);
        result.Should().BeGreaterThan(0.0);
        result.Should().BeLessThan(double.Epsilon * 1e10);
    }

    [Fact]
    public void NumberBuffer_ToString_ReturnsDebugRepresentation()
    {
        byte* pDigits = stackalloc byte[Number.DoubleNumberBufferLength];
        Number.NumberBuffer number = new(Number.NumberBufferKind.FloatingPoint, pDigits, Number.DoubleNumberBufferLength);
        FillDigits(ref number, "12345", 3, true);

        string description = number.ToString();
        description.Should().Contain("12345");
        description.Should().Contain("Scale = 3");
        description.Should().Contain("IsNegative = True");
        description.Should().Contain("FloatingPoint");
    }

    [Fact]
    public void NumberBuffer_GetDigitsPointer_ReturnsValidPointer()
    {
        byte* pDigits = stackalloc byte[Number.DoubleNumberBufferLength];
        Number.NumberBuffer number = new(Number.NumberBufferKind.FloatingPoint, pDigits, Number.DoubleNumberBufferLength);

        byte* digitsPointer = number.GetDigitsPointer();
        ((nint)digitsPointer).Should().NotBe(0);
        (*digitsPointer).Should().Be((byte)'\0');
    }

    [Fact]
    public void FormatDouble_TooSmallBuffer_StillProducesString()
    {
        // A 16-char buffer can't hold the full G17 representation of MinValue,
        // so the underlying ValueStringBuilder must grow.
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            Number.FormatDouble(double.MinValue, ref builder, "G17".AsSpan(), s_invariant);
            builder.ToString().Should().Be(double.MinValue.ToString("G17", s_invariant));
        }
        finally
        {
            builder.Dispose();
        }
    }

    [Theory]
    [InlineData(123.45, "123.45")]
    [InlineData(0.0001, "0.0001")]
    public void FormatDouble_GeneralSpecifier_HandlesExponentSwitch(double value, string expected)
    {
        FormatDouble(value, "G").Should().Be(expected);
    }

    [Fact]
    public void FormatDouble_GeneralSpecifier_RoundtripsPreservedForFiniteValues()
    {
        double[] values = [1.0, -1.0, 123.456, 1e100, 1e-100, double.MaxValue, double.MinValue];
        foreach (double value in values)
        {
            string result = FormatDouble(value, "R");
            double parsed = double.Parse(result, s_invariant);
            parsed.Should().Be(value);
        }
    }

    [Fact]
    public void FormatDouble_NegativeZero_ProducesOutput()
    {
        // Modern .NET preserves the sign on -0.0; net481's BCL does not, but the
        // polyfill follows modern behavior. We just assert non-empty output here.
        FormatDouble(-0.0, "G").Length.Should().BeGreaterThan(0);
        FormatDouble(-0.0, "R").Length.Should().BeGreaterThan(0);
    }

    private static void FillDigits(ref Number.NumberBuffer number, string digits, int scale, bool negative)
    {
        byte* dst = number.GetDigitsPointer();
        int i = 0;
        foreach (char c in digits)
        {
            dst[i++] = (byte)c;
        }

        dst[i] = 0;
        number.DigitsCount = digits.Length;
        number.Scale = scale;
        number.IsNegative = negative;
        number.HasNonZeroTail = false;
    }

    // ---- TryFormatUInt32 / TryFormatUInt64 standard formats ----

    [Theory]
    [InlineData(1234U, "N0", "1,234")]
    [InlineData(0U, "N", "0.00")]
    [InlineData(uint.MaxValue, "N0", "4,294,967,295")]
    [InlineData(1234U, "F2", "1234.00")]
    [InlineData(1234U, "C", "\u00A41,234.00")]
    [InlineData(255U, "X8", "000000FF")]
    [InlineData(255U, "x4", "00ff")]
    [InlineData(1234U, "G", "1234")]
    [InlineData(1234U, "D6", "001234")]
    public void TryFormatUInt32_StandardFormats(uint value, string format, string expected)
    {
        Span<char> destination = stackalloc char[32];
        Number.TryFormatUInt32(value, format.AsSpan(), s_invariant, destination, out int written).Should().BeTrue();
        destination[..written].ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(1234UL, "N0", "1,234")]
    [InlineData(0UL, "N", "0.00")]
    [InlineData(ulong.MaxValue, "N0", "18,446,744,073,709,551,615")]
    [InlineData(1234UL, "F2", "1234.00")]
    [InlineData(1234UL, "C", "\u00A41,234.00")]
    [InlineData(255UL, "X8", "000000FF")]
    [InlineData(255UL, "x4", "00ff")]
    [InlineData(1234UL, "G", "1234")]
    [InlineData(1234UL, "D6", "001234")]
    public void TryFormatUInt64_StandardFormats(ulong value, string format, string expected)
    {
        Span<char> destination = stackalloc char[32];
        Number.TryFormatUInt64(value, format.AsSpan(), s_invariant, destination, out int written).Should().BeTrue();
        destination[..written].ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(1234U, "##,#", "1,234")]
    [InlineData(1234U, "0.00", "1234.00")]
    public void TryFormatUInt32_CustomFormat(uint value, string format, string expected)
    {
        Span<char> destination = stackalloc char[32];
        Number.TryFormatUInt32(value, format.AsSpan(), s_invariant, destination, out int written).Should().BeTrue();
        destination[..written].ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(1234UL, "##,#", "1,234")]
    [InlineData(1234UL, "0.00", "1234.00")]
    public void TryFormatUInt64_CustomFormat(ulong value, string format, string expected)
    {
        Span<char> destination = stackalloc char[32];
        Number.TryFormatUInt64(value, format.AsSpan(), s_invariant, destination, out int written).Should().BeTrue();
        destination[..written].ToString().Should().Be(expected);
    }
}
