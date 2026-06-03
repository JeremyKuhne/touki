// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;

namespace Touki.Text;

[TestClass]
public class StringExtensionsTests
{
    [TestMethod]
    public void FormatValue_Generic_FormatsUnmanagedArgument()
    {
        string result = string.FormatValue("{0:X4}".AsSpan(), 0x2A);
        result.Should().Be("002A");
    }

    [TestMethod]
    public void FormatValue_Generic_LargeOutput_GrowsBuffer()
    {
        ReadOnlySpan<char> format = "{0}".AsSpan();
        long value = long.MaxValue;
        string result = string.FormatValue(format, value);
        result.Should().Be(long.MaxValue.ToString(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void FormatValue_ValueArg_FormatsBoxedValue()
    {
        // FormatValue constructs a ValueStringBuilder with a null IFormatProvider,
        // so number formatting follows CultureInfo.CurrentCulture. The expected
        // string must use the same culture or this assertion is locale-dependent.
        string result = string.FormatValue("{0:N0}".AsSpan(), Value.Create(1234));
        result.Should().Be(1234.ToString("N0", CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void FormatValues_SpanArgs_FormatsMultiplePlaceholders()
    {
        ReadOnlySpan<Value> args = [Value.Create("Alice"), Value.Create(30)];
        string result = string.FormatValues("{0} is {1}".AsSpan(), args);
        result.Should().Be("Alice is 30");
    }

    [TestMethod]
    public void FormatValues_TwoArgs_FormatsBothPlaceholders()
    {
        string result = string.FormatValues(
            "{0}-{1}".AsSpan(),
            Value.Create("a"),
            Value.Create(1));
        result.Should().Be("a-1");
    }

    [TestMethod]
    public void FormatValues_ThreeArgs_FormatsAllPlaceholders()
    {
        string result = string.FormatValues(
            "{0}/{1}/{2}".AsSpan(),
            Value.Create(2026),
            Value.Create(5),
            Value.Create(10));
        result.Should().Be("2026/5/10");
    }

    [TestMethod]
    public void FormatValues_FourArgs_FormatsAllPlaceholders()
    {
        string result = string.FormatValues(
            "{0},{1},{2},{3}".AsSpan(),
            Value.Create(1),
            Value.Create(2),
            Value.Create(3),
            Value.Create(4));
        result.Should().Be("1,2,3,4");
    }

    [TestMethod]
    public void FormatValues_FourArgs_LiteralFormat_NoPlaceholders_ReturnsLiteral()
    {
        string result = string.FormatValues(
            "literal".AsSpan(),
            Value.Create(1),
            Value.Create(2),
            Value.Create(3),
            Value.Create(4));
        result.Should().Be("literal");
    }
}
