// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class SequenceSetTests
{
    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new SequenceSet<int>(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Count_NewSet_IsZero()
    {
        using SequenceSet<int> set = new();
        set.Count.Should().Be(0);
    }

    [Fact]
    public void Contains_EmptySet_ReturnsFalse()
    {
        using SequenceSet<int> set = new();
        set.Contains([1, 2, 3]).Should().BeFalse();
    }

    [Fact]
    public void Add_NewSequence_ReturnsTrue()
    {
        using SequenceSet<int> set = new();
        set.Add([1, 2, 3]).Should().BeTrue();
        set.Count.Should().Be(1);
    }

    [Fact]
    public void Add_DuplicateSequence_ReturnsFalse()
    {
        using SequenceSet<int> set = new();
        set.Add([1, 2, 3]).Should().BeTrue();
        set.Add([1, 2, 3]).Should().BeFalse();
        set.Count.Should().Be(1);
    }

    [Fact]
    public void Add_DuplicateSequence_ReturnsSameHandle()
    {
        using SequenceSet<int> set = new();
        set.Add([1, 2, 3], out int first).Should().BeTrue();
        set.Add([1, 2, 3], out int second).Should().BeFalse();
        second.Should().Be(first);
    }

    [Fact]
    public void Add_DistinctSequences_ReturnDistinctHandles()
    {
        using SequenceSet<int> set = new();
        set.Add([1, 2, 3], out int first);
        set.Add([4, 5, 6], out int second);
        first.Should().NotBe(second);
        set.Count.Should().Be(2);
    }

    [Fact]
    public void Add_DifferentLengths_AreDistinct()
    {
        using SequenceSet<int> set = new();
        set.Add([1, 2]).Should().BeTrue();
        set.Add([1, 2, 3]).Should().BeTrue();
        set.Add([1]).Should().BeTrue();
        set.Count.Should().Be(3);
    }

    [Fact]
    public void Add_EmptySequence_IsInterned()
    {
        using SequenceSet<int> set = new();
        set.Add([]).Should().BeTrue();
        set.Add([]).Should().BeFalse();
        set.Count.Should().Be(1);
        set.Contains([]).Should().BeTrue();
    }

    [Fact]
    public void Contains_AddedSequence_ReturnsTrue()
    {
        using SequenceSet<int> set = new();
        set.Add([7, 8, 9]);
        set.Contains([7, 8, 9]).Should().BeTrue();
        set.Contains([7, 8]).Should().BeFalse();
        set.Contains([9, 8, 7]).Should().BeFalse();
    }

    [Fact]
    public void Indexer_ResolvesHandleToStoredSequence()
    {
        using SequenceSet<int> set = new();
        set.Add([10, 20, 30], out int handle);

        int[] expected = [10, 20, 30];
        set[handle].SequenceEqual(expected).Should().BeTrue();
    }

    [Fact]
    public void Indexer_OutOfRangeHandle_Throws()
    {
        using SequenceSet<int> set = new();
        set.Add([1]);

        Action act = () =>
        {
            ReadOnlySpan<int> span = set[5];
            _ = span.Length;
        };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_ManySequences_GrowsAndStaysCorrect()
    {
        using SequenceSet<int> set = new(minimumCapacity: 4);

        const int total = 5000;
        for (int i = 0; i < total; i++)
        {
            int[] sequence = [i, i + 1, i + 2];
            set.Add(sequence).Should().BeTrue();
        }

        set.Count.Should().Be(total);

        for (int i = 0; i < total; i++)
        {
            int[] sequence = [i, i + 1, i + 2];
            set.Contains(sequence).Should().BeTrue();
            set.Add(sequence).Should().BeFalse();
        }

        set.Count.Should().Be(total);
    }

    [Fact]
    public void Add_VariableLengthSequences_GrowArenaCorrectly()
    {
        using SequenceSet<int> set = new(minimumCapacity: 2);

        for (int length = 1; length <= 200; length++)
        {
            int[] sequence = new int[length];
            for (int j = 0; j < length; j++)
            {
                sequence[j] = length * 1000 + j;
            }

            set.Add(sequence).Should().BeTrue();
        }

        set.Count.Should().Be(200);

        for (int length = 1; length <= 200; length++)
        {
            int[] sequence = new int[length];
            for (int j = 0; j < length; j++)
            {
                sequence[j] = length * 1000 + j;
            }

            set.Contains(sequence).Should().BeTrue();
        }
    }

    [Fact]
    public void Clear_RemovesAllSequences()
    {
        using SequenceSet<int> set = new();
        set.Add([1, 2]);
        set.Add([3, 4]);

        set.Clear();

        set.Count.Should().Be(0);
        set.Contains([1, 2]).Should().BeFalse();
        set.Add([1, 2]).Should().BeTrue();
        set.Count.Should().Be(1);
    }

    [Fact]
    public void Enumerator_YieldsAllSequencesInInsertionOrder()
    {
        using SequenceSet<int> set = new();
        set.Add([1]);
        set.Add([2, 2]);
        set.Add([3, 3, 3]);

        int observed = 0;
        foreach (ReadOnlySpan<int> sequence in set)
        {
            observed++;
            sequence.Length.Should().Be(observed);
            sequence[0].Should().Be(observed);
        }

        observed.Should().Be(3);
    }

    [Fact]
    public void Add_CharSequences_InternsCorrectly()
    {
        using SequenceSet<char> set = new();
        set.Add("abc".AsSpan()).Should().BeTrue();
        set.Add("abc".AsSpan()).Should().BeFalse();
        set.Add("abd".AsSpan()).Should().BeTrue();
        set.Contains("abc".AsSpan()).Should().BeTrue();
        set.Count.Should().Be(2);
    }

    [Fact]
    public void Add_ByteSequences_InternsCorrectly()
    {
        using SequenceSet<byte> set = new();
        set.Add([1, 2, 3]).Should().BeTrue();
        set.Add([1, 2, 3]).Should().BeFalse();
        set.Add([3, 2, 1]).Should().BeTrue();
        set.Count.Should().Be(2);
    }

    [Fact]
    public void Dispose_BeforeFirstAdd_DoesNotThrow()
    {
        SequenceSet<int> set = new();
        Action act = set.Dispose;
        act.Should().NotThrow();
    }
}
