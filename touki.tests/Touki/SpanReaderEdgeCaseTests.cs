// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
/// Edge case and bug validation tests for SpanReader{T}.
/// </summary>
public class SpanReaderEdgeCaseTests
{
    [Fact]
    public void Position_Setter_AllowsSpanLength()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span)
        {
            // Setting position to span length should be valid (points to end)
            Position = 3
        };

        reader.Position.Should().Be(3);
        reader.End.Should().BeTrue();
        reader.Unread.Length.Should().Be(0);
    }

    [Fact]
    public void Position_Setter_ThrowsOnValueGreaterThanLength()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        try
        {
            reader.Position = 4;
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void Position_Setter_ThrowsOnNegativeValue()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        try
        {
            reader.Position = -1;
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void TryRead_Count_ValidatesNegativeCount()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        try
        {
            reader.TryRead(-1, out ReadOnlySpan<byte> _);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void TryRead_Count_HandlesZeroCountCorrectly()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        bool result = reader.TryRead(0, out ReadOnlySpan<byte> read);
        result.Should().BeTrue();
        read.Length.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void TryRead_Count_HandlesIntMaxValue()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        bool result = reader.TryRead(int.MaxValue, out ReadOnlySpan<byte> read);
        result.Should().BeFalse();
        read.Length.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void Advance_ValidatesNegativeCount()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        try
        {
            reader.Advance(-1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void Advance_ThrowsWhenAdvancingPastEnd()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        try
        {
            reader.Advance(4);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void Advance_AllowsAdvancingToEnd()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);
        reader.Advance(3);
        reader.Position.Should().Be(3);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void Rewind_ValidatesNegativeCount()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        try
        {
            reader.Rewind(-1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void Rewind_ThrowsWhenRewindingBeforeStart()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);
        reader.Advance(1);

        try
        {
            reader.Rewind(2);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void Rewind_AllowsRewindingToStart()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);
        reader.Advance(2);

        reader.Rewind(2);
        reader.Position.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Rewind_HandlesZeroCount(int initialPosition)
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span)
        {
            Position = initialPosition
        };

        reader.Rewind(0);
        reader.Position.Should().Be(initialPosition);
    }

    [Fact]
    public void TryRead_Struct_ValidatesTypeSizeAlignment()
    {
        // Test with a case where TValue size is not evenly divisible by T size
        // ushort = 2 bytes, and uint = 4 bytes, but we'll use a 3-byte span element type
        // Let's use a case that actually fails: trying to read a 3-byte value from 2-byte elements

        // Since C# doesn't have 3-byte built-in types, let's test the opposite:
        // Try to read a smaller type that doesn't divide evenly
        ReadOnlySpan<ushort> span = [1, 2]; // 2-byte elements
        SpanReader<ushort> reader = new(span);

        try
        {
            // Try to read a byte (1 byte) from ushort span (2-byte elements)
            // sizeof(byte) < sizeof(ushort) should trigger the exception
            reader.TryRead<byte>(out byte _);
            Assert.Fail($"Expected {nameof(ArgumentException)}");
        }
        catch (ArgumentException ex)
        {
            ex.Message.Should().Contain("evenly divisible");
        }
    }

    [Fact]
    public void TryRead_Struct_Count_ValidatesTypeSizeAlignment()
    {
        ReadOnlySpan<ushort> span = [1, 2, 3, 4]; // 2-byte elements
        SpanReader<ushort> reader = new(span);

        try
        {
            // Try to read bytes from ushort span - should throw because byte size < ushort size
            reader.TryRead<byte>(2, out ReadOnlySpan<byte> _);
            Assert.Fail($"Expected {nameof(ArgumentException)}");
        }
        catch (ArgumentException ex)
        {
            ex.Message.Should().Contain("evenly divisible");
        }
    }

    [Fact]
    public void TryRead_Struct_Count_ValidatesNegativeCount()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4];
        SpanReader<byte> reader = new(span);

        // Even though this would throw due to type validation first,
        // let's test with a valid type combination but negative count
        try
        {
            reader.TryRead<byte>(-1, out ReadOnlySpan<byte> _);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void TryRead_Struct_HandlesIntegerOverflowInSizeCalculation()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4];
        SpanReader<byte> reader = new(span);

        // Test with a very large count that would cause integer overflow
        bool result = reader.TryRead<byte>(int.MaxValue, out ReadOnlySpan<byte> read);
        result.Should().BeFalse();
        read.Length.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void TryReadTo_EmptyDelimiterSpan()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        ReadOnlySpan<byte> emptyDelimiters = [];
        bool result = reader.TryReadToAny(emptyDelimiters, true, out ReadOnlySpan<byte> read);
        result.Should().BeFalse();
        read.Length.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void TrySplitAny_EmptyDelimiterSpan()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        ReadOnlySpan<byte> emptyDelimiters = [];
        bool result = reader.TrySplitAny(emptyDelimiters, out ReadOnlySpan<byte> split);
        result.Should().BeTrue();
        split.ToArray().Should().BeEquivalentTo([1, 2, 3]);
        reader.Position.Should().Be(3);
    }

    [Fact]
    public void TryAdvancePast_EmptyPattern()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        ReadOnlySpan<byte> emptyPattern = [];
        bool result = reader.TryAdvancePast(emptyPattern);
        result.Should().BeTrue();
        reader.Position.Should().Be(0); // Should not advance
    }

    [Fact]
    public void AdvancePast_WithAllMatchingValues()
    {
        ReadOnlySpan<byte> span = [5, 5, 5, 5, 5];
        SpanReader<byte> reader = new(span);

        int advanced = reader.AdvancePast(5);
        advanced.Should().Be(5);
        reader.Position.Should().Be(5);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void AdvancePast_WithSingleValue()
    {
        ReadOnlySpan<byte> span = [5];
        SpanReader<byte> reader = new(span);

        int advanced = reader.AdvancePast(5);
        advanced.Should().Be(1);
        reader.Position.Should().Be(1);
        reader.End.Should().BeTrue();
    }

    [Fact]
    public void AdvancePast_WithNoMatchingValues()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        int advanced = reader.AdvancePast(5);
        advanced.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void Properties_RemainingReadOnlyAfterOperations()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        // Store original references
        ReadOnlySpan<byte> originalSpan = reader.Span;
        int originalLength = reader.Length;

        // Perform various operations
        reader.Advance(2);
        reader.TryRead(out byte _);
        reader.Rewind(1);

        // Original properties should remain unchanged
        reader.Span.ToArray().Should().BeEquivalentTo(originalSpan.ToArray());
        reader.Length.Should().Be(originalLength);
    }

    [Fact]
    public void Unread_ReflectsCurrentPosition()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.Unread.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 5]);

        reader.Advance(2);
        reader.Unread.ToArray().Should().BeEquivalentTo([3, 4, 5]);

        reader.Advance(3);
        reader.Unread.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void TryReadTo_DelimiterAtEveryPosition()
    {
        ReadOnlySpan<byte> span = [1, 1, 1, 1, 1];
        SpanReader<byte> reader = new(span);

        // First read should get empty span since delimiter is at start
        bool result = reader.TryReadTo(1, out ReadOnlySpan<byte> read);
        result.Should().BeTrue();
        read.Length.Should().Be(0);
        reader.Position.Should().Be(1);

        // Subsequent reads should also get empty spans
        for (int i = 0; i < 4; i++)
        {
            result = reader.TryReadTo(1, out read);
            result.Should().BeTrue();
            read.Length.Should().Be(0);
            reader.Position.Should().Be(i + 2);
        }

        reader.End.Should().BeTrue();
    }

    [Fact]
    public void TryReadToAny_TwoDelimitersOptimizationPath()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        // Test the special optimization for exactly 2 delimiters
        ReadOnlySpan<byte> twoDelimiters = [3, 9];
        bool result = reader.TryReadToAny(twoDelimiters, true, out ReadOnlySpan<byte> read);
        result.Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1, 2]);
        reader.Position.Should().Be(3);
    }

    [Fact]
    public void TryReadToAny_MoreThanTwoDelimitersGeneralPath()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        // Test the general path for more than 2 delimiters
        ReadOnlySpan<byte> multipleDelimiters = [3, 7, 9, 11];
        bool result = reader.TryReadToAny(multipleDelimiters, true, out ReadOnlySpan<byte> read);
        result.Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1, 2]);
        reader.Position.Should().Be(3);
    }

    [Fact]
    public void TryRead_Struct_EdgeCaseSizes()
    {
        // Test reading different struct sizes
        ReadOnlySpan<byte> span = new byte[16]; // 16 bytes
        SpanReader<byte> reader = new(span);

        // Test reading a 16-byte struct (should succeed)
        bool result = reader.TryRead<decimal>(out decimal _);
        result.Should().BeTrue();
        reader.Position.Should().Be(16);
        reader.End.Should().BeTrue();

        reader.Reset();

        // Test reading a 1-byte value
        result = reader.TryRead<byte>(out byte _);
        result.Should().BeTrue();
        reader.Position.Should().Be(1);
    }

    [Fact]
    public void TryRead_Struct_Count_EdgeCases()
    {
        ReadOnlySpan<byte> span = new byte[16];
        SpanReader<byte> reader = new(span);

        // Test reading exactly the maximum number of items that fit
        bool result = reader.TryRead<uint>(4, out ReadOnlySpan<uint> values); // 4 * 4 = 16 bytes
        result.Should().BeTrue();
        values.Length.Should().Be(4);
        reader.Position.Should().Be(16);
        reader.End.Should().BeTrue();

        reader.Reset();

        // Test reading one more than what fits
        result = reader.TryRead<uint>(5, out values); // 5 * 4 = 20 bytes (too much)
        result.Should().BeFalse();
        values.Length.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void Position_Setter_BoundaryValues()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span)
        {
            // Test setting to 0
            Position = 1
        };
        reader.Position = 0;
        reader.Position.Should().Be(0);
        reader.Unread.ToArray().Should().BeEquivalentTo([1, 2, 3]);

        // Test setting to exact length (end)
        reader.Position = 3;
        reader.Position.Should().Be(3);
        reader.Unread.Length.Should().Be(0);
        reader.End.Should().BeTrue();

        // Test setting back to middle
        reader.Position = 1;
        reader.Position.Should().Be(1);
        reader.Unread.ToArray().Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void Complex_SequenceOfOperations()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        SpanReader<byte> reader = new(span);

        // Complex sequence that exercises multiple code paths
        reader.TryRead(2, out ReadOnlySpan<byte> segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1, 2]);

        reader.TryReadTo(5, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([3, 4]);

        reader.AdvancePast(6).Should().Be(1);

        reader.TryAdvancePast([7, 8]).Should().BeTrue();

        reader.Rewind(1);
        reader.Position.Should().Be(7);

        reader.TryPeek(out byte value).Should().BeTrue();
        value.Should().Be(8);

        reader.Position = 9;
        reader.TryRead(out value).Should().BeTrue();
        value.Should().Be(10);

        reader.End.Should().BeTrue();
    }

    [Fact]
    public void EmptySpan_AllOperations()
    {
        ReadOnlySpan<byte> span = [];
        SpanReader<byte> reader = new(span);

        // All operations on empty span should behave correctly
        reader.Length.Should().Be(0);
        reader.Position.Should().Be(0);
        reader.End.Should().BeTrue();
        reader.Unread.Length.Should().Be(0);

        reader.TryRead(out byte _).Should().BeFalse();
        reader.TryRead(0, out ReadOnlySpan<byte> _).Should().BeTrue();
        reader.TryRead(1, out ReadOnlySpan<byte> _).Should().BeFalse();
        reader.TryPeek(out byte _).Should().BeFalse();
        reader.TryReadTo(1, out ReadOnlySpan<byte> _).Should().BeFalse();
        reader.TrySplit(1, out ReadOnlySpan<byte> _).Should().BeFalse();
        reader.AdvancePast(1).Should().Be(0);
        reader.TryAdvancePast([]).Should().BeTrue();
        reader.TryAdvancePast([1]).Should().BeFalse();

        reader.Reset();
        reader.Position.Should().Be(0);
    }
}
