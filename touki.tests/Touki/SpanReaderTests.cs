// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Drawing;

namespace Touki;

public class SpanReaderTests
{
    [Fact]
    public void SpanReader_TryReadTo_SkipDelimiter()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.TryReadTo(3, out var read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1, 2]);
        reader.Position.Should().Be(3);

        reader.TryReadTo(5, out read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([4]);
        reader.Position.Should().Be(5);
    }

    [Fact]
    public void SpanReader_TryReadTo_DoNotSkipDelimiter()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.TryReadTo(3, advancePastDelimiter: false, out var read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1, 2]);
        reader.Position.Should().Be(2);

        reader.TryReadTo(5, advancePastDelimiter: false, out read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([3, 4]);
        reader.Position.Should().Be(4);

        reader.TryReadTo(5, advancePastDelimiter: false, out read).Should().BeTrue();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(4);
    }

    [Fact]
    public void SpanReader_TryReadTo_DelimiterAtStart_SkipDelimiter()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        reader.TryReadTo(1, out var read).Should().BeTrue();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(1);

        reader.Reset();
        reader.TryReadTo(1, advancePastDelimiter: false, out read).Should().BeTrue();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_TryReadTo_DelimiterAtStart_DoNotSkipDelimiter()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        reader.TryReadTo(1, out var read).Should().BeTrue();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(1);

        reader.Reset();
        reader.TryReadTo(1, advancePastDelimiter: true, out read).Should().BeTrue();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(1);
    }

    [Fact]
    public void SpanReader_TryReadTo_DelimiterNotFound()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.TryReadTo(99, out var read).Should().BeFalse();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(0); // Position should not change when delimiter not found
    }

    [Fact]
    public void SpanReader_TryReadTo_EmptySpan()
    {
        ReadOnlySpan<byte> span = [];
        SpanReader<byte> reader = new(span);

        reader.TryReadTo(1, out var read).Should().BeFalse();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_Advance()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.Advance(2);
        reader.Position.Should().Be(2);

        reader.Advance(2);
        reader.Position.Should().Be(4);

        reader.Advance(1);
        reader.Position.Should().Be(5);

        try
        {
            reader.Advance(1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanReader_Advance_Zero()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        reader.Advance(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_Advance_Negative_ThrowsException()
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
    public void SpanReader_Rewind()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.Advance(2);
        reader.Position.Should().Be(2);

        reader.Rewind(1);
        reader.Position.Should().Be(1);

        try
        {
            reader.Rewind(2);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }

        reader.Rewind(1);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_Rewind_Zero()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);
        reader.Advance(2);

        reader.Rewind(0);
        reader.Position.Should().Be(2);
    }

    [Fact]
    public void SpanReader_Rewind_Negative_ThrowsException()
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
    public void SpanReader_Position_Setter_Valid()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span)
        {
            Position = 3
        };

        reader.Position.Should().Be(3);

        reader.Position = 0;
        reader.Position.Should().Be(0);

        reader.Position = 5;
        reader.Position.Should().Be(5);
    }

    [Fact]
    public void SpanReader_Position_Setter_Negative_ThrowsException()
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
    public void SpanReader_Position_Setter_OutOfBounds_ThrowsException()
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
    public void SpanReader_Reset()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.Advance(3);
        reader.Position.Should().Be(3);

        reader.Reset();
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_TryRead_SingleValue()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        reader.TryRead(out byte value).Should().BeTrue();
        value.Should().Be(1);
        reader.Position.Should().Be(1);

        reader.TryRead(out value).Should().BeTrue();
        value.Should().Be(2);
        reader.Position.Should().Be(2);

        reader.TryRead(out value).Should().BeTrue();
        value.Should().Be(3);
        reader.Position.Should().Be(3);

        reader.TryRead(out value).Should().BeFalse();
        value.Should().Be(default);
        reader.Position.Should().Be(3);
    }

    [Fact]
    public void SpanReader_TryRead_SingleValue_EmptySpan()
    {
        ReadOnlySpan<byte> span = [];
        SpanReader<byte> reader = new(span);

        reader.TryRead(out byte value).Should().BeFalse();
        value.Should().Be(default);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_TryRead_ReadPoints()
    {
        ReadOnlySpan<uint> span = [1, 2, 3, 4, 5];
        SpanReader<uint> reader = new(span);

        reader.TryRead(out Point value).Should().BeTrue();
        value.Should().Be(new Point(1, 2));

        reader.TryRead(out value).Should().BeTrue();
        value.Should().Be(new Point(3, 4));

        reader.TryRead(out value).Should().BeFalse();
        value.Should().Be(default(Point));
        reader.Position.Should().Be(4);
    }

    [Fact]
    public void SpanReader_TryRead_ReadPointCounts()
    {
        ReadOnlySpan<uint> span = [1, 2, 3, 4, 5];
        SpanReader<uint> reader = new(span);

        reader.TryRead(2, out ReadOnlySpan<Point> value).Should().BeTrue();
        value.ToArray().Should().BeEquivalentTo([new Point(1, 2), new Point(3, 4)]);

        // This fails to compile as the span is read only, as expected.
        // value[0].X = 0;

        reader.TryRead(2, out value).Should().BeFalse();
        value.ToArray().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public void SpanReader_TryRead_ReadPointFCounts_NotEnoughBuffer(int bufferSize, int readCount)
    {
        ReadOnlySpan<float> span = new float[bufferSize];
        SpanReader<float> reader = new(span);

        reader.TryRead<PointF>(readCount, out _).Should().BeFalse();
    }

    [Fact]
    public void SpanReader_TryRead_Count()
    {
        ReadOnlySpan<uint> span = [1, 2, 3, 4, 5];
        SpanReader<uint> reader = new(span);

        reader.TryRead(2, out var read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1, 2]);
        reader.Position.Should().Be(2);

        reader.TryRead(2, out read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([3, 4]);
        reader.Position.Should().Be(4);

        reader.TryRead(2, out _).Should().BeFalse();
        reader.Position.Should().Be(4);
    }

    [Fact]
    public void SpanReader_TryRead_Count_Zero()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        reader.TryRead(0, out var read).Should().BeTrue();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_TryRead_Count_Negative_ThrowsException()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        try
        {
            reader.TryRead(-1, out _);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanReader_TryRead_Struct_ValidSizes()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4];
        SpanReader<byte> reader = new(span);

        // Try to read a struct that's evenly divisible by byte size
        reader.TryRead<ushort>(out _).Should().BeTrue(); // ushort is 2 bytes, should work

        reader.Reset();
        reader.TryRead<uint>(out _).Should().BeTrue(); // uint is 4 bytes, should work
    }

    [Fact]
    public void SpanReader_TryRead_Struct_Count_InvalidSize_ThrowsException()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4];
        SpanReader<byte> reader = new(span);

        // This should work - reading 2 ushorts from 4 bytes
        reader.TryRead<ushort>(2, out _).Should().BeTrue();
    }

    [Fact]
    public void SpanReader_AdvancePast_SingleValue()
    {
        ReadOnlySpan<byte> span = [1, 1, 1, 2, 3];
        SpanReader<byte> reader = new(span);

        int advanced = reader.AdvancePast(1);
        advanced.Should().Be(3);
        reader.Position.Should().Be(3);

        // Should not advance past different value
        advanced = reader.AdvancePast(1);
        advanced.Should().Be(0);
        reader.Position.Should().Be(3);
    }

    [Fact]
    public void SpanReader_AdvancePast_AllSameValue()
    {
        ReadOnlySpan<byte> span = [1, 1, 1, 1];
        SpanReader<byte> reader = new(span);

        int advanced = reader.AdvancePast(1);
        advanced.Should().Be(4);
        reader.Position.Should().Be(4);
    }

    [Fact]
    public void SpanReader_AdvancePast_NoMatch()
    {
        ReadOnlySpan<byte> span = [2, 3, 4];
        SpanReader<byte> reader = new(span);

        int advanced = reader.AdvancePast(1);
        advanced.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_AdvancePast_EmptySpan()
    {
        ReadOnlySpan<byte> span = [];
        SpanReader<byte> reader = new(span);

        int advanced = reader.AdvancePast(1);
        advanced.Should().Be(0);
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_TryAdvancePast_Success()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        ReadOnlySpan<byte> pattern = [1, 2];
        reader.TryAdvancePast(pattern).Should().BeTrue();
        reader.Position.Should().Be(2);

        pattern = [3, 4];
        reader.TryAdvancePast(pattern).Should().BeTrue();
        reader.Position.Should().Be(4);
    }

    [Fact]
    public void SpanReader_TryAdvancePast_Failure()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        ReadOnlySpan<byte> pattern = [1, 3]; // Not consecutive
        reader.TryAdvancePast(pattern).Should().BeFalse();
        reader.Position.Should().Be(0);

        pattern = [2, 3]; // Not at current position
        reader.TryAdvancePast(pattern).Should().BeFalse();
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_TryAdvancePast_EmptyPattern()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        ReadOnlySpan<byte> pattern = [];
        reader.TryAdvancePast(pattern).Should().BeTrue();
        reader.Position.Should().Be(0); // Should not advance for empty pattern
    }

    [Fact]
    public void SpanReader_TryAdvancePast_PatternLargerThanRemaining()
    {
        ReadOnlySpan<byte> span = [1, 2];
        SpanReader<byte> reader = new(span);

        ReadOnlySpan<byte> pattern = [1, 2, 3];
        reader.TryAdvancePast(pattern).Should().BeFalse();
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_Length_Property()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.Length.Should().Be(5);

        reader.Advance(2);
        reader.Length.Should().Be(5); // Length should not change with position
    }

    [Fact]
    public void SpanReader_Span_Property()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.Span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 5]);

        reader.Advance(2);
        reader.Span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 5]); // Span should not change with position
    }

    [Fact]
    public void SpanReader_ComplexScenario()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 2, 4, 5, 2, 6];
        SpanReader<byte> reader = new(span);

        // Read until first delimiter
        reader.TryReadTo(2, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1]);
        reader.Position.Should().Be(2);

        // Skip some values
        reader.Advance(1);
        reader.Position.Should().Be(3);

        // At position 3, we're at a '2', so TryReadTo(2) should find delimiter at start
        reader.TryReadTo(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEmpty(); // Empty because delimiter is at start
        reader.Position.Should().Be(4);

        // Read until next delimiter
        reader.TryReadTo(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([4, 5]);
        reader.Position.Should().Be(7);

        // Read remaining
        reader.TryRead(out byte value).Should().BeTrue();
        value.Should().Be(6);
        reader.Position.Should().Be(8);

        // Should be at end
        reader.TryRead(out _).Should().BeFalse();
    }

    [Fact]
    public void SpanReader_TryReadTo_BoundaryConditions()
    {
        // Test when delimiter is at the very end
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        reader.TryReadTo(3, out var read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1, 2]);
        reader.Position.Should().Be(3);

        // Test when already at end
        reader.TryReadTo(1, out read).Should().BeFalse();
        read.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(3);
    }

    [Fact]
    public void SpanReader_TryRead_Struct_InvalidSize_ThrowsException()
    {
        ReadOnlySpan<ushort> span = [1, 2]; // 4 bytes total
        SpanReader<ushort> reader = new(span);

        // Try to read a byte (1 byte) from ushort span (2 bytes per element) - should throw
        try
        {
            reader.TryRead<byte>(out _);
            Assert.Fail($"Expected {nameof(ArgumentException)}");
        }
        catch (ArgumentException ex)
        {
            ex.Message.Should().Contain("evenly divisible");
        }
    }

    [Fact]
    public void SpanReader_TryRead_Struct_Count_InvalidSize_ThrowsException_New()
    {
        ReadOnlySpan<ushort> span = [1, 2]; // 4 bytes total
        SpanReader<ushort> reader = new(span);

        // Try to read bytes from ushort span - should throw
        try
        {
            reader.TryRead<byte>(1, out _);
            Assert.Fail($"Expected {nameof(ArgumentException)}");
        }
        catch (ArgumentException ex)
        {
            ex.Message.Should().Contain("evenly divisible");
        }
    }

    [Fact]
    public void SpanReader_Position_Setter_EdgeCases()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span)
        {
            // Test setting position to exact length (valid)
            Position = 3
        };

        reader.Position.Should().Be(3);

        // Test that we can still read nothing at end
        reader.TryRead(out byte _).Should().BeFalse();
    }

    [Fact]
    public void SpanReader_Rewind_EdgeCases()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span)
        {
            // Advance to end
            Position = 5
        };

        // Rewind to beginning
        reader.Rewind(5);
        reader.Position.Should().Be(0);

        // Try to rewind past beginning - should throw
        try
        {
            reader.Rewind(1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanReader_TryRead_Count_ExactMatch()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        // Read exactly all remaining
        reader.TryRead(3, out var read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1, 2, 3]);
        reader.Position.Should().Be(3);

        // Try to read more - should fail
        reader.TryRead(1, out read).Should().BeFalse();
        read.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void SpanReader_TryReadTo_ConsecutiveDelimiters()
    {
        ReadOnlySpan<byte> span = [1, 2, 2, 3];
        SpanReader<byte> reader = new(span);

        // Read to first delimiter
        reader.TryReadTo(2, out var read).Should().BeTrue();
        read.ToArray().Should().BeEquivalentTo([1]);
        reader.Position.Should().Be(2);

        // Read to next delimiter (immediately following)
        reader.TryReadTo(2, out read).Should().BeTrue();
        read.ToArray().Should().BeEmpty(); // Empty because delimiter is at start
        reader.Position.Should().Be(3);

        // Read remaining
        reader.TryRead(out byte value).Should().BeTrue();
        value.Should().Be(3);
    }

    [Fact]
    public void SpanReader_AdvancePast_PartialMatch()
    {
        ReadOnlySpan<byte> span = [1, 1, 2, 1, 1, 1];
        SpanReader<byte> reader = new(span);

        // Should advance past first two 1s
        int advanced = reader.AdvancePast(1);
        advanced.Should().Be(2);
        reader.Position.Should().Be(2);

        // Should not advance past the 2
        advanced = reader.AdvancePast(1);
        advanced.Should().Be(0);
        reader.Position.Should().Be(2);

        // Skip the 2
        reader.Advance(1);

        // Should advance past remaining 1s
        advanced = reader.AdvancePast(1);
        advanced.Should().Be(3);
        reader.Position.Should().Be(6);
    }

    [Fact]
    public void SpanReader_TryAdvancePast_ExactMatch()
    {
        ReadOnlySpan<byte> span = [1, 2, 3];
        SpanReader<byte> reader = new(span);

        // Pattern matches entire remaining span
        ReadOnlySpan<byte> pattern = [1, 2, 3];
        reader.TryAdvancePast(pattern).Should().BeTrue();
        reader.Position.Should().Be(3);

        // Should be at end
        reader.TryRead(out byte _).Should().BeFalse();
    }

    [Fact]
    public void SpanReader_Mixed_Operations()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5, 6, 7, 8];
        SpanReader<byte> reader = new(span);

        // Read some values
        reader.TryRead(2, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1, 2]);

        // Advance past some values
        int advanced = reader.AdvancePast(3);
        advanced.Should().Be(1);

        // Try to advance past pattern
        ReadOnlySpan<byte> pattern = [4, 5];
        reader.TryAdvancePast(pattern).Should().BeTrue();

        // Read to delimiter
        reader.TryReadTo(7, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([6]);

        // Read remaining
        reader.TryRead(out byte value).Should().BeTrue();
        value.Should().Be(8);

        reader.Position.Should().Be(8);
    }

    [Fact]
    public void SpanReader_TrySplit_EmptySpan()
    {
        ReadOnlySpan<byte> span = [];
        SpanReader<byte> reader = new(span);

        reader.TrySplit(1, out var segment).Should().BeFalse();
        segment.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(0);
    }

    [Fact]
    public void SpanReader_TrySplit_NoDelimiterFound()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.TrySplit(99, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 5]);
        reader.Position.Should().Be(5); // Should be at end
    }

    [Fact]
    public void SpanReader_TrySplit_DelimiterFound()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.TrySplit(3, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1, 2]);
        reader.Position.Should().Be(3); // Should be past the delimiter
    }

    [Fact]
    public void SpanReader_TrySplit_DelimiterAtStart()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.TrySplit(1, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEmpty(); // No data before delimiter
        reader.Position.Should().Be(1); // Should be past the delimiter
    }

    [Fact]
    public void SpanReader_TrySplit_DelimiterAtEnd()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5];
        SpanReader<byte> reader = new(span);

        reader.TrySplit(5, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1, 2, 3, 4]);
        reader.Position.Should().Be(5); // Should be at end
    }

    [Fact]
    public void SpanReader_TrySplit_MultipleDelimiters()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 2, 4, 5, 2, 6];
        SpanReader<byte> reader = new(span);

        // First split
        reader.TrySplit(2, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1]);
        reader.Position.Should().Be(2);

        // Second split
        reader.TrySplit(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([3]);
        reader.Position.Should().Be(4);

        // Third split
        reader.TrySplit(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([4, 5]);
        reader.Position.Should().Be(7);

        // Final split (no more delimiters)
        reader.TrySplit(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([6]);
        reader.Position.Should().Be(8);

        // Should be empty now
        reader.TrySplit(2, out segment).Should().BeFalse();
        segment.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void SpanReader_TrySplit_SingleElement()
    {
        ReadOnlySpan<byte> span = [42];
        SpanReader<byte> reader = new(span);

        // Split with different delimiter
        reader.TrySplit(99, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([42]);
        reader.Position.Should().Be(1);

        // Should be empty now
        reader.TrySplit(99, out segment).Should().BeFalse();
        segment.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void SpanReader_TrySplit_SingleElementAsDelimiter()
    {
        ReadOnlySpan<byte> span = [42];
        SpanReader<byte> reader = new(span);

        // Split with same element as delimiter
        reader.TrySplit(42, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEmpty(); // No data before delimiter
        reader.Position.Should().Be(1);

        // Should be empty now
        reader.TrySplit(42, out segment).Should().BeFalse();
        segment.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void SpanReader_TrySplit_AfterOtherOperations()
    {
        ReadOnlySpan<byte> span = [1, 2, 3, 4, 5, 6, 7, 8];
        SpanReader<byte> reader = new(span);

        // Advance past some data
        reader.Advance(2);
        reader.Position.Should().Be(2);

        // Now split from current position
        reader.TrySplit(5, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([3, 4]);
        reader.Position.Should().Be(5);

        // Continue splitting
        reader.TrySplit(99, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([6, 7, 8]);
        reader.Position.Should().Be(8);
    }

    [Fact]
    public void SpanReader_TrySplit_ConsecutiveDelimiters()
    {
        ReadOnlySpan<byte> span = [1, 2, 2, 2, 3];
        SpanReader<byte> reader = new(span);

        // First split
        reader.TrySplit(2, out var segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([1]);
        reader.Position.Should().Be(2);

        // Second split (delimiter at start)
        reader.TrySplit(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(3);

        // Third split (delimiter at start again)
        reader.TrySplit(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEmpty();
        reader.Position.Should().Be(4);

        // Final split
        reader.TrySplit(2, out segment).Should().BeTrue();
        segment.ToArray().Should().BeEquivalentTo([3]);
        reader.Position.Should().Be(5);
    }

    [Fact]
    public void SpanReader_TrySplit_CsvLikeUsage()
    {
        // Simulate CSV parsing: "field1,field2,,field4"
        ReadOnlySpan<char> csvData = "field1,field2,,field4".AsSpan();
        SpanReader<char> reader = new(csvData);
        List<string> fields = [];

        // Parse each field using TrySplit
        while (reader.TrySplit(',', out var fieldSpan))
        {
            fields.Add(fieldSpan.ToString());
        }

        fields.Count.Should().Be(4);
        fields[0].Should().Be("field1");
        fields[1].Should().Be("field2");
        fields[2].Should().Be(""); // Empty field
        fields[3].Should().Be("field4");
    }
}
