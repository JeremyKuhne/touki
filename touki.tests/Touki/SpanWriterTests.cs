// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Drawing;

namespace Touki;

public class SpanWriterTests
{
    [Fact]
    public void SpanWriter_TryWrite()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite(1).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 0, 0, 0, 0]);

        writer.TryWrite(2).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 0, 0, 0]);

        writer.TryWrite(3).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 3, 0, 0]);

        writer.TryWrite(4).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 0]);

        writer.TryWrite(5).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 5]);

        writer.TryWrite(6).Should().BeFalse();
    }

    [Fact]
    public void SpanWriter_TryWrite_Spans()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite([1, 2]).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 0, 0, 0]);

        writer.TryWrite([3, 4]).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 0]);

        writer.TryWrite([5]).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 5]);

        writer.TryWrite([6]).Should().BeFalse();
    }

    [Fact]
    public void SpanWriter_TryWrite_Count()
    {
        Span<int> span = new int[5];
        SpanWriter<int> writer = new(span);

        writer.TryWrite(2, 1).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 1, 0, 0, 0]);

        writer.TryWrite(2, 2).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 1, 2, 2, 0]);

        writer.TryWrite(1, 3).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 1, 2, 2, 3]);

        writer.TryWrite(1, 4).Should().BeFalse();
    }

    [Fact]
    public void SpanWriter_TryWrite_CountPoints()
    {
        Span<Point> span = new Point[5];
        SpanWriter<Point> writer = new(span);

        writer.TryWrite(2, new Point(1, 2)).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([new Point(1, 2), new Point(1, 2), default, default, default]);

        writer.TryWrite(2, new Point(3, 4)).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([new Point(1, 2), new Point(1, 2), new Point(3, 4), new Point(3, 4), default]);

        writer.TryWrite(1, new Point(5, 6)).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([new Point(1, 2), new Point(1, 2), new Point(3, 4), new Point(3, 4), new Point(5, 6)]);

        writer.TryWrite(1, new Point(7, 8)).Should().BeFalse();
    }

    [Fact]
    public void SpanWriter_Position_Property()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.Position.Should().Be(0);

        writer.TryWrite(1);
        writer.Position.Should().Be(1);

        writer.TryWrite([2, 3]);
        writer.Position.Should().Be(3);

        writer.Position = 1;
        writer.Position.Should().Be(1);
    }

    [Fact]
    public void SpanWriter_Position_Setter_Valid()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span)
        {
            Position = 3
        };

        writer.Position.Should().Be(3);

        writer.Position = 0;
        writer.Position.Should().Be(0);

        writer.Position = 5;
        writer.Position.Should().Be(5);
    }

    [Fact]
    public void SpanWriter_Position_Setter_Negative_ThrowsException()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        try
        {
            writer.Position = -1;
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanWriter_Position_Setter_OutOfBounds_ThrowsException()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        try
        {
            writer.Position = 4;
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanWriter_Length_Property()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.Length.Should().Be(5);

        writer.TryWrite([1, 2]);
        writer.Length.Should().Be(5); // Length should not change with position
    }

    [Fact]
    public void SpanWriter_Span_Property()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.Span.ToArray().Should().BeEquivalentTo([0, 0, 0, 0, 0]);

        writer.TryWrite([1, 2]);
        writer.Span.ToArray().Should().BeEquivalentTo([1, 2, 0, 0, 0]);
    }

    [Fact]
    public void SpanWriter_TryWrite_SingleValue_EmptySpan()
    {
        Span<byte> span = [];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite(1).Should().BeFalse();
        writer.Position.Should().Be(0);
    }

    [Fact]
    public void SpanWriter_TryWrite_Span_EmptyInput()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        ReadOnlySpan<byte> empty = [];
        writer.TryWrite(empty).Should().BeTrue();
        writer.Position.Should().Be(0); // Should not advance for empty input
        span.ToArray().Should().BeEquivalentTo([0, 0, 0]);
    }

    [Fact]
    public void SpanWriter_TryWrite_Span_ExactFit()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite([1, 2, 3]).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([1, 2, 3]);
        writer.Position.Should().Be(3);

        writer.TryWrite([4]).Should().BeFalse(); // No space left
    }

    [Fact]
    public void SpanWriter_TryWrite_Span_TooLarge()
    {
        Span<byte> span = new byte[2];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite([1, 2, 3]).Should().BeFalse();
        span.ToArray().Should().BeEquivalentTo([0, 0]); // Should not be modified
        writer.Position.Should().Be(0);
    }

    [Fact]
    public void SpanWriter_TryWrite_Count_Zero()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite(0, 5).Should().BeTrue();
        writer.Position.Should().Be(0); // Should not advance
        span.ToArray().Should().BeEquivalentTo([0, 0, 0]);
    }

    [Fact]
    public void SpanWriter_TryWrite_Count_Negative_ThrowsException()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        try
        {
            writer.TryWrite(-1, 5);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanWriter_TryWrite_Count_ExactFit()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite(3, 7).Should().BeTrue();
        span.ToArray().Should().BeEquivalentTo([7, 7, 7]);
        writer.Position.Should().Be(3);

        writer.TryWrite(1, 8).Should().BeFalse(); // No space left
    }

    [Fact]
    public void SpanWriter_Advance()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.Advance(2);
        writer.Position.Should().Be(2);

        writer.Advance(2);
        writer.Position.Should().Be(4);

        writer.Advance(1);
        writer.Position.Should().Be(5);

        try
        {
            writer.Advance(1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanWriter_Advance_Zero()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        writer.Advance(0);
        writer.Position.Should().Be(0);
    }

    [Fact]
    public void SpanWriter_Advance_Negative_ThrowsException()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        try
        {
            writer.Advance(-1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanWriter_Rewind()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.Advance(3);
        writer.Position.Should().Be(3);

        writer.Rewind(1);
        writer.Position.Should().Be(2);

        writer.Rewind(2);
        writer.Position.Should().Be(0);

        try
        {
            writer.Rewind(1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanWriter_Rewind_Zero()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);
        writer.Advance(2);

        writer.Rewind(0);
        writer.Position.Should().Be(2);
    }

    [Fact]
    public void SpanWriter_Rewind_Negative_ThrowsException()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span);

        try
        {
            writer.Rewind(-1);
            Assert.Fail($"Expected {nameof(ArgumentOutOfRangeException)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            // Expected
        }
    }

    [Fact]
    public void SpanWriter_Reset()
    {
        Span<byte> span = new byte[5];
        SpanWriter<byte> writer = new(span);

        writer.TryWrite([1, 2, 3]);
        writer.Position.Should().Be(3);

        writer.Reset();
        writer.Position.Should().Be(0);

        // Should be able to write from beginning again
        writer.TryWrite([4, 5]);
        span.ToArray().Should().BeEquivalentTo([4, 5, 3, 0, 0]); // Note: previous data at index 2 remains
    }

    [Fact]
    public void SpanWriter_ComplexScenario()
    {
        Span<byte> span = new byte[10];
        SpanWriter<byte> writer = new(span);

        // Write some individual values
        writer.TryWrite(1).Should().BeTrue();
        writer.TryWrite(2).Should().BeTrue();

        // Write a span
        writer.TryWrite([3, 4, 5]).Should().BeTrue();
        writer.Position.Should().Be(5);

        // Write repeated values
        writer.TryWrite(2, 6).Should().BeTrue();
        writer.Position.Should().Be(7);

        // Rewind and overwrite
        writer.Rewind(3);
        writer.Position.Should().Be(4);
        writer.TryWrite([7, 8]).Should().BeTrue();

        span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 7, 8, 6, 0, 0, 0]);
        writer.Position.Should().Be(6);
    }

    [Fact]
    public void SpanWriter_BoundaryConditions()
    {
        Span<byte> span = new byte[1];
        SpanWriter<byte> writer = new(span);

        // Single element span
        writer.TryWrite(42).Should().BeTrue();
        span[0].Should().Be(42);
        writer.Position.Should().Be(1);

        // No more space
        writer.TryWrite(43).Should().BeFalse();
        writer.Position.Should().Be(1);

        // Reset and try again
        writer.Reset();
        writer.TryWrite(44).Should().BeTrue();
        span[0].Should().Be(44);
    }

    [Fact]
    public void SpanWriter_EdgeCases_EmptySpan()
    {
        Span<byte> span = [];
        SpanWriter<byte> writer = new(span);

        writer.Length.Should().Be(0);
        writer.Position.Should().Be(0);

        writer.TryWrite(1).Should().BeFalse();
        writer.TryWrite([]).Should().BeTrue(); // Empty write should succeed
        writer.TryWrite(0, 1).Should().BeTrue(); // Zero count should succeed
        writer.TryWrite(1, 1).Should().BeFalse(); // Non-zero count should fail

        writer.Position.Should().Be(0);
    }

    [Fact]
    public void SpanWriter_Position_EdgeCases()
    {
        Span<byte> span = new byte[3];
        SpanWriter<byte> writer = new(span)
        {
            // Test setting position to exact length (valid)
            Position = 3
        };

        writer.Position.Should().Be(3);

        // Test that we can't write at end
        writer.TryWrite(1).Should().BeFalse();
    }

    [Fact]
    public void SpanWriter_MixedOperations()
    {
        Span<byte> span = new byte[8];
        SpanWriter<byte> writer = new(span);

        // Write, advance manually, write again
        writer.TryWrite([1, 2]).Should().BeTrue();
        writer.Advance(1); // Skip one position
        writer.TryWrite(5).Should().BeTrue();

        // Rewind and fill gap
        writer.Rewind(2);
        writer.TryWrite(3).Should().BeTrue();

        // Continue from where we left off
        writer.TryWrite([4, 6, 7]).Should().BeTrue();

        span.ToArray().Should().BeEquivalentTo([1, 2, 3, 4, 6, 7, 0, 0]);
        writer.Position.Should().Be(6);
    }
}
