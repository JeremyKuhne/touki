// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace Touki.Text;

/// <summary>
///  Tests for <see cref="StringBuilderExtensions"/> AppendFormatted methods.
/// </summary>
public unsafe class StringBuilderExtensionsTests
{
    [Fact]
    public void AppendFormatted_SimpleInterpolatedString()
    {
        StringBuilder builder = new();
        int value = 42;

        builder.AppendFormatted($"Value is {value}.");

        builder.ToString().Should().Be("Value is 42.");
    }

    [Fact]
    public void AppendFormatted_MultipleValues()
    {
        StringBuilder builder = new();
        string name = "Alice";
        int age = 30;
        double salary = 75000.50;

        builder.AppendFormatted($"Name: {name}, Age: {age}, Salary: ${salary:F2}");

        builder.ToString().Should().Be("Name: Alice, Age: 30, Salary: $75000.50");
    }

    [Fact]
    public void AppendFormatted_WithFormattingSpecifiers()
    {
        StringBuilder builder = new();
        DateTime now = new(2025, 6, 23, 14, 30, 45);
        int hex = 255;

        builder.AppendFormatted($"Date: {now:yyyy-MM-dd HH:mm:ss}, Hex: 0x{hex:X2}");

        builder.ToString().Should().Be("Date: 2025-06-23 14:30:45, Hex: 0xFF");
    }

    [Fact]
    public void AppendFormatted_EmptyInterpolatedString()
    {
        StringBuilder builder = new();

        builder.AppendFormatted($"");

        builder.ToString().Should().Be("");
    }

    [Fact]
    public void AppendFormatted_OnlyLiteralString()
    {
        StringBuilder builder = new();

        builder.AppendFormatted($"Hello World!");

        builder.ToString().Should().Be("Hello World!");
    }

    [Fact]
    public void AppendFormatted_ChainedCalls()
    {
        StringBuilder builder = new();

        builder.AppendFormatted($"First: {1}")
               .AppendFormatted($", Second: {2}")
               .AppendFormatted($", Third: {3}");

        builder.ToString().Should().Be("First: 1, Second: 2, Third: 3");
    }

    [Fact]
    public void AppendFormatted_WithExistingContent()
    {
        StringBuilder builder = new("Prefix ");
        int value = 123;

        builder.AppendFormatted($"Value: {value}");

        builder.ToString().Should().Be("Prefix Value: 123");
    }

    [Fact]
    public void AppendFormatted_NullValues()
    {
        StringBuilder builder = new();
        string? nullString = null;
        object? nullObject = null;

        builder.AppendFormatted($"String: '{nullString}', Object: '{nullObject}'");

        builder.ToString().Should().Be("String: '', Object: ''");
    }

    [Fact]
    public void AppendFormatted_GenericOverload_WithStringFormat()
    {
        StringBuilder builder = new();
        int value = 42;

        builder.AppendFormatted("The answer is {0}!", value);

        builder.ToString().Should().Be("The answer is 42!");
    }

    [Fact]
    public void AppendFormatted_GenericOverload_WithSpanFormat()
    {
        StringBuilder builder = new();
        double value = 3.14159;

        builder.AppendFormatted("Pi is approximately {0:F2}".AsSpan(), value);

        builder.ToString().Should().Be("Pi is approximately 3.14");
    }

    [Fact]
    public void AppendFormatted_ValueArrayOverload_WithStringFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("Numbers: {0}, {1}, {2}", 1, 2, 3);
        builder.ToString().Should().Be("Numbers: 1, 2, 3");
    }

    [Fact]
    public void AppendFormatted_ValueArrayOverload_WithSpanFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("Values: {0}, {1}, {2}".AsSpan(), "Hello", 42, true);
        builder.ToString().Should().Be("Values: Hello, 42, True");
    }

    [Fact]
    public void AppendFormatted_TwoValueOverload_WithStringFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("First: {0}, Second: {1}", 10, 20);
        builder.ToString().Should().Be("First: 10, Second: 20");
    }

    [Fact]
    public void AppendFormatted_TwoValueOverload_WithSpanFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("A: {0:X}, B: {1:F1}".AsSpan(), 255, 3.14);
        builder.ToString().Should().Be("A: FF, B: 3.1");
    }

    [Fact]
    public void AppendFormatted_ThreeValueOverload_WithStringFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("RGB: ({0}, {1}, {2})", 255, 128, 0);
        builder.ToString().Should().Be("RGB: (255, 128, 0)");
    }

    [Fact]
    public void AppendFormatted_ThreeValueOverload_WithSpanFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("Point: ({0:F2}, {1:F2}, {2:F2})".AsSpan(), 1.5, 2.7, 3.9);
        builder.ToString().Should().Be("Point: (1.50, 2.70, 3.90)");
    }

    [Fact]
    public void AppendFormatted_FourValueOverload_WithStringFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("RGBA: ({0}, {1}, {2}, {3})", 255, 128, 64, 192);
        builder.ToString().Should().Be("RGBA: (255, 128, 64, 192)");
    }

    [Fact]
    public void AppendFormatted_FourValueOverload_WithSpanFormat()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("Rect: ({0}, {1}, {2}, {3})".AsSpan(), 10, 20, 100, 200);
        builder.ToString().Should().Be("Rect: (10, 20, 100, 200)");
    }

    [Fact]
    public void AppendFormatted_WithEnums()
    {
        StringBuilder builder = new();
        builder.AppendFormatted($"Today is {DayOfWeek.Monday}");
        builder.ToString().Should().Be("Today is Monday");
    }

    [Fact]
    public void AppendFormatted_WithEnumValues()
    {
        StringBuilder builder = new();
        builder.AppendFormatted("Day: {0}", Value.Create(DayOfWeek.Friday));
        builder.ToString().Should().Be("Day: Friday");
    }

    [Fact]
    public void AppendFormatted_MixedOverloads()
    {
        StringBuilder builder = new();

        // Start with interpolated string
        builder.AppendFormatted($"Start: {1}");

        // Add using string format with single value
        builder.AppendFormatted(", Middle: {0}", 2);

        // Add using span format with two values
        builder.AppendFormatted(", End: {0} and {1}".AsSpan(), 3, 4);

        builder.ToString().Should().Be("Start: 1, Middle: 2, End: 3 and 4");
    }

    [Fact]
    public void AppendFormatted_LargeContent()
    {
        StringBuilder builder = new();
        string largeText = new('A', 300); // Larger than the 256 char buffer in the implementation

        builder.AppendFormatted($"Large: {largeText}");

        builder.ToString().Should().Be($"Large: {largeText}");
    }

    [Fact]
    public void AppendFormatted_EmptyFormatString()
    {
        StringBuilder builder = new();

        builder.AppendFormatted("", 42);

        builder.ToString().Should().Be("");
    }

    [Fact]
    public void AppendFormatted_FormatStringWithoutPlaceholders()
    {
        StringBuilder builder = new();

        builder.AppendFormatted("No placeholders here", 42);

        builder.ToString().Should().Be("No placeholders here");
    }

    private enum TestEnum
    {
        First,
        Second,
        Third
    }
}
