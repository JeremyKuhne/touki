// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public unsafe class ValueStringBuilderTests
{
    [Fact]
    public void Constructor_WithStackAlloc()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().Be(10);
    }

    [Fact]
    public void Constructor_WithInitialCapacity()
    {
        using ValueStringBuilder builder = new(20);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void Constructor_WithInterpolatedStringParameters()
    {
        using ValueStringBuilder builder = new(5, 2);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(5 + (2 * 11)); // literalLength + (formattedCount * GuessedLengthPerHole)
    }

    [Fact]
    public void Append_SingleChar()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.Append('H');
        builder.Length.Should().Be(1);
        builder.ToString().Should().Be("H");

        builder.Append('i');
        builder.Length.Should().Be(2);
        builder.ToString().Should().Be("Hi");
    }

    [Fact]
    public void Append_CharCount()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.Append('X', 3);
        builder.Length.Should().Be(3);
        builder.ToString().Should().Be("XXX");

        builder.Append('Y', 2);
        builder.Length.Should().Be(5);
        builder.ToString().Should().Be("XXXYY");
    }

    [Fact]
    public void Append_ReadOnlySpan()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);

        builder.Append("Hello".AsSpan());
        builder.Length.Should().Be(5);
        builder.ToString().Should().Be("Hello");

        builder.Append(" World".AsSpan());
        builder.Length.Should().Be(11);
        builder.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void Append_CharPointer()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        string source = "Test";
        fixed (char* ptr = source)
        {
            builder.Append(ptr, source.Length);
        }

        builder.Length.Should().Be(4);
        builder.ToString().Should().Be("Test");
    }

    [Fact]
    public void AppendLiteral_String()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);

        builder.AppendLiteral("Hello");
        builder.Length.Should().Be(5);
        builder.ToString().Should().Be("Hello");

        builder.AppendLiteral(" World");
        builder.Length.Should().Be(11);
        builder.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void AppendLiteral_NullString()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.AppendLiteral(null);
        builder.Length.Should().Be(0);
        builder.ToString().Should().Be("");
    }

    [Fact]
    public void AppendLiteral_SingleChar()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.AppendLiteral("A");
        builder.Length.Should().Be(1);
        builder.ToString().Should().Be("A");
    }

    [Fact]
    public void AppendFormatted_String()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);

        builder.AppendFormatted("Hello");
        builder.Length.Should().Be(5);
        builder.ToString().Should().Be("Hello");

        builder.AppendFormatted(null as string);
        builder.Length.Should().Be(5);
        builder.ToString().Should().Be("Hello");
    }

    [Fact]
    public void AppendFormatted_Object()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);

        builder.AppendFormatted(42);
        builder.ToString().Should().Be("42");

        builder.AppendFormatted(null as object);
        builder.ToString().Should().Be("42");
    }

    [Fact]
    public void AppendFormatted_ISpanFormattable()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);

        builder.AppendFormatted(123);
        builder.ToString().Should().Be("123");

        builder.AppendFormatted(45.67);
        builder.ToString().Should().Be("12345.67");
    }

    [Fact]
    public void Insert_CharCount()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        builder.Insert(5, 'X', 2);
        builder.ToString().Should().Be("HelloXX World");

        builder.Insert(0, 'Y', 1);
        builder.ToString().Should().Be("YHelloXX World");
    }

    [Fact]
    public void Insert_String()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        builder.Insert(5, " Beautiful");
        builder.ToString().Should().Be("Hello Beautiful World");

        builder.Insert(0, "Oh ");
        builder.ToString().Should().Be("Oh Hello Beautiful World");
    }

    [Fact]
    public void Insert_NullString()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello");

        builder.Insert(2, null as string);
        builder.ToString().Should().Be("Hello");
        builder.Length.Should().Be(5);
    }

    [Fact]
    public void Length_Property()
    {
        ValueStringBuilder builder = new(stackalloc char[10]);

        builder.Length.Should().Be(0);

        builder.Append("Test");
        builder.Length.Should().Be(4);

        builder.Length = 2;
        builder.Length.Should().Be(2);
        builder.ToString().Should().Be("Te");

        builder.Length = 0;
        builder.Length.Should().Be(0);
        builder.ToString().Should().Be("");

        builder.Dispose();
    }

    [Fact]
    public void Capacity_Property()
    {
        using ValueStringBuilder builder = new(stackalloc char[15]);
        builder.Capacity.Should().Be(15);
    }

    [Fact]
    public void Indexer_GetSet()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Append("Hello");

        builder[0].Should().Be('H');
        builder[4].Should().Be('o');

        builder[1] = 'a';
        builder.ToString().Should().Be("Hallo");
    }

    [Fact]
    public void EnsureCapacity_AlreadySufficient()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        int originalCapacity = builder.Capacity;

        builder.EnsureCapacity(5);
        builder.Capacity.Should().Be(originalCapacity);
    }

    [Fact]
    public void EnsureCapacity_NeedsGrowth()
    {
        using ValueStringBuilder builder = new(stackalloc char[5]);

        builder.EnsureCapacity(10);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void ToString_EmptyBuilder()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.ToString().Should().Be("");
    }

    [Fact]
    public void ToString_WithContent()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");
        builder.ToString().Should().Be("Hello World");
    }

    [Fact]
    public void ToStringAndClear_WithContent()
    {
        ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        string result = builder.ToStringAndClear();
        result.Should().Be("Hello World");

        // Builder should be disposed and unusable after ToStringAndClear
    }

    [Fact]
    public void AsSpan_Basic()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello");

        ReadOnlySpan<char> span = builder.AsSpan();
        span.ToString().Should().Be("Hello");
        span.Length.Should().Be(5);
    }

    [Fact]
    public void AsSpan_WithStart()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        ReadOnlySpan<char> span = builder.AsSpan(6);
        span.ToString().Should().Be("World");
        span.Length.Should().Be(5);
    }

    [Fact]
    public void AsSpan_WithStartAndLength()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        ReadOnlySpan<char> span = builder.AsSpan(0, 5);
        span.ToString().Should().Be("Hello");
        span.Length.Should().Be(5);
    }

    [Fact]
    public void AsSpan_WithTerminate()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello");

        ReadOnlySpan<char> span = builder.AsSpan(terminate: true);
        span.ToString().Should().Be("Hello");

        // Check that null terminator was added (though not included in the span)
        builder.RawChars[5].Should().Be('\0');
    }

    [Fact]
    public void TryCopyTo_Success()
    {
        ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello");

        Span<char> destination = new char[10];
        bool result = builder.TryCopyTo(destination, out int charsWritten);

        result.Should().BeTrue();
        charsWritten.Should().Be(5);
        destination[..5].ToString().Should().Be("Hello");

        // Builder should be disposed after TryCopyTo
    }

    [Fact]
    public void TryCopyTo_InsufficientSpace()
    {
        ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        Span<char> destination = new char[5];
        bool result = builder.TryCopyTo(destination, out int charsWritten);

        result.Should().BeFalse();
        charsWritten.Should().Be(0);

        // Builder should be disposed after TryCopyTo even on failure
    }

    [Fact]
    public void GetPinnableReference_Basic()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Append("Test");

        fixed (char* ptr = builder)
        {
            (*ptr).Should().Be('T');
        }
    }

    [Fact]
    public void GetPinnableReference_WithTerminate()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Test");

        ref char reference = ref builder.GetPinnableReference(terminate: true);
        reference.Should().Be('T');

        // Check that null terminator was added
        builder.RawChars[4].Should().Be('\0');
    }

    [Fact]
    public void RawChars_Property()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Append("Test");

        Span<char> rawChars = builder.RawChars;
        rawChars.Length.Should().Be(10);
        rawChars[0].Should().Be('T');
        rawChars[1].Should().Be('e');
        rawChars[2].Should().Be('s');
        rawChars[3].Should().Be('t');
    }

    [Fact]
    public void GrowthBehavior_ExceedsInitialCapacity()
    {
        using ValueStringBuilder builder = new(stackalloc char[5]);
        builder.Append("Hello");

        // This should trigger growth
        builder.Append(" World");

        builder.Length.Should().Be(11);
        builder.ToString().Should().Be("Hello World");
        builder.Capacity.Should().BeGreaterThan(5);
    }

    [Fact]
    public void GrowthBehavior_MultipleGrowths()
    {
        using ValueStringBuilder builder = new(stackalloc char[2]);

        for (int i = 0; i < 100; i++)
        {
            builder.Append('X');
        }

        builder.Length.Should().Be(100);
        builder.ToString().Should().Be(new string('X', 100));
    }

    [Fact]
    public void Dispose_MultipleCallsSafe()
    {
        ValueStringBuilder builder = new(10);
        builder.Append("Test");

        builder.Dispose();
        builder.Dispose(); // Should not throw
    }

    [Fact]
    public void EdgeCases_EmptyOperations()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.Append('A', 0);
        builder.Length.Should().Be(0);

        builder.Insert(0, "");
        builder.Length.Should().Be(0);

        builder.Append([]);
        builder.Length.Should().Be(0);
    }

    [Fact]
    public void EdgeCases_LargeContent()
    {
        using ValueStringBuilder builder = new(10);

        string largeString = new string('A', 1000);
        builder.Append(largeString.AsSpan());

        builder.Length.Should().Be(1000);
        builder.ToString().Should().Be(largeString);
    }

    [Fact]
    public void ComplexScenario_MixedOperations()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.Append("Start");
        builder.Insert(0, "Begin ");
        builder.Append(' ');
        builder.Append("Middle".AsSpan());
        builder.Insert(builder.Length, " End");
        builder.AppendLiteral("!");

        builder.ToString().Should().Be("Begin Start Middle End!");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Constructor_VariousCapacities(int capacity)
    {
        using ValueStringBuilder builder = new(capacity);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(capacity);
        builder.Length.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Hello")]
    [InlineData("This is a longer string to test")]
    public void RoundTrip_StringOperations(string input)
    {
        using ValueStringBuilder builder = new(input.Length + 10);
        builder.Append(input.AsSpan());

        builder.ToString().Should().Be(input);
        builder.Length.Should().Be(input.Length);
    }

    [Fact]
    public void InterpolatedStringHandler_BasicUsage()
    {
        // Test the interpolated string handler constructor
        using ValueStringBuilder builder = new(10, 2); // 10 literal chars, 2 holes

        builder.AppendLiteral("Value: ");
        builder.AppendFormatted(42);
        builder.AppendLiteral(", Text: ");
        builder.AppendFormatted("Hello");

        builder.ToString().Should().Be("Value: 42, Text: Hello");
    }
}
