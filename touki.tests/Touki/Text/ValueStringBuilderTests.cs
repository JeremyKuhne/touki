// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Globalization;
using System.Text;
using Touki.Text;

namespace Touki;

[TestClass]
public unsafe class ValueStringBuilderTests
{
    [TestMethod]
    public void Constructor_WithStackAlloc()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().Be(10);
    }

    [TestMethod]
    public void Constructor_WithInitialCapacity()
    {
        using ValueStringBuilder builder = new(20);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(20);
    }

    [TestMethod]
    public void Constructor_WithInterpolatedStringParameters()
    {
        using ValueStringBuilder builder = new(5, 2);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(5 + (2 * 11)); // literalLength + (formattedCount * GuessedLengthPerHole)
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void Append_InterpolatedString_AppendsFormattedValue()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Before ");

        builder.Append($"Value {42:X2}");

        builder.ToString().Should().Be("Before Value 2A");
    }

    [TestMethod]
    public void Append_InterpolatedStringLongerThanInitialCapacity_GrowsSafely()
    {
        using ValueStringBuilder builder = new(stackalloc char[4]);
        builder.Append("A");

        builder.Append($"{1234567890}-{1234567890}-{1234567890}");

        builder.ToString().Should().Be("A1234567890-1234567890-1234567890");
    }

    [TestMethod]
    public void Append_InterpolatedStringWithProvider_UsesProvider()
    {
        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo culture = CultureInfo.GetCultureInfo("fr-FR");
            using ValueStringBuilder builder = new(stackalloc char[32], culture);

            builder.Append($"{1234.5:N1}");

            builder.ToString().Should().Be(1234.5.ToString("N1", culture));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    public void AppendLine_InterpolatedString_AppendsFormattedValueAndNewLine()
    {
        using ValueStringBuilder builder = new(stackalloc char[32]);

        builder.AppendLine($"Value {42:X2}");

        builder.ToString().Should().Be($"Value 2A{Environment.NewLine}");
    }

#if !DEBUG
    [TestMethod]
    public void Append_InterpolatedStringWithLargeContent_DoesNotAllocateIntermediateString()
    {
        string large = new('x', 8192);
        {
            using ValueStringBuilder warmup = new(stackalloc char[32]);
            warmup.Append($"<{large}>");
        }

        using ValueStringBuilder builder = new(stackalloc char[32]);
        using (MemoryWatch.Create)
        {
            builder.Append($"<{large}>");
        }

        builder.ToString().Should().Be("<" + large + ">");
    }

    [TestMethod]
    public void AppendLine_InterpolatedStringWithLargeContent_DoesNotAllocateIntermediateString()
    {
        string large = new('x', 8192);
        {
            using ValueStringBuilder warmup = new(stackalloc char[32]);
            warmup.AppendLine($"<{large}>");
        }

        using ValueStringBuilder builder = new(stackalloc char[32]);
        using (MemoryWatch.Create)
        {
            builder.AppendLine($"<{large}>");
        }

        builder.ToString().Should().Be("<" + large + ">" + Environment.NewLine);
    }
#endif

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void AppendLiteral_NullString()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.AppendLiteral(null);
        builder.Length.Should().Be(0);
        builder.ToString().Should().Be("");
    }

    [TestMethod]
    public void AppendLiteral_SingleChar()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        builder.AppendLiteral("A");
        builder.Length.Should().Be(1);
        builder.ToString().Should().Be("A");
    }

    [TestMethod]
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

    [TestMethod]
    public void AppendFormatted_Object()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);

        builder.AppendFormatted(42);
        builder.ToString().Should().Be("42");

        builder.AppendFormatted(null as object);
        builder.ToString().Should().Be("42");
    }

    [TestMethod]
    public void AppendFormatted_ISpanFormattable()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);

        builder.AppendFormatted(123);
        builder.ToString().Should().Be("123");

        builder.AppendFormatted(45.67);
        builder.ToString().Should().Be("12345.67");
    }

    [TestMethod]
    public void Insert_CharCount()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        builder.Insert(5, 'X', 2);
        builder.ToString().Should().Be("HelloXX World");

        builder.Insert(0, 'Y', 1);
        builder.ToString().Should().Be("YHelloXX World");
    }

    [TestMethod]
    public void Insert_String()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        builder.Insert(5, " Beautiful");
        builder.ToString().Should().Be("Hello Beautiful World");

        builder.Insert(0, "Oh ");
        builder.ToString().Should().Be("Oh Hello Beautiful World");
    }

    [TestMethod]
    public void Insert_NullString()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello");

        builder.Insert(2, null as string);
        builder.ToString().Should().Be("Hello");
        builder.Length.Should().Be(5);
    }

    [TestMethod]
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

    [TestMethod]
    public void Capacity_Property()
    {
        using ValueStringBuilder builder = new(stackalloc char[15]);
        builder.Capacity.Should().Be(15);
    }

    [TestMethod]
    public void Indexer_GetSet()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Append("Hello");

        builder[0].Should().Be('H');
        builder[4].Should().Be('o');

        builder[1] = 'a';
        builder.ToString().Should().Be("Hallo");
    }

    [TestMethod]
    public void EnsureCapacity_AlreadySufficient()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        int originalCapacity = builder.Capacity;

        builder.EnsureCapacity(5);
        builder.Capacity.Should().Be(originalCapacity);
    }

    [TestMethod]
    public void EnsureCapacity_NeedsGrowth()
    {
        using ValueStringBuilder builder = new(stackalloc char[5]);

        builder.EnsureCapacity(10);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(10);
    }

    [TestMethod]
    public void ToString_EmptyBuilder()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.ToString().Should().Be("");
    }

    [TestMethod]
    public void ToString_WithContent()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");
        builder.ToString().Should().Be("Hello World");
    }

    [TestMethod]
    public void ToStringAndClear_WithContent()
    {
        ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        string result = builder.ToStringAndDispose();
        result.Should().Be("Hello World");

        // Builder should be disposed and unusable after ToStringAndClear
    }

    [TestMethod]
    public void AsSpan_Basic()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello");

        ReadOnlySpan<char> span = builder.AsSpan();
        span.ToString().Should().Be("Hello");
        span.Length.Should().Be(5);
    }

    [TestMethod]
    public void AsSpan_WithStart()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        ReadOnlySpan<char> span = builder.Slice(6);
        span.ToString().Should().Be("World");
        span.Length.Should().Be(5);
    }

    [TestMethod]
    public void AsSpan_WithStartAndLength()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello World");

        ReadOnlySpan<char> span = builder.Slice(0, 5);
        span.ToString().Should().Be("Hello");
        span.Length.Should().Be(5);
    }

    [TestMethod]
    public unsafe void AsSpan_WithTerminate()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Hello");

        ReadOnlySpan<char> span = builder.AsSpan(terminate: true);
        span.ToString().Should().Be("Hello");

        // Check that null terminator was added (though not included in the span)
        fixed (char* ptr = builder)
        {
            (*(ptr + 5)).Should().Be('\0');
        }
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void GetPinnableReference_Basic()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Append("Test");

        fixed (char* ptr = builder)
        {
            (*ptr).Should().Be('T');
        }
    }

    [TestMethod]
    public unsafe void GetPinnableReference_WithTerminate()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Test");

        ref char reference = ref builder.GetPinnableReference();
        reference.Should().Be('T');

        // Check that null terminator was added
        Unsafe.Add(ref reference, 4).Should().Be('\0');
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void Dispose_MultipleCallsSafe()
    {
        ValueStringBuilder builder = new(10);
        builder.Append("Test");

        builder.Dispose();
        builder.Dispose(); // Should not throw
    }

    [TestMethod]
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

    [TestMethod]
    public void EdgeCases_LargeContent()
    {
        using ValueStringBuilder builder = new(10);

        string largeString = new string('A', 1000);
        builder.Append(largeString.AsSpan());

        builder.Length.Should().Be(1000);
        builder.ToString().Should().Be(largeString);
    }

    [TestMethod]
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

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(10)]
    [DataRow(100)]
    [DataRow(1000)]
    public void Constructor_VariousCapacities(int capacity)
    {
        using ValueStringBuilder builder = new(capacity);
        builder.Capacity.Should().BeGreaterThanOrEqualTo(capacity);
        builder.Length.Should().Be(0);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("A")]
    [DataRow("Hello")]
    [DataRow("This is a longer string to test")]
    public void RoundTrip_StringOperations(string input)
    {
        using ValueStringBuilder builder = new(input.Length + 10);
        builder.Append(input.AsSpan());

        builder.ToString().Should().Be(input);
        builder.Length.Should().Be(input.Length);
    }

    [TestMethod]
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

    [TestMethod]
    [DataRow(10)]
    [DataRow(100)]
    public void AsHandler_Int(int value)
    {
        string result = TestFormat($"Hello, {value}!");
        result.Should().Be($"Hello, {value}!");
    }

    [TestMethod]
    [DataRow(DayOfWeek.Monday)]
    [DataRow(DayOfWeek.Friday)]
    public void AsHandler_Enum(DayOfWeek value)
    {
        string result = TestFormat($"Hello, it's {value}!");
        result.Should().Be($"Hello, it's {value}!");
    }

    [TestMethod]
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

    private static string TestFormat(ref ValueStringBuilder builder) => builder.ToStringAndDispose();

    [TestMethod]
    public void AppendFormat_Int()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Value: {0}", 42);
        builder.ToString().Should().Be("Value: 42");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_String()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Hello {0}!", Value.Create("World"));
        builder.ToString().Should().Be("Hello World!");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Byte()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Byte: {0}", (byte)255);
        builder.ToString().Should().Be("Byte: 255");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_SByte()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("SByte: {0}", (sbyte)-128);
        builder.ToString().Should().Be("SByte: -128");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Bool()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("True: {0}, False: {1}", true, false);
        builder.ToString().Should().Be("True: True, False: False");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Char()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Char: {0}", 'A');
        builder.ToString().Should().Be("Char: A");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Short()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("Short: {0}", (short)-32768);
        builder.ToString().Should().Be("Short: -32768");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_UShort()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("UShort: {0}", (ushort)65535);
        builder.ToString().Should().Be("UShort: 65535");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_UInt()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("UInt: {0}", 4294967295U);
        builder.ToString().Should().Be("UInt: 4294967295");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Long()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Long: {0}", -9223372036854775808L);
        builder.ToString().Should().Be("Long: -9223372036854775808");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_ULong()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("ULong: {0}", 18446744073709551615UL);
        builder.ToString().Should().Be("ULong: 18446744073709551615");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Float()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Float: {0}", 3.14159f);
        builder.ToString().Should().Be("Float: 3.14159");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Double()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Double: {0}", 3.141592653589793);
        builder.ToString().Should().Be(string.Format("Double: {0}", 3.141592653589793));
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Decimal()
    {
        using ValueStringBuilder builder = new(stackalloc char[30]);
        builder.AppendFormat("Decimal: {0}", 123.456m);
        builder.ToString().Should().Be("Decimal: 123.456");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_DateTime()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        DateTime dateTime = new(2025, 6, 18, 15, 30, 45);
        builder.AppendFormat("DateTime: {0}", dateTime);
        string expected = $"DateTime: {dateTime}";
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_SingleArg_DateTimeOffset()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        DateTimeOffset dateTimeOffset = new(2025, 6, 18, 15, 30, 45, TimeSpan.FromHours(-5));
        builder.AppendFormat("DateTimeOffset: {0}", dateTimeOffset);
        string expected = $"DateTimeOffset: {dateTimeOffset}";
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_SingleArg_NullableTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        int? nullableInt = 42;
        int? nullInt = null;

        builder.AppendFormat("Nullable: {0}, Null: {1}", nullableInt, nullInt);
        builder.ToString().Should().Be("Nullable: 42, Null: ");
    }

    [TestMethod]
    public void AppendFormat_SingleArg_Object()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        object obj = "Hello World";
        builder.AppendFormat("Object: {0}", Value.Create(obj));
        builder.ToString().Should().Be("Object: Hello World");
    }

    [TestMethod]
    public void AppendFormat_TwoArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Name: {0}, Age: {1}", "John", 25);
        builder.ToString().Should().Be("Name: John, Age: 25");
    }

    [TestMethod]
    public void AppendFormat_TwoArgs_ReversedOrder()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Age: {1}, Name: {0}", "Alice", 30);
        builder.ToString().Should().Be("Age: 30, Name: Alice");
    }

    [TestMethod]
    public void AppendFormat_ThreeArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("Name: {0}, Age: {1}, City: {2}", "Bob", 35, "Seattle");
        builder.ToString().Should().Be("Name: Bob, Age: 35, City: Seattle");
    }

    [TestMethod]
    public void AppendFormat_FourArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[150]);
        builder.AppendFormat("Name: {0}, Age: {1}, City: {2}, Country: {3}", "Charlie", 40, "London", "UK");
        builder.ToString().Should().Be("Name: Charlie, Age: 40, City: London, Country: UK");
    }

    [TestMethod]
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

    [TestMethod]
    public void AppendFormat_WithAlignment()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Left: '{0,-10}' Right: '{1,10}'", "Hello", "World");
        builder.ToString().Should().Be("Left: 'Hello     ' Right: '     World'");
    }

    [TestMethod]
    public void AppendFormat_WithAlignmentAndFormat()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Padded hex: '{0,8:X8}'", 255);
        builder.ToString().Should().Be("Padded hex: '000000FF'");
    }

    [TestMethod]
    public void AppendFormat_ReadOnlySpanArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        Value[] args = [42, "Hello", 3.14, true];
        builder.AppendFormat("Int: {0}, String: {1}, Double: {2}, Bool: {3}".AsSpan(), args.AsSpan());
        builder.ToString().Should().Be("Int: 42, String: Hello, Double: 3.14, Bool: True");
    }

    [TestMethod]
    public void AppendFormat_EmptyFormatString()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);
        builder.AppendFormat("", 42);
        builder.ToString().Should().Be("");
    }

    [TestMethod]
    public void AppendFormat_NoPlaceholders()
    {
        using ValueStringBuilder builder = new(stackalloc char[20]);
        builder.AppendFormat("No placeholders", 42);
        builder.ToString().Should().Be("No placeholders");
    }

    [TestMethod]
    public void AppendFormat_MultipleSameArgument()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{0} + {0} = {1}", 5, 10);
        builder.ToString().Should().Be("5 + 5 = 10");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{0}} = {0}", 42);
        builder.ToString().Should().Be("{0} = 42");
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void AppendFormat_StringFormatBehavior_NumberFormats()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test that behavior matches string.Format
        int number = 1234;
        string expected = string.Format("D: {0:D6}, F: {0:F2}, N: {0:N0}, P: {1:P1}", number, 0.1234);

        builder.AppendFormat("D: {0:D6}, F: {0:F2}, N: {0:N0}, P: {1:P1}", number, 0.1234);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_StringFormatBehavior_DateTimeFormats()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        DateTime dateTime = new(2025, 6, 18, 15, 30, 45);
        string expected = string.Format("Short: {0:d}, Long: {0:D}, Time: {0:t}", dateTime);

        builder.AppendFormat("Short: {0:d}, Long: {0:D}, Time: {0:t}", dateTime);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_StringFormatBehavior_CustomFormats()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        DateTime dateTime = new(2025, 6, 18, 15, 30, 45);
        string expected = string.Format("Custom: {0:yyyy-MM-dd HH:mm:ss}", dateTime);

        builder.AppendFormat("Custom: {0:yyyy-MM-dd HH:mm:ss}", dateTime);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    [DataRow("G")]
    [DataRow("N")]
    [DataRow("F")]
    [DataRow("E")]
    [DataRow("e")]
    [DataRow("C")]
    [DataRow("P")]
    public void AppendFormat_StandardNumericFormats(string format)
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        double value = 1234.5678;
        string formatString = "{0:" + format + "}";
        string expected = string.Format(formatString, value);

        builder.AppendFormat(formatString, value);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void AppendFormat_ArgumentIndexBoundaries(int argIndex)
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        Value[] args = ["First", "Second", "Third", "Fourth"];
        string formatString = $"Value: {{{argIndex}}}";

        builder.AppendFormat(formatString.AsSpan(), args.AsSpan());
        builder.ToString().Should().Be("Value: " + args[argIndex].As<object>().ToString());
    }

    [TestMethod]
    public void AppendFormat_LargeFormatString()
    {
        using ValueStringBuilder builder = new(stackalloc char[500]);

        string largeFormat = string.Join(" ", Enumerable.Range(0, 20).Select(i => $"{{{i}}}"));
        Value[] args = [.. Enumerable.Range(0, 20).Select(i => (Value)i)];

        builder.AppendFormat(largeFormat.AsSpan(), args.AsSpan());

        string expected = string.Join(" ", Enumerable.Range(0, 20));
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
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

        Value[] args = [stringVal, intVal, doubleVal, boolVal, dateVal];
        builder.AppendFormat("String: {0}, Int: {1:D5}, Double: {2:F2}, Bool: {3}, Date: {4:yyyy-MM-dd}".AsSpan(), args.AsSpan());

        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
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

    [TestMethod]
    public void AppendFormat_CustomFormatsWithPadding()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test custom numeric formats with padding
        int number = 42;
        string expected = string.Format("Binary-like: {0:0000000000}, Custom: {0:#,##0.00}", number);

        builder.AppendFormat("Binary-like: {0:0000000000}, Custom: {0:#,##0.00}", number);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_StringWithSpecialCharacters()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        string specialString = "Hello\tWorld\nNew Line\r\nCarriage Return";
        builder.AppendFormat("Special: '{0}'", specialString);
        builder.ToString().Should().Be($"Special: '{specialString}'");
    }

    [TestMethod]
    public void AppendFormat_NullValue()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        Value nullValue = Value.Create((object?)null);
        builder.AppendFormat("Null value: '{0}'", nullValue);
        builder.ToString().Should().Be("Null value: ''");
    }

    [TestMethod]
    public void AppendFormat_VeryLongString()
    {
        using ValueStringBuilder builder = new(stackalloc char[1000]);

        string longString = new('A', 500);
        string expected = $"Long: {longString}";

        builder.AppendFormat("Long: {0}", longString);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_MultipleFormatsSequentially()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);

        // Test multiple AppendFormat calls in sequence
        builder.AppendFormat("First: {0}", 1);
        builder.AppendFormat(", Second: {0}", 2);
        builder.AppendFormat(", Third: {0}", "three");

        builder.ToString().Should().Be("First: 1, Second: 2, Third: three");
    }

    [TestMethod]
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

    [TestMethod]
    public void AppendFormat_EscapedBraces_AtBeginning()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{Start}} {0}", "value");
        builder.ToString().Should().Be("{Start} value");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_AtEnd()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{0} {{End}}", "value");
        builder.ToString().Should().Be("value {End}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_OnlyEscaped()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{No}} {{Arguments}}", 42); // Need at least one argument
        builder.ToString().Should().Be("{No} {Arguments}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_Multiple()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{First}} {0} {{Second}} {1} {{Third}}", "arg1", "arg2");
        builder.ToString().Should().Be("{First} arg1 {Second} arg2 {Third}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_Consecutive()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{{{{0}}}}}", 42);
        builder.ToString().Should().Be("{{42}}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_Mixed()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("Value is {{0: {0}}} and {{1: {1}}}", 42, "test");
        builder.ToString().Should().Be("Value is {0: 42} and {1: test}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_WithFormatSpecifiers()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{Format: {0:X8}}}", 255);
        builder.ToString().Should().Be("{Format: 000000FF}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_WithAlignment()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{Padded: {0,10}}}", "test");
        builder.ToString().Should().Be("{Padded:       test}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_WithAlignmentAndFormat()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{Hex: {0,8:X8}}}", 255);
        builder.ToString().Should().Be("{Hex: 000000FF}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_ComplexPattern()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);
        builder.AppendFormat("{{obj: {{ name: \"{0}\", value: {1} }}}}", "test", 42);
        builder.ToString().Should().Be("{obj: { name: \"test\", value: 42 }}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_ArgumentIndexReuse()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{arg0}} = {0}, {{arg0}} = {0}", 42);
        builder.ToString().Should().Be("{arg0} = 42, {arg0} = 42");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_EmptyBetween()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{}}{0}{{}}", "middle");
        builder.ToString().Should().Be("{}middle{}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_QuadrupleOpening()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{{{{{0}", 42);
        builder.ToString().Should().Be("{{42");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_QuadrupleClosing()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("{0}}}}}", 42);
        builder.ToString().Should().Be("42}}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_NestedPattern()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("{{outer {{inner {0}}} outer}}", "value");
        builder.ToString().Should().Be("{outer {inner value} outer}");
    }

    [TestMethod]
    public void AppendFormat_EscapedBraces_AllTypesOfEscaping()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);
        builder.AppendFormat("{{}} {{ }} {0} }} {{ {{", "test");
        builder.ToString().Should().Be("{} { } test } { {");
    }

    // Define test enum types for the AppendFormat tests
    private enum TestEnum
    {
        First = 1,
        Second = 2,
        Third = 3
    }

    private enum TestEnumWithZero
    {
        Zero = 0,
        One = 1,
        Two = 2
    }

    [Flags]
    private enum TestFlagsEnum
    {
        None = 0,
        Flag1 = 1,
        Flag2 = 2,
        Flag3 = 4,
        Flag1And2 = Flag1 | Flag2,
        All = Flag1 | Flag2 | Flag3
    }

    [TestMethod]
    public void AppendFormat_SingleEnumArg()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Value: {0}", TestEnum.Second);
        builder.ToString().Should().Be("Value: Second");

        builder.Clear();
        builder.AppendFormat("Value: {0}", DayOfWeek.Wednesday);
        builder.ToString().Should().Be("Value: Wednesday");
    }

    [TestMethod]
    public void AppendFormat_MultipleEnumArgs()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        builder.AppendFormat("First: {0}, Second: {1}", Value.Create(TestEnum.First), Value.Create(DayOfWeek.Friday));
        builder.ToString().Should().Be("First: First, Second: Friday");

        builder.Clear();
        builder.AppendFormat("Values: {0}, {1}, {2}",
            Value.Create(TestEnum.First),
            Value.Create(TestEnum.Second),
            Value.Create(TestEnum.Third));
        builder.ToString().Should().Be("Values: First, Second, Third");
    }

    [TestMethod]
    public void AppendFormat_EnumWithZeroValue()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Value: {0}", TestEnumWithZero.Zero);
        builder.ToString().Should().Be("Value: Zero");
    }

    [TestMethod]
    public void AppendFormat_UndefinedEnumValue()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        // Cast to create an undefined enum value
        TestEnum undefinedValue = (TestEnum)42;

        builder.AppendFormat("Value: {0}", undefinedValue);
        builder.ToString().Should().Be("Value: 42");

        // Compare with string.Format behavior
        string expected = string.Format("Value: {0}", undefinedValue);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_SingleFlagsEnum()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Single flag
        builder.AppendFormat("Flags: {0}", TestFlagsEnum.Flag1);
        builder.ToString().Should().Be("Flags: Flag1");

        builder.Clear();
        // Multiple flags
        builder.AppendFormat("Flags: {0}", TestFlagsEnum.Flag1And2);
        builder.ToString().Should().Be("Flags: Flag1And2");

        builder.Clear();
        // All flags
        builder.AppendFormat("Flags: {0}", TestFlagsEnum.All);
        builder.ToString().Should().Be("Flags: All");
    }

    [TestMethod]
    public void AppendFormat_FlagsEnumNone()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Flags: {0}", TestFlagsEnum.None);
        builder.ToString().Should().Be("Flags: None");
    }

    [TestMethod]
    public void AppendFormat_UndefinedFlagsEnum()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        // Create an undefined flags enum value
        TestFlagsEnum undefinedFlag = (TestFlagsEnum)32;

        builder.AppendFormat("Flags: {0}", undefinedFlag);
        builder.ToString().Should().Be("Flags: 32");

        // Compare with string.Format behavior
        string expected = string.Format("Flags: {0}", undefinedFlag);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_CombinedDefinedAndUndefinedFlags()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        // Combine a defined flag with an undefined one
        TestFlagsEnum mixedFlags = TestFlagsEnum.Flag1 | (TestFlagsEnum)32;

        builder.AppendFormat("Flags: {0}", mixedFlags);
        // The output should be the numeric value since it can't represent this combination with names
        builder.ToString().Should().Be("Flags: 33");

        // Compare with string.Format behavior
        string expected = string.Format("Flags: {0}", mixedFlags);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_SystemFlagsEnum()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        FileAttributes attrs = FileAttributes.Hidden | FileAttributes.ReadOnly;
        builder.AppendFormat("Attributes: {0}", attrs);

        // Compare with string.Format behavior for system-defined enums
        string expected = string.Format("Attributes: {0}", attrs);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    [DataRow(ConsoleColor.Red)]
    [DataRow(ConsoleColor.Blue)]
    [DataRow(ConsoleColor.Yellow)]
    public void AppendFormat_EnumTheory(ConsoleColor color)
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);
        builder.AppendFormat("Color: {0}", color);
        builder.ToString().Should().Be($"Color: {color}");

        // Check that the output matches string.Format
        string expected = string.Format("Color: {0}", color);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumWithFormat()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Format "D" shows the decimal value
        builder.AppendFormat("Value: {0:D}", TestEnum.Second);
        builder.ToString().Should().Be("Value: 2");

        builder.Clear();
        // Format "X" shows the hex value
        builder.AppendFormat("Value: {0:X}", TestEnum.Second);
        builder.ToString().Should().Be("Value: 00000002");

        builder.Clear();
        // Format "G" is the default format (name)
        builder.AppendFormat("Value: {0:G}", TestEnum.Second);
        builder.ToString().Should().Be("Value: Second");
    }

    [TestMethod]
    public void AppendFormat_EnumWithFormatAndAlignment()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Right-aligned with format specifier
        builder.AppendFormat("Value: {0,10:D}", TestEnum.Second);
        builder.ToString().Should().Be("Value:          2");

        builder.Clear();

        // Left-aligned with format specifier
        builder.AppendFormat("Value: {0,-10:D}", TestEnum.Second);
        builder.ToString().Should().Be("Value: 2         ");
    }

    [TestMethod]
    public void AppendFormat_EnumArraySegment()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        string expected = string.Format("Values: {0}, {1}, {2}", TestEnum.First, TestFlagsEnum.Flag1And2, DayOfWeek.Monday);
        builder.AppendFormat(
            "Values: {0}, {1}, {2}",
            Value.Create(TestEnum.First),
            Value.Create(TestFlagsEnum.Flag1And2),
            Value.Create(DayOfWeek.Monday));

        builder.ToString().Should().Be("Values: First, Flag1And2, Monday");
        builder.ToString().Should().Be(expected);
    }

    // Comprehensive enum type definitions for all integer backing types
    private enum ByteEnum : byte
    {
        ByteFirst = 1,
        ByteSecond = 2,
        ByteMax = 255
    }

    [Flags]
    private enum ByteFlagsEnum : byte
    {
        ByteNone = 0,
        ByteFlag1 = 1,
        ByteFlag2 = 2,
        ByteFlag4 = 4,
        ByteAll = ByteFlag1 | ByteFlag2 | ByteFlag4
    }

    private enum SByteEnum : sbyte
    {
        SByteMin = -128,
        SByteFirst = -1,
        SByteZero = 0,
        SByteSecond = 1,
        SByteMax = 127
    }

    [Flags]
    private enum SByteFlagsEnum : sbyte
    {
        SByteNone = 0,
        SByteFlag1 = 1,
        SByteFlag2 = 2,
        SByteFlag4 = 4,
        SByteAll = SByteFlag1 | SByteFlag2 | SByteFlag4
    }

    private enum ShortEnum : short
    {
        ShortMin = -32768,
        ShortFirst = -1,
        ShortZero = 0,
        ShortSecond = 1,
        ShortMax = 32767
    }

    [Flags]
    private enum ShortFlagsEnum : short
    {
        ShortNone = 0,
        ShortFlag1 = 1,
        ShortFlag2 = 2,
        ShortFlag4 = 4,
        ShortAll = ShortFlag1 | ShortFlag2 | ShortFlag4
    }

    private enum UShortEnum : ushort
    {
        UShortFirst = 1,
        UShortSecond = 2,
        UShortMax = 65535
    }

    [Flags]
    private enum UShortFlagsEnum : ushort
    {
        UShortNone = 0,
        UShortFlag1 = 1,
        UShortFlag2 = 2,
        UShortFlag4 = 4,
        UShortAll = UShortFlag1 | UShortFlag2 | UShortFlag4
    }

    private enum IntEnum : int
    {
        IntMin = int.MinValue,
        IntFirst = -1,
        IntZero = 0,
        IntSecond = 1,
        IntMax = int.MaxValue
    }

    [Flags]
    private enum IntFlagsEnum : int
    {
        IntNone = 0,
        IntFlag1 = 1,
        IntFlag2 = 2,
        IntFlag4 = 4,
        IntAll = IntFlag1 | IntFlag2 | IntFlag4
    }

    private enum UIntEnum : uint
    {
        UIntFirst = 1,
        UIntSecond = 2,
        UIntMax = uint.MaxValue
    }

    [Flags]
    private enum UIntFlagsEnum : uint
    {
        UIntNone = 0,
        UIntFlag1 = 1,
        UIntFlag2 = 2,
        UIntFlag4 = 4,
        UIntAll = UIntFlag1 | UIntFlag2 | UIntFlag4
    }

    private enum LongEnum : long
    {
        LongMin = long.MinValue,
        LongFirst = -1,
        LongZero = 0,
        LongSecond = 1,
        LongMax = long.MaxValue
    }

    [Flags]
    private enum LongFlagsEnum : long
    {
        LongNone = 0,
        LongFlag1 = 1,
        LongFlag2 = 2,
        LongFlag4 = 4,
        LongAll = LongFlag1 | LongFlag2 | LongFlag4
    }

    private enum ULongEnum : ulong
    {
        ULongFirst = 1,
        ULongSecond = 2,
        ULongMax = ulong.MaxValue
    }

    [Flags]
    private enum ULongFlagsEnum : ulong
    {
        ULongNone = 0,
        ULongFlag1 = 1,
        ULongFlag2 = 2,
        ULongFlag4 = 4, ULongAll = ULongFlag1 | ULongFlag2 | ULongFlag4
    }

    [TestMethod]
    public void AppendFormat_ByteEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test direct enum formatting
        builder.AppendFormat("Value: {0}", ByteEnum.ByteSecond);
        builder.ToString().Should().Be("Value: ByteSecond");

        builder.Clear();

        // Test Value-wrapped enum formatting
        builder.AppendFormat("Value: {0}", Value.Create(ByteEnum.ByteMax));
        builder.ToString().Should().Be("Value: ByteMax");

        builder.Clear();

        // Test undefined positive value
        ByteEnum undefinedByte = (ByteEnum)42;
        builder.AppendFormat("Value: {0}", undefinedByte);
        builder.ToString().Should().Be("Value: 42");
        string expected = string.Format("Value: {0}", undefinedByte);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_ByteFlagsEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test single flag
        builder.AppendFormat("Flags: {0}", ByteFlagsEnum.ByteFlag1);
        builder.ToString().Should().Be("Flags: ByteFlag1");

        builder.Clear();

        // Test combined flags via Value
        builder.AppendFormat("Flags: {0}", Value.Create(ByteFlagsEnum.ByteAll));
        builder.ToString().Should().Be("Flags: ByteAll");

        builder.Clear();

        // Test undefined flags value
        ByteFlagsEnum undefinedFlags = (ByteFlagsEnum)64;
        builder.AppendFormat("Flags: {0}", undefinedFlags);
        builder.ToString().Should().Be("Flags: 64");
        string expected = string.Format("Flags: {0}", undefinedFlags);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_SByteEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test negative value
        builder.AppendFormat("Value: {0}", SByteEnum.SByteMin);
        builder.ToString().Should().Be("Value: SByteMin");

        builder.Clear();

        // Test Value-wrapped negative enum
        builder.AppendFormat("Value: {0}", Value.Create(SByteEnum.SByteFirst));
        builder.ToString().Should().Be("Value: SByteFirst");

        builder.Clear();

        // Test undefined negative value
        SByteEnum undefinedNegative = (SByteEnum)(-42);
        builder.AppendFormat("Value: {0}", undefinedNegative);
        builder.ToString().Should().Be("Value: -42");
        string expected = string.Format("Value: {0}", undefinedNegative);
        builder.ToString().Should().Be(expected);

        builder.Clear();

        // Test undefined positive value
        SByteEnum undefinedPositive = (SByteEnum)42;
        builder.AppendFormat("Value: {0}", undefinedPositive);
        builder.ToString().Should().Be("Value: 42");
        expected = string.Format("Value: {0}", undefinedPositive);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_ShortEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);        // Test extreme values
        builder.AppendFormat("Min: {0}, Max: {1}", Value.Create(ShortEnum.ShortMin), Value.Create(ShortEnum.ShortMax));
        builder.ToString().Should().Be("Min: ShortMin, Max: ShortMax");

        builder.Clear();

        // Test undefined values
        ShortEnum undefinedNegative = (ShortEnum)(-1000);
        ShortEnum undefinedPositive = (ShortEnum)1000;
        builder.AppendFormat("Negative: {0}, Positive: {1}", Value.Create(undefinedNegative), Value.Create(undefinedPositive));
        builder.ToString().Should().Be("Negative: -1000, Positive: 1000");
        string expected = string.Format("Negative: {0}, Positive: {1}", undefinedNegative, undefinedPositive);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_UShortEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test max value
        builder.AppendFormat("Value: {0}", UShortEnum.UShortMax);
        builder.ToString().Should().Be("Value: UShortMax");

        builder.Clear();

        // Test Value-wrapped and undefined
        UShortEnum undefined = (UShortEnum)30000;
        builder.AppendFormat("Values: {0}, {1}", Value.Create(UShortEnum.UShortFirst), Value.Create(undefined));
        builder.ToString().Should().Be("Values: UShortFirst, 30000");
        string expected = string.Format("Values: {0}, {1}", UShortEnum.UShortFirst, undefined);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_IntEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[150]);        // Test extreme values
        builder.AppendFormat("Min: {0}, Max: {1}", Value.Create(IntEnum.IntMin), Value.Create(IntEnum.IntMax));
        builder.ToString().Should().Be("Min: IntMin, Max: IntMax");

        builder.Clear();

        // Test undefined large values
        IntEnum undefinedLarge = (IntEnum)1000000;
        IntEnum undefinedNegativeLarge = (IntEnum)(-1000000);
        builder.AppendFormat("Large: {0}, NegLarge: {1}", Value.Create(undefinedLarge), Value.Create(undefinedNegativeLarge));
        builder.ToString().Should().Be("Large: 1000000, NegLarge: -1000000");
        string expected = string.Format("Large: {0}, NegLarge: {1}", undefinedLarge, undefinedNegativeLarge);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_UIntEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test max value
        builder.AppendFormat("Max: {0}", UIntEnum.UIntMax);
        builder.ToString().Should().Be("Max: UIntMax");

        builder.Clear();

        // Test undefined large value
        UIntEnum undefinedLarge = (UIntEnum)3000000000;
        builder.AppendFormat("Value: {0}", Value.Create(undefinedLarge));
        builder.ToString().Should().Be("Value: 3000000000");
        string expected = string.Format("Value: {0}", undefinedLarge);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_LongEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);        // Test extreme values
        builder.AppendFormat("Min: {0}, Max: {1}", Value.Create(LongEnum.LongMin), Value.Create(LongEnum.LongMax));
        builder.ToString().Should().Be("Min: LongMin, Max: LongMax");

        builder.Clear();

        // Test undefined very large values
        LongEnum undefinedVeryLarge = (LongEnum)9000000000000000000L;
        LongEnum undefinedVeryNegative = (LongEnum)(-9000000000000000000L);
        builder.AppendFormat("VeryLarge: {0}, VeryNeg: {1}", Value.Create(undefinedVeryLarge), Value.Create(undefinedVeryNegative));
        builder.ToString().Should().Be("VeryLarge: 9000000000000000000, VeryNeg: -9000000000000000000");
        string expected = string.Format("VeryLarge: {0}, VeryNeg: {1}", undefinedVeryLarge, undefinedVeryNegative);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_ULongEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[150]);

        // Test max value
        builder.AppendFormat("Max: {0}", ULongEnum.ULongMax);
        builder.ToString().Should().Be("Max: ULongMax");

        builder.Clear();

        // Test undefined very large value
        ULongEnum undefinedVeryLarge = (ULongEnum)18000000000000000000UL;
        builder.AppendFormat("Value: {0}", Value.Create(undefinedVeryLarge));
        builder.ToString().Should().Be("Value: 18000000000000000000");
        string expected = string.Format("Value: {0}", undefinedVeryLarge);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_AllFlagsEnumsWithCombinations()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);        // Test combinations across different backing types
        builder.AppendFormat("Byte: {0}, SByte: {1}, Short: {2}",
            Value.Create(ByteFlagsEnum.ByteFlag1 | ByteFlagsEnum.ByteFlag2),
            Value.Create(SByteFlagsEnum.SByteFlag1 | SByteFlagsEnum.SByteFlag4),
            Value.Create(ShortFlagsEnum.ShortAll));
        builder.ToString().Should().Be("Byte: ByteFlag1, ByteFlag2, SByte: SByteFlag1, SByteFlag4, Short: ShortAll");

        builder.Clear();

        // Test undefined flag combinations
        IntFlagsEnum undefinedCombination = IntFlagsEnum.IntFlag1 | (IntFlagsEnum)64;
        ULongFlagsEnum undefinedULongCombination = ULongFlagsEnum.ULongFlag2 | (ULongFlagsEnum)128;
        builder.AppendFormat("Undefined: {0}, ULongUndefined: {1}",
            Value.Create(undefinedCombination),
            Value.Create(undefinedULongCombination));
        builder.ToString().Should().Be("Undefined: 65, ULongUndefined: 130");
        string expected = string.Format("Undefined: {0}, ULongUndefined: {1}", undefinedCombination, undefinedULongCombination);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumFormatsWithAllBackingTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);        // Test decimal format on different backing types
        builder.AppendFormat("Byte: {0:D}, Short: {1:D}, Int: {2:D}",
            Value.Create(ByteEnum.ByteMax),
            Value.Create(ShortEnum.ShortMax),
            Value.Create(IntEnum.IntMax));
        builder.ToString().Should().Be("Byte: 255, Short: 32767, Int: 2147483647");

        builder.Clear();        // Test hex format
        builder.AppendFormat("Hex Byte: {0:X}, Hex UInt: {1:X}",
            Value.Create(ByteEnum.ByteMax),
            Value.Create(UIntEnum.UIntMax));
        builder.ToString().Should().Be("Hex Byte: FF, Hex UInt: FFFFFFFF");

        builder.Clear();        // Test general format (default)
        builder.AppendFormat("General Long: {0:G}, General ULong: {1:G}",
            Value.Create(LongEnum.LongSecond),
            Value.Create(ULongEnum.ULongSecond));
        builder.ToString().Should().Be("General Long: LongSecond, General ULong: ULongSecond");
    }

    [TestMethod]
    public void AppendFormat_EnumZeroValuesAllTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[300]);        // Test zero values across all backing types - split into multiple calls due to parameter limit
        builder.AppendFormat("Zeros: {0}, {1}, {2}, {3}",
            Value.Create(SByteEnum.SByteZero),
            Value.Create(ShortEnum.ShortZero),
            Value.Create(IntEnum.IntZero),
            Value.Create(LongEnum.LongZero));
        builder.AppendFormat(", {0}, {1}, {2}, {3}",
            Value.Create(ByteFlagsEnum.ByteNone),
            Value.Create(UShortFlagsEnum.UShortNone),
            Value.Create(UIntFlagsEnum.UIntNone),
            Value.Create(ULongFlagsEnum.ULongNone));
        builder.ToString().Should().Be("Zeros: SByteZero, ShortZero, IntZero, LongZero, ByteNone, UShortNone, UIntNone, ULongNone");
    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_Byte()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        ByteEnum enumValue = ByteEnum.ByteFirst;

        string expected = string.Format("Value: {0}", enumValue);
        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();
        directResult.Should().Be(expected);

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();
        valueResult.Should().Be(expected);

    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_SByte()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        SByteEnum enumValue = SByteEnum.SByteFirst;

        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();

        valueResult.Should().Be(directResult);
        string expected = $"Value: {enumValue}";
        directResult.Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_Short()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        ShortEnum enumValue = ShortEnum.ShortFirst;

        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();

        valueResult.Should().Be(directResult);
        string expected = $"Value: {enumValue}";
        directResult.Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_UShort()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        UShortEnum enumValue = UShortEnum.UShortFirst;

        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();

        valueResult.Should().Be(directResult);
        string expected = $"Value: {enumValue}";
        directResult.Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_Int()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        IntEnum enumValue = IntEnum.IntFirst;

        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();

        valueResult.Should().Be(directResult);
        string expected = $"Value: {enumValue}";
        directResult.Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_UInt()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        UIntEnum enumValue = UIntEnum.UIntFirst;

        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();

        valueResult.Should().Be(directResult);
        string expected = $"Value: {enumValue}";
        directResult.Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_Long()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        LongEnum enumValue = LongEnum.LongFirst;

        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();

        valueResult.Should().Be(directResult);
        string expected = $"Value: {enumValue}";
        directResult.Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_EnumAllBackingTypes_ULong()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);
        ULongEnum enumValue = ULongEnum.ULongFirst;

        builder.AppendFormat("Value: {0}", enumValue);
        string directResult = builder.ToString();

        builder.Clear();
        builder.AppendFormat("Value: {0}", Value.Create(enumValue));
        string valueResult = builder.ToString();

        valueResult.Should().Be(directResult);
        string expected = $"Value: {enumValue}";
        directResult.Should().Be(expected);
    }

    // Additional enum backing type tests
    private enum Int16Enum : short { First = 1, Negative = -1, Max = short.MaxValue }
    private enum UInt16Enum : ushort { First = 1, Max = ushort.MaxValue }
    private enum Int64Enum : long { First = 1, Negative = -1, Max = long.MaxValue }
    private enum UInt64Enum : ulong { First = 1, Max = ulong.MaxValue }

    [Flags] private enum Int16FlagsEnum : short { None = 0, Flag1 = 1, Flag2 = 2 }
    [Flags] private enum UInt64FlagsEnum : ulong { None = 0, Flag1 = 1, Flag2 = 2 }

    [TestMethod]
    public void AppendFormat_AdditionalIntegerBackedEnums()
    {
        using ValueStringBuilder builder = new(stackalloc char[200]);        // Test short-backed enums
        builder.AppendFormat("Short: {0}, UShort: {1}", Value.Create(Int16Enum.Negative), Value.Create(UInt16Enum.Max));
        builder.ToString().Should().Be("Short: Negative, UShort: Max");

        builder.Clear();

        // Test long-backed enums via Value
        builder.AppendFormat("Long: {0}, ULong: {1}", Value.Create(Int64Enum.Max), Value.Create(UInt64Enum.Max));
        string expected = string.Format("Long: {0}, ULong: {1}", Int64Enum.Max, UInt64Enum.Max);
        builder.ToString().Should().Be(expected);

        builder.Clear();

        // Test undefined values for different backing types
        Int16Enum undefinedShort = (Int16Enum)999;
        UInt64Enum undefinedULong = (UInt64Enum)999999999999UL;
        builder.AppendFormat("Undefined: {0}, {1}", Value.Create(undefinedShort), Value.Create(undefinedULong));
        builder.ToString().Should().Be("Undefined: 999, 999999999999");
    }

    [TestMethod]
    public void AppendFormat_FlagsEnumsAdditionalTypes()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test flags with different backing types
        builder.AppendFormat("Flags: {0}", Int16FlagsEnum.Flag1 | Int16FlagsEnum.Flag2);
        builder.ToString().Should().Be("Flags: Flag1, Flag2");

        builder.Clear();

        // Test undefined flags
        UInt64FlagsEnum undefinedFlags = (UInt64FlagsEnum)128;
        builder.AppendFormat("UndefinedFlags: {0}", Value.Create(undefinedFlags));
        builder.ToString().Should().Be("UndefinedFlags: 128");
        string expected = string.Format("UndefinedFlags: {0}", undefinedFlags);
        builder.ToString().Should().Be(expected);
    }

    [TestMethod]
    public void AppendFormat_InvalidFormatString_MissingClosingBrace()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Test format string with unclosed brace
        bool threwException = false;
        try
        {
            builder.AppendFormat("Value: {0", 42);
        }
        catch (FormatException)
        {
            threwException = true;
        }
        threwException.Should().BeTrue("Format string with unclosed brace should throw FormatException");
    }

    [TestMethod]
    public void AppendFormat_InvalidFormatString_InvalidAlignment()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Test format string with invalid alignment (non-numeric)
        bool threwException = false;
        try
        {
            builder.AppendFormat("Value: {0,abc}", 42);
        }
        catch (FormatException)
        {
            threwException = true;
        }
        threwException.Should().BeTrue("Format string with invalid alignment should throw FormatException");
    }

    [TestMethod]
    public void AppendFormat_InvalidFormatString_CloseBraceWithoutOpen()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Test format string with unexpected closing brace
        bool threwException = false;
        try
        {
            builder.AppendFormat("Value: }", 42);
        }
        catch (FormatException)
        {
            threwException = true;
        }
        threwException.Should().BeTrue("Format string with unexpected closing brace should throw FormatException");
    }

    [TestMethod]
    public void Dispose_AccessAfterDispose()
    {
        ValueStringBuilder builder = new(stackalloc char[20]);
        builder.Append("Test");

        // First dispose should succeed
        builder.Dispose();

        // Second dispose should not throw
        builder.Dispose();

        // Attempting to use after dispose - fields should be cleared
        builder.Length.Should().Be(0);
        builder.Capacity.Should().Be(0);
    }

    [TestMethod]
    public void EnsureCapacity_NegativeCapacity()
    {
        using ValueStringBuilder builder = new(stackalloc char[10]);

        // Negative capacity should throw
        try
        {
            builder.EnsureCapacity(-1);
            Assert.Fail("Expected ArgumentOutOfRangeException for negative capacity");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ex.ParamName.Should().Be("capacity");
        }
    }

    [TestMethod]
    public void AppendFormat_ComplexNestedBraces()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Test complex nested braces format
        builder.AppendFormat("{{{{Nested}}}} {0} {{Escaped}} {1:D5}", 42, 123);

        builder.ToString().Should().Be("{{Nested}} 42 {Escaped} 00123");
    }

    [TestMethod]
    public void AppendFormat_ManyConsecutiveEscapedBraces()
    {
        using ValueStringBuilder builder = new(stackalloc char[50]);

        // Test many consecutive escaped braces
        builder.AppendFormat("{{{{{{{0}}}}}}}", 42);

        builder.ToString().Should().Be("{{{42}}}");
    }

    [TestMethod]
    public void Append_AfterLengthManipulation()
    {
        ValueStringBuilder builder = new(stackalloc char[20]);

        builder.Append("Hello");
        builder.Length = 2;     // Truncate to "He"
        builder.Append("llo");  // Should append to truncated string

        builder.ToString().Should().Be("Hello");
    }

    [TestMethod]
    public void Length_InvalidValues()
    {
        ValueStringBuilder builder = new(stackalloc char[10]);
        builder.Append("Test");

        // Attempting to set negative length should throw
        try
        {
            builder.Length = -1;
            Assert.Fail("Expected ArgumentOutOfRangeException for negative length");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ex.ParamName.Should().Be("value");
        }

        // Attempting to set length > capacity should throw
        try
        {
            builder.Length = builder.Capacity + 1;
            Assert.Fail("Expected ArgumentOutOfRangeException for length greater than capacity");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            ex.ParamName.Should().Be("value");
        }

        builder.Dispose();
    }

    [TestMethod]
    public void AppendFormat_MaximumArgumentIndex()
    {
        using ValueStringBuilder builder = new(stackalloc char[100]);

        // Create a format string with highest valid index (int.MaxValue - 1)
        string formatWithMaxIndex = "{2147483646}";

        // Create an array with that many elements + 1
        // This would cause memory issues, so we'll mock it with a smaller array
        // and test the exception logic instead
        Value[] args = [42];

        bool threwException = false;
        try
        {
            builder.AppendFormat(formatWithMaxIndex.AsSpan(), args.AsSpan());
        }
        catch (FormatException)
        {
            threwException = true;
        }

        threwException.Should().BeTrue("Index beyond array bounds should throw FormatException");
    }

    [TestMethod]
    public void AppendFormat_WithExtremelyLargeInput()
    {
        // Test with very large input to ensure memory management works properly
        using ValueStringBuilder builder = new(10);

        // Create a large string (10,000 characters)
        string largeString = new string('A', 10000);

        // Append it
        builder.AppendFormat("Large: {0}", Value.Create(largeString));

        // Verify
        builder.Length.Should().Be(10007); // "Large: " + 10000 chars
        builder.ToString().Should().StartWith("Large: AAAAA");
        builder.ToString().Should().EndWith("AAAAA");
    }

    // ---- Branch-coverage tests ----

    [TestMethod]
    public void Insert_CharCount_TriggersGrow()
    {
        using ValueStringBuilder builder = new(stackalloc char[8]);
        builder.Append("abc");
        // Insert 20 X's at position 1, forcing a grow.
        builder.Insert(1, 'X', 20);
        builder.Length.Should().Be(23);
        builder.ToString().Should().StartWith("aXXXXXXXXXX");
        builder.ToString().Should().EndWith("XXXXbc");
    }

    [TestMethod]
    public void Insert_NegativeIndex_Throws()
    {
        ValueStringBuilder builder = new(stackalloc char[8]);
        bool threw = false;
        try
        {
            try
            {
                builder.Insert(-1, 'X', 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
        }
        finally
        {
            builder.Dispose();
        }

        threw.Should().BeTrue();
    }

    [TestMethod]
    public void Insert_IndexBeyondLength_Throws()
    {
        ValueStringBuilder builder = new(stackalloc char[8]);
        builder.Append("ab");
        bool threw = false;
        try
        {
            try
            {
                builder.Insert(5, 'X', 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
        }
        finally
        {
            builder.Dispose();
        }

        threw.Should().BeTrue();
    }

    [TestMethod]
    public void Insert_NegativeCount_Throws()
    {
        ValueStringBuilder builder = new(stackalloc char[8]);
        bool threw = false;
        try
        {
            try
            {
                builder.Insert(0, 'X', -1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
        }
        finally
        {
            builder.Dispose();
        }

        threw.Should().BeTrue();
    }

    [TestMethod]
    public void Insert_String_TriggersGrow()
    {
        using ValueStringBuilder builder = new(stackalloc char[8]);
        builder.Append("ab");
        builder.Insert(1, "abcdefghij");
        builder.Length.Should().Be(12);
        builder.ToString().Should().Be("aabcdefghijb");
    }

    [TestMethod]
    public void Replace_OldEqualsNew_NoOp()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("Hello");
        builder.Replace('l', 'l');
        builder.ToString().Should().Be("Hello");
    }

    [TestMethod]
    public void Replace_EmptyBuilder_NoOp()
    {
        using ValueStringBuilder builder = new(stackalloc char[8]);
        builder.Replace('a', 'b');
        builder.Length.Should().Be(0);
    }

    [TestMethod]
    public void Replace_ReplacesAllOccurrences()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("Hello");
        builder.Replace('l', 'X');
        builder.ToString().Should().Be("HeXXo");
    }

    [TestMethod]
    public void Append_Char_Count_TriggersGrow()
    {
        using ValueStringBuilder builder = new(stackalloc char[4]);
        builder.Append("ab");
        builder.Append('X', 20);
        builder.Length.Should().Be(22);
        builder.ToString().Should().StartWith("abXXXXX");
    }

    [TestMethod]
    public unsafe void Append_CharPointer_TriggersGrow()
    {
        using ValueStringBuilder builder = new(stackalloc char[4]);
        builder.Append("ab");
        char[] chars = "0123456789ABCDEF".ToCharArray();
        fixed (char* p = chars)
        {
            builder.Append(p, chars.Length);
        }

        builder.ToString().Should().Be("ab0123456789ABCDEF");
    }

    [TestMethod]
    public void EnsureCapacity_NegativeCapacity_Throws()
    {
        ValueStringBuilder builder = new(stackalloc char[8]);
        bool threw = false;
        try
        {
            try
            {
                builder.EnsureCapacity(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
        }
        finally
        {
            builder.Dispose();
        }

        threw.Should().BeTrue();
    }

    [TestMethod]
    public void EnsureCapacity_AlreadySufficient_NoGrow()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);
        int originalCapacity = builder.Capacity;
        builder.EnsureCapacity(8);
        builder.Capacity.Should().Be(originalCapacity);
    }

    [TestMethod]
    public void CopyTo_Stream_EmptyBuilder_NoOp()
    {
        using MemoryStream stream = new();
        using ValueStringBuilder builder = new(stackalloc char[8]);
        builder.CopyTo(stream);
        stream.Length.Should().Be(0);
    }

    [TestMethod]
    public void CopyTo_TextWriter_EmptyBuilder_NoOp()
    {
        System.IO.StringWriter writer = new();
        using ValueStringBuilder builder = new(stackalloc char[8]);
        builder.CopyTo(writer);
        writer.ToString().Should().BeEmpty();
    }

    [TestMethod]
    public void CopyTo_TextWriter_NullWriter_Throws()
    {
        ValueStringBuilder builder = new(stackalloc char[8]);
        builder.Append("x");
        bool threw = false;
        try
        {
            try
            {
                builder.CopyTo((System.IO.TextWriter)null!);
            }
            catch (ArgumentNullException)
            {
                threw = true;
            }
        }
        finally
        {
            builder.Dispose();
        }

        threw.Should().BeTrue();
    }

    [TestMethod]
    public void Constructor_LiteralLengthFormattedCount_AllocatesPooledBuffer()
    {
        using ValueStringBuilder builder = new(literalLength: 8, formattedCount: 2);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Constructor_LiteralLengthFormattedCount_Provider_AllocatesPooledBuffer()
    {
        using ValueStringBuilder builder = new(literalLength: 4, formattedCount: 1, provider: CultureInfo.InvariantCulture);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Constructor_LiteralLengthFormattedCount_InitialBuffer_UsesProvidedBuffer()
    {
        using ValueStringBuilder builder = new(
            literalLength: 4,
            formattedCount: 1,
            provider: null,
            initialBuffer: stackalloc char[16]);
        builder.Length.Should().Be(0);
        builder.Capacity.Should().Be(16);
    }

    [TestMethod]
    public void Constructor_Span_WithFormatProvider_Works()
    {
        ValueStringBuilder builder = new(stackalloc char[8], CultureInfo.InvariantCulture);
        try
        {
            builder.Append("hi");
            builder.ToString().Should().Be("hi");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Constructor_InitialCapacity_WithFormatProvider_Works()
    {
        using ValueStringBuilder builder = new(initialCapacity: 32, provider: CultureInfo.InvariantCulture);
        builder.Append("ok");
        builder.ToString().Should().Be("ok");
    }

    [TestMethod]
    public void AppendSpan_RequestedLength_ReturnsWritableRegionAndAdvancesLength()
    {
        ValueStringBuilder builder = new(stackalloc char[8]);
        try
        {
            Span<char> region = builder.AppendSpan(4);
            region.Length.Should().Be(4);
            region[0] = 'a';
            region[1] = 'b';
            region[2] = 'c';
            region[3] = 'd';
            builder.Length.Should().Be(4);
            builder.ToString().Should().Be("abcd");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendSpan_GrowsBufferWhenExceedingCapacity()
    {
        ValueStringBuilder builder = new(stackalloc char[2]);
        try
        {
            Span<char> region = builder.AppendSpan(8);
            region.Length.Should().Be(8);
            "abcdefgh".AsSpan().CopyTo(region);
            builder.Length.Should().Be(8);
            builder.ToString().Should().Be("abcdefgh");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendSpan_NegativeLength_Throws()
    {
        ValueStringBuilder builder = new(stackalloc char[4]);
        bool threw = false;
        try
        {
            try
            {
                builder.AppendSpan(-1);
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
        }
        finally
        {
            builder.Dispose();
        }

        threw.Should().BeTrue();
    }

    [TestMethod]
    public void RangeIndexer_ReturnsRequestedSubsection()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.Append("Hello World");
            builder[6..].ToString().Should().Be("World");
            builder[..5].ToString().Should().Be("Hello");
            builder[1..4].ToString().Should().Be("ell");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_ReadOnlySpan_AppendsContent()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted("hello".AsSpan());
            builder.ToString().Should().Be("hello");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_StringWithAlignmentAndFormat_RightAlignsValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(value: "x", alignment: 5, format: null);
            builder.ToString().Should().Be("    x");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_StringWithNegativeAlignment_LeftAlignsValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(value: "x", alignment: -5, format: null);
            builder.ToString().Should().Be("x    ");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_ValueWithStringFormat_AppliesFormat()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(Value.Create(255), format: "X");
            builder.ToString().Should().Be("FF");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_GenericValueWithAlignment_RightAlignsValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted<int>(42, alignment: 5);
            builder.ToString().Should().Be("   42");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_GenericValueWithAlignmentZero_DoesNotPad()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted<int>(42, alignment: 0);
            builder.ToString().Should().Be("42");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_ReadOnlySpanWithAlignment_RightAlignsValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted("ab".AsSpan(), alignment: 5);
            builder.ToString().Should().Be("   ab");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_ReadOnlySpanWithNegativeAlignment_LeftAlignsValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted("ab".AsSpan(), alignment: -5);
            builder.ToString().Should().Be("ab   ");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_ReadOnlySpanWithAlignmentSmallerThanValue_AppendsUnchanged()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted("abcdef".AsSpan(), alignment: 2);
            builder.ToString().Should().Be("abcdef");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_Object_NullValue_AppendsNothing()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted((object?)null);
            builder.ToString().Should().Be(string.Empty);
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_GenericWithStringFormat_AppliesFormat()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted<int>(255, format: "X");
            builder.ToString().Should().Be("FF");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_NullStringValue_AppendsNothing()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted((string?)null);
            builder.ToString().Should().Be(string.Empty);
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void AppendFormatted_CustomFormatter_InterceptsValue()
    {
        UpperCaseFormatProvider provider = new();
        ValueStringBuilder builder = new(literalLength: 0, formattedCount: 1, provider: provider, initialBuffer: stackalloc char[32]);
        try
        {
            builder.AppendFormatted("hi");
            builder.AppendFormatted<int>(42);
            builder.ToString().Should().Be("[HI][42]");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Append_InterpolatedString_AppendsToExistingContent()
    {
        using ValueStringBuilder builder = new(stackalloc char[64]);
        builder.Append("Start: ");

        int value = 42;
        builder.Append($"value={value}");

        builder.ToString().Should().Be("Start: value=42");
    }

    [TestMethod]
    public void Append_InterpolatedString_OnlyLiteral_AppendsLiteral()
    {
        using ValueStringBuilder builder = new(stackalloc char[32]);

        builder.Append($"just literal text");

        builder.ToString().Should().Be("just literal text");
    }

    [TestMethod]
    public void Append_InterpolatedString_MultipleHoles_FormatsEach()
    {
        using ValueStringBuilder builder = new(stackalloc char[64]);
        int a = 1;
        string b = "two";
        int c = 3;

        builder.Append($"{a}-{b}-{c}");

        builder.ToString().Should().Be("1-two-3");
    }

    [TestMethod]
    public void Append_InterpolatedString_WithFormatSpecifier_AppliesFormat()
    {
        using ValueStringBuilder builder = new(stackalloc char[32]);
        int value = 255;

        builder.Append($"hex={value:X4}");

        builder.ToString().Should().Be("hex=00FF");
    }

    [TestMethod]
    public void Append_InterpolatedString_GrowsBeyondInitialBuffer()
    {
        // Start with a tiny stack buffer so the interpolated content forces a pool rental.
        using ValueStringBuilder builder = new(stackalloc char[4]);
        builder.Append("AB");

        string large = new('x', 500);
        builder.Append($"{large}");

        builder.Length.Should().Be(502);
        builder.ToString().Should().Be("AB" + large);
    }

    [TestMethod]
    public void Append_InterpolatedString_MultipleCalls_Accumulate()
    {
        using ValueStringBuilder builder = new(stackalloc char[64]);
        int x = 1;
        int y = 2;

        builder.Append($"a={x}");
        builder.Append($";b={y}");

        builder.ToString().Should().Be("a=1;b=2");
    }

    [TestMethod]
    public void Append_InterpolatedString_MatchesStringBuilder()
    {
        int id = 7;
        string name = "widget";

        using ValueStringBuilder builder = new(stackalloc char[64]);
        builder.Append($"id={id}, name={name}");

        StringBuilder expected = new();
        expected.Append($"id={id}, name={name}");

        builder.ToString().Should().Be(expected.ToString());
    }

    [TestMethod]
    public void Append_InterpolatedString_EmptyContent_AppendsNothing()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("kept");

        builder.Append($"");

        builder.ToString().Should().Be("kept");
        builder.Length.Should().Be(4);
    }

    [TestMethod]
    public void Append_InterpolatedStringWithCustomProvider_UsesProvider()
    {
        UpperCaseFormatProvider provider = new();
        ValueStringBuilder builder = new(literalLength: 0, formattedCount: 0, provider: provider, initialBuffer: stackalloc char[64]);
        try
        {
            string text = "hi";
            builder.Append($"x{text}");

            builder.ToString().Should().Be("x[HI]");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Append_InterpolatedStringHoleMutatesBuilder_AppendsAfterMutation()
    {
        ValueStringBuilder builder = new(stackalloc char[64]);
        try
        {
            builder.Append("a");

            builder.Append($"b{AppendAndReturn(ref builder, "c", "d")}");

            builder.ToString().Should().Be("acbd");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Append_InterpolatedStringReferencingSelf_AppendsSnapshotOfReceiver()
    {
        using ValueStringBuilder builder = new(stackalloc char[64]);
        builder.Append("A");

        // The hole reads the receiver through its implicit ReadOnlySpan<char> conversion. The
        // interpolation is built in an independent buffer, so the hole sees a snapshot of the
        // receiver ("A") taken when the hole is evaluated, not the in-progress interpolation.
        builder.Append($"pre{builder}post");

        builder.ToString().Should().Be("ApreApost");
    }

    [TestMethod]
    public void AppendLine_InterpolatedString_AppendsContentAndNewLine()
    {
        using ValueStringBuilder builder = new(stackalloc char[64]);
        int value = 42;

        builder.AppendLine($"value={value}");

        builder.ToString().Should().Be("value=42" + Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_InterpolatedString_AppendsToExistingContent()
    {
        using ValueStringBuilder builder = new(stackalloc char[64]);
        builder.Append("header");

        builder.AppendLine($" {1}");

        builder.ToString().Should().Be("header 1" + Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_InterpolatedString_MultipleLines_ProduceMultipleNewLines()
    {
        using ValueStringBuilder builder = new(stackalloc char[64]);

        builder.AppendLine($"line {1}");
        builder.AppendLine($"line {2}");

        builder.ToString().Should().Be(
            "line 1" + Environment.NewLine + "line 2" + Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_InterpolatedString_EmptyContent_AppendsOnlyNewLine()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);

        builder.AppendLine($"");

        builder.ToString().Should().Be(Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_InterpolatedString_GrowsBeyondInitialBuffer()
    {
        using ValueStringBuilder builder = new(stackalloc char[4]);

        string large = new('y', 300);
        builder.AppendLine($"{large}");

        builder.ToString().Should().Be(large + Environment.NewLine);
    }

    [TestMethod]
    public void Append_And_AppendLine_Interpolated_Chained()
    {
        using ValueStringBuilder builder = new(stackalloc char[128]);
        int count = 3;

        builder.Append($"count={count}");
        builder.AppendLine($" done");
        builder.Append($"next");

        builder.ToString().Should().Be("count=3 done" + Environment.NewLine + "next");
    }

    [TestMethod]
    public void AppendLine_NoArgs_AppendsNewLine()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);
        builder.Append("text");

        builder.AppendLine();

        builder.ToString().Should().Be("text" + Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_NoArgs_OnEmpty_AppendsOnlyNewLine()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);

        builder.AppendLine();

        builder.ToString().Should().Be(Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_NoArgs_MultipleCalls_AppendMultipleNewLines()
    {
        using ValueStringBuilder builder = new(stackalloc char[16]);

        builder.AppendLine();
        builder.AppendLine();

        builder.ToString().Should().Be(Environment.NewLine + Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_String_AppendsValueAndNewLine()
    {
        using ValueStringBuilder builder = new(stackalloc char[32]);
        builder.Append("start ");
        string value = "hello";

        builder.AppendLine(value);

        builder.ToString().Should().Be("start hello" + Environment.NewLine);
    }

    [TestMethod]
    public void AppendLine_StringSegment_AppendsSegmentAndNewLine()
    {
        using ValueStringBuilder builder = new(stackalloc char[32]);
        StringSegment segment = new("Hello World", 0, 5);

        builder.AppendLine(segment);

        builder.ToString().Should().Be("Hello" + Environment.NewLine);
    }

    private static string AppendAndReturn(ref ValueStringBuilder builder, string text, string result)
    {
        builder.Append(text);
        return result;
    }

    private sealed class UpperCaseFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object? GetFormat(Type? formatType) =>
            formatType == typeof(ICustomFormatter) ? this : null;

        public string Format(string? format, object? arg, IFormatProvider? formatProvider)
        {
            string text = arg?.ToString() ?? string.Empty;
            return "[" + text.ToUpperInvariant() + "]";
        }
    }
}
