// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Text;

namespace Touki;

[TestClass]
public class CoverageGaps
{
    // Exercises the explicit conversion operator `(double?)value`.
    [TestMethod]
    public void ExplicitOperator_NullableDouble_RoundTrips()
    {
        Value value = 3.14;
        double? result = (double?)value;
        result.Should().Be(3.14);
    }

    // Exercises the explicit conversion operator `(ArraySegment<byte>)value`.
    [TestMethod]
    public void ExplicitOperator_ArraySegmentByte_RoundTrips()
    {
        byte[] array = [1, 2, 3, 4];
        ArraySegment<byte> segment = new(array, 1, 2);
        Value value = segment;
        ArraySegment<byte> result = (ArraySegment<byte>)value;
        result.Should().Equal(segment);
    }

    // Exercises the explicit conversion operator `(ArraySegment<char>)value`.
    [TestMethod]
    public void ExplicitOperator_ArraySegmentChar_RoundTrips()
    {
        char[] array = ['a', 'b', 'c', 'd'];
        ArraySegment<char> segment = new(array, 1, 2);
        Value value = segment;
        ArraySegment<char> result = (ArraySegment<char>)value;
        result.Should().Equal(segment);
    }

    // Storing a default(ArraySegment<char>) (null Array) should throw ArgumentNullException.
    [TestMethod]
    public void Value_FromDefaultArraySegmentChar_Throws()
    {
        ArraySegment<char> segment = default;
        Action action = () => { Value _ = segment; };
        action.Should().Throw<ArgumentNullException>();
    }

    // As<string>() on a stored string should return the string (covers
    // TryGetObjectSlow's success branch for string).
    [TestMethod]
    public void As_String_FromStoredString_ReturnsString()
    {
        Value value = "hello";
        value.As<string>().Should().Be("hello");
    }

    // As<string>() on a stored non-string value type should fail.
    [TestMethod]
    public void As_String_FromStoredByte_ThrowsInvalidCast()
    {
        Value value = (byte)5;
        Action action = () => value.As<string>();
        action.Should().Throw<InvalidCastException>();
    }

    // As<string>() on a stored StringSegment (string + non-zero union) should fail.
    [TestMethod]
    public void As_String_FromStringSegment_ThrowsInvalidCast()
    {
        StringSegment segment = new("hello world", 6, 5);
        Value value = segment;
        Action action = () => value.As<string>();
        action.Should().Throw<InvalidCastException>();
    }

    // TryGetValue<T> for a value type that doesn't match anything stored
    // should return false (covers the outermost else in TryGetValueSlow).
    [TestMethod]
    public void TryGetValue_UnmatchedValueType_ReturnsFalse()
    {
        Value value = 42;
        value.TryGetValue(out TimeSpan _).Should().BeFalse();
    }

    // As<StringSegment>() when the stored value isn't a string should fail
    // (covers the inner else that sets value = default!).
    [TestMethod]
    public void TryGetValue_StringSegment_FromNonString_ReturnsFalse()
    {
        Value value = (byte)5;
        value.TryGetValue(out StringSegment _).Should().BeFalse();
    }

    // Nullable long-backed enum exercises the size==16 branch of the
    // nullable-enum switch in TryGetValueSlow.
    private enum LongEnum : long
    {
        Zero = 0,
        Max = long.MaxValue
    }

    [TestMethod]
    public void TryGetValue_NullableLongEnum_RoundTrips()
    {
        Value value = Value.Create(LongEnum.Max);
        value.TryGetValue(out LongEnum? result).Should().BeTrue();
        result.Should().Be(LongEnum.Max);
    }

    // Format paths for primitive TypeFlag values that don't hit the inlined
    // fast path: char, byte, sbyte, short, ushort, DateTimeOffset.
    [TestMethod]
    public void Format_Char_WritesValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(Value.Create('Q'));
            builder.AsSpan().ToString().Should().Be("Q");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Format_Byte_WritesValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(Value.Create((byte)200));
            builder.AsSpan().ToString().Should().Be("200");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Format_SByte_WritesValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(Value.Create((sbyte)-100));
            builder.AsSpan().ToString().Should().Be("-100");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Format_Short_WritesValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(Value.Create((short)-1234));
            builder.AsSpan().ToString().Should().Be("-1234");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Format_UShort_WritesValue()
    {
        ValueStringBuilder builder = new(stackalloc char[16]);
        try
        {
            builder.AppendFormatted(Value.Create((ushort)50000));
            builder.AsSpan().ToString().Should().Be("50000");
        }
        finally
        {
            builder.Dispose();
        }
    }

    [TestMethod]
    public void Format_DateTimeOffset_WritesValue()
    {
        DateTimeOffset dto = new(2025, 1, 2, 3, 4, 5, TimeSpan.FromHours(5));
        ValueStringBuilder builder = new(stackalloc char[64]);
        try
        {
            builder.AppendFormatted(Value.Create(dto));
            builder.AsSpan().ToString().Should().Be(dto.ToString());
        }
        finally
        {
            builder.Dispose();
        }
    }

    // Format path for a Value containing a non-full StringSegment (string + non-zero union).
    [TestMethod]
    public void Format_StringSegment_WritesSegmentText()
    {
        StringSegment segment = new("hello world", 6, 5);
        Value value = segment;
        ValueStringBuilder builder = new(stackalloc char[32]);
        try
        {
            builder.AppendFormatted(value);
            builder.AsSpan().ToString().Should().Be("world");
        }
        finally
        {
            builder.Dispose();
        }
    }

    // A DateTimeOffset whose offset is not a 30-minute multiple cannot fit in
    // PackedDateTimeOffset and falls through to the boxing path.
    [TestMethod]
    public void Value_DateTimeOffset_UnpackableOffset_FallsBackToBoxing()
    {
        // UTC+5:45 (Nepal) is 345 minutes, not a multiple of 30.
        DateTimeOffset dto = new(2025, 6, 15, 12, 0, 0, TimeSpan.FromMinutes(345));
        Value value = dto;
        value.As<DateTimeOffset>().Should().Be(dto);
        value.Type.Should().Be(typeof(DateTimeOffset));
    }
}
