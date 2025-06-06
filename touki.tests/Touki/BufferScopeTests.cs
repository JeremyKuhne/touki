// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public unsafe class BufferScopeTests
{
    [Fact]
    public void Construct_WithStackAlloc()
    {
        using BufferScope<char> buffer = new(stackalloc char[10]);
        buffer.Length.Should().Be(10);
        buffer[0] = 'Y';
        buffer[..1].ToString().Should().Be("Y");
    }

    [Fact]
    public void Construct_WithStackAlloc_GrowAndCopy()
    {
        using BufferScope<char> buffer = new(stackalloc char[10]);
        buffer.Length.Should().Be(10);
        buffer[0] = 'Y';
        buffer.EnsureCapacity(64, copy: true);
        buffer.Length.Should().BeGreaterThanOrEqualTo(64);
        buffer[..1].ToString().Should().Be("Y");
    }

    [Fact]
    public void Construct_WithStackAlloc_Pin()
    {
        using BufferScope<char> buffer = new(stackalloc char[10]);
        buffer.Length.Should().Be(10);
        buffer[0] = 'Y';
        fixed (char* c = buffer)
        {
            (*c).Should().Be('Y');
            *c = 'Z';
        }

        buffer[..1].ToString().Should().Be("Z");
    }

    [Fact]
    public void Construct_GrowAndCopy()
    {
        using BufferScope<char> buffer = new(32);
        buffer.Length.Should().BeGreaterThanOrEqualTo(32);
        buffer[0] = 'Y';
        buffer.EnsureCapacity(64, copy: true);
        buffer.Length.Should().BeGreaterThanOrEqualTo(64);
        buffer[..1].ToString().Should().Be("Y");
    }

    [Fact]
    public void Construct_Pin()
    {
        using BufferScope<char> buffer = new(64);
        buffer.Length.Should().BeGreaterThanOrEqualTo(64);
        buffer[0] = 'Y';
        fixed (char* c = buffer)
        {
            (*c).Should().Be('Y');
            *c = 'Z';
        }

        buffer[..1].ToString().Should().Be("Z");
    }

    [Fact]
    public void Construct_WithMinimumLength_ZeroLength()
    {
        using BufferScope<int> buffer = new(0);
        buffer.Length.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Construct_WithMinimumLength_SmallLength()
    {
        using BufferScope<byte> buffer = new(5);
        buffer.Length.Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void Construct_WithSpan_EmptySpan()
    {
        using BufferScope<int> buffer = new([]);
        buffer.Length.Should().Be(0);
    }

    [Fact]
    public void Construct_WithSpanAndMinimumLength_SpanLargerThanMinimum()
    {
        using BufferScope<char> buffer = new(stackalloc char[20], 10);
        buffer.Length.Should().Be(20);
        buffer[0] = 'A';
        buffer[19] = 'Z';
        buffer[0].Should().Be('A');
        buffer[19].Should().Be('Z');
    }

    [Fact]
    public void Construct_WithSpanAndMinimumLength_SpanSmallerThanMinimum()
    {
        using BufferScope<char> buffer = new(stackalloc char[5], 20);
        buffer.Length.Should().BeGreaterThanOrEqualTo(20);
    }

    [Fact]
    public void Construct_WithSpanAndMinimumLength_SpanEqualsMinimum()
    {
        using BufferScope<char> buffer = new(stackalloc char[10], 10);
        buffer.Length.Should().Be(10);
    }

    [Fact]
    public void EnsureCapacity_AlreadySufficientCapacity()
    {
        using BufferScope<int> buffer = new(20);
        int originalLength = buffer.Length;
        buffer[0] = 42;

        buffer.EnsureCapacity(10);

        buffer.Length.Should().Be(originalLength);
        buffer[0].Should().Be(42);
    }

    [Fact]
    public void EnsureCapacity_GrowWithoutCopy()
    {
        using BufferScope<int> buffer = new(10);
        buffer[0] = 42;
        buffer[5] = 100;

        buffer.EnsureCapacity(50, copy: false);

        buffer.Length.Should().BeGreaterThanOrEqualTo(50);
        // Data should not be copied when copy is false
    }

    [Fact]
    public void EnsureCapacity_GrowFromStackAllocWithCopy()
    {
        using BufferScope<char> buffer = new(stackalloc char[5]);
        buffer[0] = 'H';
        buffer[1] = 'e';
        buffer[2] = 'l';
        buffer[3] = 'l';
        buffer[4] = 'o';

        buffer.EnsureCapacity(20, copy: true);

        buffer.Length.Should().BeGreaterThanOrEqualTo(20);
        buffer[0].Should().Be('H');
        buffer[1].Should().Be('e');
        buffer[2].Should().Be('l');
        buffer[3].Should().Be('l');
        buffer[4].Should().Be('o');
    }

    [Fact]
    public void EnsureCapacity_MultipleGrows()
    {
        using BufferScope<int> buffer = new(5);
        buffer[0] = 1;
        buffer[1] = 2;

        buffer.EnsureCapacity(10, copy: true);
        buffer[0].Should().Be(1);
        buffer[1].Should().Be(2);

        buffer.EnsureCapacity(50, copy: true);
        buffer.Length.Should().BeGreaterThanOrEqualTo(50);
        buffer[0].Should().Be(1);
        buffer[1].Should().Be(2);
    }

    [Fact]
    public void Indexer_Range_FullRange()
    {
        using BufferScope<char> buffer = new(stackalloc char[5]);
        buffer[0] = 'A';
        buffer[1] = 'B';
        buffer[2] = 'C';
        buffer[3] = 'D';
        buffer[4] = 'E';

        Span<char> slice = buffer[..];
        slice.Length.Should().Be(5);
        slice[0].Should().Be('A');
        slice[4].Should().Be('E');
    }

    [Fact]
    public void Indexer_Range_PartialRange()
    {
        using BufferScope<int> buffer = new(stackalloc int[10]);
        for (int i = 0; i < 10; i++)
        {
            buffer[i] = i;
        }

        Span<int> slice = buffer[2..8];
        slice.Length.Should().Be(6);
        slice[0].Should().Be(2);
        slice[5].Should().Be(7);
    }

    [Fact]
    public void Indexer_Range_EmptyRange()
    {
        using BufferScope<byte> buffer = new(10);
        Span<byte> slice = buffer[5..5];
        slice.Length.Should().Be(0);
    }

    [Fact]
    public void Slice_ValidRange()
    {
        using BufferScope<char> buffer = new(stackalloc char[10]);
        for (int i = 0; i < 10; i++)
        {
            buffer[i] = (char)('A' + i);
        }

        Span<char> slice = buffer.Slice(3, 4);
        slice.Length.Should().Be(4);
        slice[0].Should().Be('D');
        slice[3].Should().Be('G');
    }

    [Fact]
    public void Slice_ZeroLength()
    {
        using BufferScope<int> buffer = new(10);
        Span<int> slice = buffer.Slice(5, 0);
        slice.Length.Should().Be(0);
    }

    [Fact]
    public void AsSpan_ReturnsCorrectSpan()
    {
        using BufferScope<double> buffer = new(5);
        buffer[0] = 3.14;
        buffer[1] = 2.71;

        Span<double> span = buffer.AsSpan();
        span.Length.Should().Be(buffer.Length);
        span[0].Should().Be(3.14);
        span[1].Should().Be(2.71);
    }

    [Fact]
    public void ImplicitOperator_ToSpan()
    {
        using BufferScope<int> buffer = new(stackalloc int[3]);
        buffer[0] = 10;
        buffer[1] = 20;
        buffer[2] = 30;

        Span<int> span = buffer;
        span.Length.Should().Be(3);
        span[0].Should().Be(10);
        span[1].Should().Be(20);
        span[2].Should().Be(30);
    }

    [Fact]
    public void ImplicitOperator_ToReadOnlySpan()
    {
        using BufferScope<char> buffer = new(stackalloc char[3]);
        buffer[0] = 'X';
        buffer[1] = 'Y';
        buffer[2] = 'Z';

        ReadOnlySpan<char> readOnlySpan = buffer;
        readOnlySpan.Length.Should().Be(3);
        readOnlySpan[0].Should().Be('X');
        readOnlySpan[1].Should().Be('Y');
        readOnlySpan[2].Should().Be('Z');
    }

    [Fact]
    public void GetEnumerator_IteratesCorrectly()
    {
        using BufferScope<int> buffer = new(stackalloc int[5]);
        for (int i = 0; i < 5; i++)
        {
            buffer[i] = i * 10;
        }

        var values = new List<int>();
        foreach (int value in buffer)
        {
            values.Add(value);
        }

        values.Should().Equal(0, 10, 20, 30, 40);
    }

    [Fact]
    public void GetEnumerator_EmptyBuffer()
    {
        using BufferScope<string> buffer = new([]);

        var values = new List<string>();
        foreach (string value in buffer)
        {
            values.Add(value);
        }

        values.Should().BeEmpty();
    }

    [Fact]
    public void ToString_WithCharBuffer()
    {
        using BufferScope<char> buffer = new(stackalloc char[5]);
        buffer[0] = 'H';
        buffer[1] = 'e';
        buffer[2] = 'l';
        buffer[3] = 'l';
        buffer[4] = 'o';

        string result = buffer.ToString();
        result.Should().Be("Hello");
    }

    [Fact]
    public void ToString_EmptyBuffer()
    {
        using BufferScope<char> buffer = new([]);
        string result = buffer.ToString();
        result.Should().Be("");
    }

    [Fact]
    public void GetPinnableReference_WithStackAlloc()
    {
        using BufferScope<byte> buffer = new(stackalloc byte[10]);
        buffer[0] = 255;
        buffer[9] = 128;

        ref byte reference = ref buffer.GetPinnableReference();
        reference.Should().Be(255);

        // Modify through reference
        reference = 100;
        buffer[0].Should().Be(100);
    }

    [Fact]
    public void GetPinnableReference_EmptyBuffer()
    {
        using BufferScope<int> buffer = new([]);
        ref int reference = ref buffer.GetPinnableReference();
        // Should not throw, but reference may be to null location
    }

    [Fact]
    public void Dispose_MultipleCallsSafe()
    {
        BufferScope<int> buffer = new(100);
        buffer[0] = 42;

        buffer.Dispose();
        buffer.Dispose(); // Should not throw
    }

    [Fact]
    public void Dispose_StackAllocBuffer()
    {
        BufferScope<int> buffer = new(stackalloc int[10]);
        buffer[0] = 42;

        buffer.Dispose(); // Should not throw for stack allocated buffer
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void Construct_WithMinimumLength_VariousSizes(int minimumLength)
    {
        using BufferScope<long> buffer = new(minimumLength);
        buffer.Length.Should().BeGreaterThanOrEqualTo(minimumLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void EnsureCapacity_BoundaryConditions(int capacity)
    {
        using BufferScope<short> buffer = new(5);
        buffer.EnsureCapacity(capacity);
        buffer.Length.Should().BeGreaterThanOrEqualTo(Math.Max(5, capacity));
    }

    [Fact]
    public void WorksWithDifferentTypes_String()
    {
        using BufferScope<string> buffer = new(5);
        buffer[0] = "Hello";
        buffer[1] = "World";
        buffer[0].Should().Be("Hello");
        buffer[1].Should().Be("World");
    }

    [Fact]
    public void WorksWithDifferentTypes_CustomStruct()
    {
        using BufferScope<DateTime> buffer = new(3);
        var date1 = new DateTime(2025, 1, 1);
        var date2 = new DateTime(2025, 12, 31);

        buffer[0] = date1;
        buffer[1] = date2;

        buffer[0].Should().Be(date1);
        buffer[1].Should().Be(date2);
    }

    [Fact]
    public void CombinedOperations_ComplexScenario()
    {
        using BufferScope<int> buffer = new(stackalloc int[5], 10);

        // Initial setup
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = i + 1;
        }

        // Grow the buffer
        buffer.EnsureCapacity(20, copy: true);

        // Verify data was copied
        for (int i = 0; i < Math.Min(5, buffer.Length); i++)
        {
            buffer[i].Should().Be(i + 1);
        }

        // Test slicing
        Span<int> slice = buffer[1..4];
        slice.Length.Should().Be(3);
        slice[0].Should().Be(2);
        slice[2].Should().Be(4);

        // Test enumeration
        var firstFive = buffer.AsSpan()[..5];
        var values = new List<int>();
        foreach (int value in firstFive)
        {
            values.Add(value);
        }

        values.Should().Equal(1, 2, 3, 4, 5);
    }
}
