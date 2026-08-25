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
    public void FormatValues_ProviderWithSpanArgs_UsesProvider()
    {
        CultureInfo provider = CultureInfo.GetCultureInfo("fr-FR");
        ReadOnlySpan<Value> args = [Value.Create(1234.5), Value.Create(0.25)];

        string result = string.FormatValues(provider, "{0:N1} {1:P0}".AsSpan(), args);

        result.Should().Be(string.Format(provider, "{0:N1} {1:P0}", 1234.5, 0.25));
    }

    [TestMethod]
    public void FormatValues_NullProvider_UsesCurrentCulture()
    {
        ReadOnlySpan<Value> args = [Value.Create(1234.5), Value.Create(0.25)];

        string result = string.FormatValues(null, "{0:N1} {1:P0}".AsSpan(), args);

        result.Should().Be(string.Format(CultureInfo.CurrentCulture, "{0:N1} {1:P0}", 1234.5, 0.25));
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
    public void FormatValues_ProviderWithTwoArgs_UsesProvider()
    {
        CultureInfo provider = CultureInfo.GetCultureInfo("fr-FR");

        string result = string.FormatValues(
            provider,
            "{0:N1} {1:P0}".AsSpan(),
            Value.Create(1234.5),
            Value.Create(0.25));

        result.Should().Be(string.Format(provider, "{0:N1} {1:P0}", 1234.5, 0.25));
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
    public void FormatValues_ProviderWithThreeArgs_UsesProvider()
    {
        CultureInfo provider = CultureInfo.GetCultureInfo("fr-FR");

        string result = string.FormatValues(
            provider,
            "{0:N1} {1:N1} {2:P0}".AsSpan(),
            Value.Create(1234.5),
            Value.Create(67.5),
            Value.Create(0.25));

        result.Should().Be(string.Format(provider, "{0:N1} {1:N1} {2:P0}", 1234.5, 67.5, 0.25));
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
    public void FormatValues_ProviderWithFourArgs_UsesProvider()
    {
        CultureInfo provider = CultureInfo.GetCultureInfo("fr-FR");

        string result = string.FormatValues(
            provider,
            "{0:N1} {1:N1} {2:N1} {3:P0}".AsSpan(),
            Value.Create(1234.5),
            Value.Create(67.5),
            Value.Create(8.24),
            Value.Create(0.25));

        result.Should().Be(string.Format(provider, "{0:N1} {1:N1} {2:N1} {3:P0}", 1234.5, 67.5, 8.24, 0.25));
    }

    [TestMethod]
    public void FormatValues_CustomFormatter_ReceivesUnderlyingValues()
    {
        UnderlyingTypeFormatProvider provider = new();
        ReadOnlySpan<Value> args = [Value.Create(42), Value.Create("text"), Value.Create((object?)null)];

        string result = string.FormatValues(provider, "{0}|{1}|{2}".AsSpan(), args);

        result.Should().Be("[Int32:42]|[String:text]|[null]");
    }

    [TestMethod]
    public void FormatValues_CustomFormatterReturnsNull_FallsBackToDefaultFormatting()
    {
        NullFormatProvider provider = new(CultureInfo.GetCultureInfo("fr-FR"));
        ReadOnlySpan<Value> args = [Value.Create(1234.5), Value.Create("text"), Value.Create((object?)null)];

        string result = string.FormatValues(provider, "{0:N1}|{1}|{2}".AsSpan(), args);

        result.Should().Be(string.Format(provider, "{0:N1}|{1}|{2}", 1234.5, "text", null));
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

    private sealed class UnderlyingTypeFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object? GetFormat(Type? formatType) => formatType == typeof(ICustomFormatter) ? this : null;

        public string Format(string? format, object? arg, IFormatProvider? formatProvider) =>
            arg is null ? "[null]" : $"[{arg.GetType().Name}:{arg}]";
    }

    private sealed class NullFormatProvider(CultureInfo culture) : IFormatProvider, ICustomFormatter
    {
        public object? GetFormat(Type? formatType) =>
            formatType == typeof(ICustomFormatter) ? this : culture.GetFormat(formatType);

        public string Format(string? format, object? arg, IFormatProvider? formatProvider) => null!;
    }
}
