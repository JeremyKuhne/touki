// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

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

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    public void AsHandler_Int(int value)
    {
        string result = TestFormat($"Hello, {value}!");
        result.Should().Be($"Hello, {value}!");
    }

    [Theory]
    [InlineData(DayOfWeek.Monday)]
    [InlineData(DayOfWeek.Friday)]
    public void AsHandler_Enum(DayOfWeek value)
    {
        string result = TestFormat($"Hello, it's {value}!");
        result.Should().Be($"Hello, it's {value}!");
    }

    [Fact]
    public void StringBuilderBehavior()
    {
        StringBuilder builder = new();
        builder.Append($"Test{builder}");
        string result = builder.ToString();
#if NETFRAMEWORK
        result.Should().Be("Test");
#else
        result.Should().Be("TestTest");
#endif
    }

    private static string TestFormat(ref ValueStringBuilder builder) => builder.ToStringAndClear();

    [Fact]
    public void AppendFormat_Int()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Value: {0}", 42);
        builder.ToString().Should().Be("Value: 42");
    }

    [Fact]
    public void AppendFormat_SingleArg_String()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Hello {0}!", new Value("World"));
        builder.ToString().Should().Be("Hello World!");
    }

    [Fact]
    public void AppendFormat_SingleArg_Byte()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Byte: {0}", (byte)255);
        builder.ToString().Should().Be("Byte: 255");
    }

    [Fact]
    public void AppendFormat_SingleArg_SByte()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("SByte: {0}", (sbyte)-128);
        builder.ToString().Should().Be("SByte: -128");
    }

    [Fact]
    public void AppendFormat_SingleArg_Bool()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("True: {0}, False: {1}", true, false);
        builder.ToString().Should().Be("True: True, False: False");
    }

    [Fact]
    public void AppendFormat_SingleArg_Char()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Char: {0}", 'A');
        builder.ToString().Should().Be("Char: A");
    }

    [Fact]
    public void AppendFormat_SingleArg_Short()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Short: {0}", (short)-32768);
        builder.ToString().Should().Be("Short: -32768");
    }

    [Fact]
    public void AppendFormat_SingleArg_UShort()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("UShort: {0}", (ushort)65535);
        builder.ToString().Should().Be("UShort: 65535");
    }

    [Fact]
    public void AppendFormat_SingleArg_UInt()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("UInt: {0}", 4294967295U);
        builder.ToString().Should().Be("UInt: 4294967295");
    }

    [Fact]
    public void AppendFormat_SingleArg_Long()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Long: {0}", -9223372036854775808L);
        builder.ToString().Should().Be("Long: -9223372036854775808");
    }

    [Fact]
    public void AppendFormat_SingleArg_ULong()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("ULong: {0}", 18446744073709551615UL);
        builder.ToString().Should().Be("ULong: 18446744073709551615");
    }

    [Fact]
    public void AppendFormat_SingleArg_Float()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Float: {0}", 3.14159f);
        builder.ToString().Should().Be("Float: 3.14159");
    }

    [Fact]
    public void AppendFormat_SingleArg_Double()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Double: {0}", 3.141592653589793);
        builder.ToString().Should().Be(string.Format("Double: {0}", 3.141592653589793));
    }

    [Fact]
    public void AppendFormat_SingleArg_Decimal()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Decimal: {0}", 123.456m);
        builder.ToString().Should().Be("Decimal: 123.456");
    }

    [Fact]
    public void AppendFormat_SingleArg_DateTime()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        DateTime dateTime = new(2025, 6, 18, 15, 30, 45);
        builder.AppendFormat("DateTime: {0}", dateTime);
        string expected = $"DateTime: {dateTime}";
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_SingleArg_DateTimeOffset()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        DateTimeOffset dateTimeOffset = new(2025, 6, 18, 15, 30, 45, TimeSpan.FromHours(-5));
        builder.AppendFormat("DateTimeOffset: {0}", dateTimeOffset);
        string expected = $"DateTimeOffset: {dateTimeOffset}";
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_SingleArg_NullableTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        int? nullableInt = 42;
        int? nullInt = null;

        builder.AppendFormat("Nullable: {0}, Null: {1}", nullableInt, nullInt);
        builder.ToString().Should().Be("Nullable: 42, Null: ");
    }
    [Fact]
    public void AppendFormat_SingleArg_Object()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        object obj = "Hello World";
        builder.AppendFormat("Object: {0}", new Value(obj));
        builder.ToString().Should().Be("Object: Hello World");
    }

    [Fact]
    public void AppendFormat_TwoArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Name: {0}, Age: {1}", new Value("John"), 25);
        builder.ToString().Should().Be("Name: John, Age: 25");
    }

    [Fact]
    public void AppendFormat_TwoArgs_ReversedOrder()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Age: {1}, Name: {0}", new Value("Alice"), 30);
        builder.ToString().Should().Be("Age: 30, Name: Alice");
    }

    [Fact]
    public void AppendFormat_ThreeArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("Name: {0}, Age: {1}, City: {2}", new Value("Bob"), 35, new Value("Seattle"));
        builder.ToString().Should().Be("Name: Bob, Age: 35, City: Seattle");
    }

    [Fact]
    public void AppendFormat_FourArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[150]);
        builder.AppendFormat("Name: {0}, Age: {1}, City: {2}, Country: {3}", new Value("Charlie"), 40, new Value("London"), new Value("UK"));
        builder.ToString().Should().Be("Name: Charlie, Age: 40, City: London, Country: UK");
    }

    [Fact]
    public void AppendFormat_WithFormatSpecifiers()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test various format specifiers
        builder.AppendFormat("Hex: {0:X}, Decimal: {0:D}, Currency: {1:C}", 255, 123.45m);

        // Note: The exact output may vary based on culture, so we'll test the structure
        string result = builder.ToString();
        result.Should().StartWith("Hex: FF, Decimal: 255, Currency:");
        result.Should().Contain("123.45");
    }
    [Fact]
    public void AppendFormat_WithAlignment()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Left: '{0,-10}' Right: '{1,10}'", new Value("Hello"), new Value("World"));
        builder.ToString().Should().Be("Left: 'Hello     ' Right: '     World'");
    }

    [Fact]
    public void AppendFormat_WithAlignmentAndFormat()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Padded hex: '{0,8:X8}'", 255);
        builder.ToString().Should().Be("Padded hex: '000000FF'");
    }
    [Fact]
    public void AppendFormat_ReadOnlySpanArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        Value[] args = [42, new Value("Hello"), 3.14, true];
        builder.AppendFormat("Int: {0}, String: {1}, Double: {2}, Bool: {3}".AsSpan(), args.AsSpan());
        builder.ToString().Should().Be("Int: 42, String: Hello, Double: 3.14, Bool: True");
    }

    [Fact]
    public void AppendFormat_EmptyFormatString()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.AppendFormat("", 42);
        builder.ToString().Should().Be("");
    }

    [Fact]
    public void AppendFormat_NoPlaceholders()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("No placeholders", 42);
        builder.ToString().Should().Be("No placeholders");
    }

    [Fact]
    public void AppendFormat_MultipleSameArgument()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{0} + {0} = {1}", 5, 10);
        builder.ToString().Should().Be("5 + 5 = 10");
    }

    [Fact]
    public void AppendFormat_EscapedBraces()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{0}} = {0}", 42);
        builder.ToString().Should().Be("{0} = 42");
    }

    [Fact]
    public void AppendFormat_ArraySegmentByte()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        byte[] data = [1, 2, 3, 4, 5];
        ArraySegment<byte> segment = new(data, 1, 3);
        builder.AppendFormat("Segment: {0}", segment);

        string result = builder.ToString();
        result.Should().StartWith("Segment:");
        // ArraySegment<byte> format may vary, but should contain the type information
    }

    [Fact]
    public void AppendFormat_ArraySegmentChar()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        char[] data = ['H', 'e', 'l', 'l', 'o'];
        ArraySegment<char> segment = new(data, 1, 3);
        builder.AppendFormat("Segment: {0}", segment);

        string result = builder.ToString();
        result.Should().StartWith("Segment:");
        // ArraySegment<char> format may vary, but should contain the type information
    }

    [Fact]
    public void AppendFormat_StringFormatBehavior_NumberFormats()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test that behavior matches string.Format
        int number = 1234;
        string expected = string.Format("D: {0:D6}, F: {0:F2}, N: {0:N0}, P: {1:P1}", number, 0.1234);

        builder.AppendFormat("D: {0:D6}, F: {0:F2}, N: {0:N0}, P: {1:P1}", number, 0.1234);
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_StringFormatBehavior_DateTimeFormats()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        DateTime dateTime = new(2025, 6, 18, 15, 30, 45);
        string expected = string.Format("Short: {0:d}, Long: {0:D}, Time: {0:t}", dateTime);

        builder.AppendFormat("Short: {0:d}, Long: {0:D}, Time: {0:t}", dateTime);
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_StringFormatBehavior_CustomFormats()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        DateTime dateTime = new(2025, 6, 18, 15, 30, 45);
        string expected = string.Format("Custom: {0:yyyy-MM-dd HH:mm:ss}", dateTime);

        builder.AppendFormat("Custom: {0:yyyy-MM-dd HH:mm:ss}", dateTime);
        builder.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("G")]
    [InlineData("N")]
    [InlineData("F")]
    [InlineData("E")]
    [InlineData("e")]
    [InlineData("C")]
    [InlineData("P")]
    public void AppendFormat_StandardNumericFormats(string format)
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        double value = 1234.5678;
        string formatString = "{0:" + format + "}";
        string expected = string.Format(formatString, value);

        builder.AppendFormat(formatString, value);
        builder.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void AppendFormat_ArgumentIndexBoundaries(int argIndex)
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        Value[] args = [new Value("First"), new Value("Second"), new Value("Third"), new Value("Fourth")];
        string formatString = $"Value: {{{argIndex}}}";

        builder.AppendFormat(formatString.AsSpan(), args.AsSpan());
        builder.ToString().Should().Be("Value: " + args[argIndex].As<object>().ToString());
    }

    [Fact]
    public void AppendFormat_LargeFormatString()
    {
        using ValueStringBuilder builder = new(stackalloc char[500]);

        string largeFormat = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"{{{i}}}"));
        Value[] args = [.. Enumerable.Range(0, 20).Select(i => (Value)i)];

        builder.AppendFormat(largeFormat.AsSpan(), args.AsSpan());

        string expected = string.Join(" ", Enumerable.Range(0, 20));
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_MixedTypesPrecisionTest()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test that behavior exactly matches string.Format for mixed types
        string stringVal = "test";
        int intVal = 42;
        double doubleVal = 3.14159;
        bool boolVal = true;
        DateTime dateVal = new(2025, 1, 1);

        string expected = string.Format("String: {0}, Int: {1:D5}, Double: {2:F2}, Bool: {3}, Date: {4:yyyy-MM-dd}",
            stringVal, intVal, doubleVal, boolVal, dateVal);

        Value[] args = [new Value(stringVal), intVal, doubleVal, boolVal, dateVal];
        builder.AppendFormat("String: {0}, Int: {1:D5}, Double: {2:F2}, Bool: {3}, Date: {4:yyyy-MM-dd}".AsSpan(), args.AsSpan());

        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_ArgumentIndexOutOfRange_ThrowsFormatException()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Test argument index that's out of range
        bool threwException = false;
        try
        {
            builder.AppendFormat("{1}", 42); // Only one argument provided, but index 1 requested
        }
        catch (FormatException)
        {
            threwException = true;
        }
        threwException.Should().BeTrue("Out of range argument index should throw FormatException");
    }

    [Fact]
    public void AppendFormat_NegativeArgumentIndex_ThrowsFormatException()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Test negative argument index
        bool threwException = false;
        try
        {
            builder.AppendFormat("{-1}", 42);
        }
        catch (FormatException)
        {
            threwException = true;
        }
        threwException.Should().BeTrue("Negative argument index should throw FormatException");
    }

    [Fact]
    public void AppendFormat_VeryLargeAlignment_ThrowsFormatException()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Test alignment that exceeds the width limit (1000000)
        bool threwException = false;
        try
        {
            builder.AppendFormat("{0,2000000}", 42);
        }
        catch (FormatException)
        {
            threwException = true;
        }
        threwException.Should().BeTrue("Very large alignment should throw FormatException");
    }

    [Fact]
    public void AppendFormat_ComplexFormatSpecifiers()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test complex format scenarios that match string.Format behavior
        decimal value = 1234567.89m;
        DateTime date = new(2025, 12, 25, 14, 30, 0);

        string expected = string.Format("Money: {0:C2}, Scientific: {1:E3}, Date: {2:MMM dd, yyyy}",
            value, 987654.321, date);

        Value[] args = [value, 987654.321, date];
        builder.AppendFormat("Money: {0:C2}, Scientific: {1:E3}, Date: {2:MMM dd, yyyy}".AsSpan(), args.AsSpan());

        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_CustomFormatsWithPadding()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test custom numeric formats with padding
        int number = 42;
        string expected = string.Format("Binary-like: {0:0000000000}, Custom: {0:#,##0.00}", number);

        builder.AppendFormat("Binary-like: {0:0000000000}, Custom: {0:#,##0.00}", number);
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_StringWithSpecialCharacters()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        string specialString = "Hello\tWorld\nNew Line\r\nCarriage Return";
        builder.AppendFormat("Special: '{0}'", new Value(specialString));
        builder.ToString().Should().Be($"Special: '{specialString}'");
    }

    [Fact]
    public void AppendFormat_NullValue()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        Value nullValue = new((object?)null);
        builder.AppendFormat("Null value: '{0}'", nullValue);
        builder.ToString().Should().Be("Null value: ''");
    }

    [Fact]
    public void AppendFormat_VeryLongString()
    {
        using ValueStringBuilder builder = new(stackalloc char[1000]);

        string longString = new('A', 500);
        string expected = $"Long: {longString}";

        builder.AppendFormat("Long: {0}", new Value(longString));
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_MultipleFormatsSequentially()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test multiple AppendFormat calls in sequence
        builder.AppendFormat("First: {0}", 1);
        builder.AppendFormat(", Second: {0}", 2);
        builder.AppendFormat(", Third: {0}", new Value("three"));

        builder.ToString().Should().Be("First: 1, Second: 2, Third: three");
    }

    [Fact]
    public void AppendFormat_PerformanceWithManyArguments()
    {
        using ValueStringBuilder builder = new(2000);

        // Test with the maximum number of arguments supported by the 4-arg overload
        Value[] manyArgs = [.. Enumerable.Range(0, 100).Select(i => (Value)i)];
        string format = string.Join(", ", Enumerable.Range(0, 100).Select(i => $"{{{i}}}"));

        builder.AppendFormat(format.AsSpan(), manyArgs.AsSpan());

        string expected = string.Join(", ", Enumerable.Range(0, 100));
        builder.ToString().Should().Be(expected);
    }

    [Fact]
    public void AppendFormat_EscapedBraces_AtBeginning()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{Start}} {0}", new Value("value"));
        builder.ToString().Should().Be("{Start} value");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_AtEnd()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{0} {{End}}", new Value("value"));
        builder.ToString().Should().Be("value {End}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_OnlyEscaped()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{No}} {{Arguments}}", 42); // Need at least one argument
        builder.ToString().Should().Be("{No} {Arguments}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_Multiple()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{First}} {0} {{Second}} {1} {{Third}}", new Value("arg1"), new Value("arg2"));
        builder.ToString().Should().Be("{First} arg1 {Second} arg2 {Third}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_Consecutive()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{{{{0}}}}}", 42);
        builder.ToString().Should().Be("{{42}}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_Mixed()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("Value is {{0: {0}}} and {{1: {1}}}", 42, new Value("test"));
        builder.ToString().Should().Be("Value is {0: 42} and {1: test}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_WithFormatSpecifiers()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{Format: {0:X8}}}", 255);
        builder.ToString().Should().Be("{Format: 000000FF}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_WithAlignment()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{Padded: {0,10}}}", new Value("test"));
        builder.ToString().Should().Be("{Padded:       test}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_WithAlignmentAndFormat()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{Hex: {0,8:X8}}}", 255);
        builder.ToString().Should().Be("{Hex: 000000FF}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_ComplexPattern()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);
        builder.AppendFormat("{{obj: {{ name: \"{0}\", value: {1} }}}}", new Value("test"), 42);
        builder.ToString().Should().Be("{obj: { name: \"test\", value: 42 }}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_ArgumentIndexReuse()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{arg0}} = {0}, {{arg0}} = {0}", 42);
        builder.ToString().Should().Be("{arg0} = 42, {arg0} = 42");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_EmptyBetween()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{}}{0}{{}}", new Value("middle"));
        builder.ToString().Should().Be("{}middle{}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_QuadrupleOpening()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{{{{0}", 42);
        builder.ToString().Should().Be("{{42");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_QuadrupleClosing()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{0}}}}}", 42);
        builder.ToString().Should().Be("42}}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_NestedPattern()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{outer {{inner {0}}} outer}}", new Value("value"));
        builder.ToString().Should().Be("{outer {inner value} outer}");
    }

    [Fact]
    public void AppendFormat_EscapedBraces_AllTypesOfEscaping()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);
        builder.AppendFormat("{{}} {{ }} {0} }} {{ {{", new Value("test"));
        builder.ToString().Should().Be("{} { } test } { {");
    }
}
