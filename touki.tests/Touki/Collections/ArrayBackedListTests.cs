// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class ArrayBackedListTests
{
    /// <summary>
    ///  Concrete implementation of ArrayBackedList for testing
    /// </summary>
    private class TestArrayBackedList<T> : ArrayBackedList<T> where T : notnull
    {
        public TestArrayBackedList() : base([]) { }

        public TestArrayBackedList(T[] backingArray) : base(backingArray) { }

        protected override T[] GetNewArray(int miminumCapacity) => new T[miminumCapacity];

        protected override void ReturnArray(T[] array) { /* No-op for testing */ }
    }

    [Fact]
    public void Constructor_WithEmptyArray_InitializesCorrectly()
    {
        using TestArrayBackedList<int> list = new();

        list.Should().BeEmpty();
        list.Empty.Should().BeTrue();
        list.Count.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithInitialItems_InitializesCorrectly()
    {
        using TestArrayBackedList<int> list = new([1, 2, 3]);

        list.Count.Should().Be(0); // Initial array doesn't add items
        list.Empty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithNullArray_ThrowsArgumentNullException()
    {
        Action act = () => new TestArrayBackedList<int>(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_SingleItem_IncrementsCount()
    {
        using TestArrayBackedList<int> list = new()
        {
            42
        };

        list.Should().ContainSingle();
        list[0].Should().Be(42);
        list.Empty.Should().BeFalse();
    }

    [Fact]
    public void Add_NullItem_ThrowsArgumentNullException()
    {
        using TestArrayBackedList<string> list = new();
        Action act = () => list.Add(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Add_MultipleItems_IncrementsCount()
    {
        using TestArrayBackedList<int> list = new();
        for (int i = 0; i < 100; i++)
        {
            list.Add(i);
        }

        list.Count.Should().Be(100);
        for (int i = 0; i < 100; i++)
        {
            list[i].Should().Be(i);
        }
    }

    [Fact]
    public void Indexer_GetWithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using TestArrayBackedList<int> list = new()
        {
            42
        };

        Action actNegative = () => _ = list[-1];
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => _ = list[1];
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_SetWithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using TestArrayBackedList<int> list = new()
        {
            42
        };

        Action actNegative = () => list[-1] = 10;
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list[1] = 10;
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Insert_AtValidPositions_ShiftsItems()
    {
        using TestArrayBackedList<int> list = new()
        {
            3
        };
        list.Insert(0, 1);
        list.Insert(1, 2);

        list.Count.Should().Be(3);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        list[2].Should().Be(3);
    }

    [Fact]
    public void Insert_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using TestArrayBackedList<int> list = new()
        {
            1
        };

        Action actNegative = () => list.Insert(-1, 0);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list.Insert(2, 0);
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Insert_NullItem_ThrowsArgumentNullException()
    {
        using TestArrayBackedList<string> list = new()
        {
            "test"
        };

        Action act = () => list.Insert(0, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveAt_ValidIndex_RemovesItem()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            2,
            3
        };

        list.RemoveAt(1);

        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }

    [Fact]
    public void RemoveAt_InvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using TestArrayBackedList<int> list = new()
        {
            1
        };

        Action actNegative = () => list.RemoveAt(-1);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list.RemoveAt(1);
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Remove_ExistingItem_RemovesAndReturnsTrue()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            2,
            3
        };

        bool result = list.Remove(2);

        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }

    [Fact]
    public void Remove_NonExistingItem_ReturnsFalse()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            3
        };

        bool result = list.Remove(2);

        result.Should().BeFalse();
        list.Count.Should().Be(2);
    }

    [Fact]
    public void Contains_ExistingItem_ReturnsTrue()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            2,
            3
        };

        list.Should().Contain(2);
        list.Contains(2).Should().BeTrue();
    }

    [Fact]
    public void Contains_NonExistingItem_ReturnsFalse()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            3
        };

        list.Should().NotContain(2);
        list.Contains(2).Should().BeFalse();
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            2,
            3
        };

        list.Clear();

        list.Should().BeEmpty();
        list.Empty.Should().BeTrue();
        list.Count.Should().Be(0);
    }

    [Fact]
    public void EnsureCapacity_GrowsCapacity()
    {
        using TestArrayBackedList<int> list = new();
        for (int i = 0; i < 5; i++)
        {
            list.Add(i);
        }

        int newCapacity = list.EnsureCapacity(20);
        newCapacity.Should().BeGreaterThanOrEqualTo(20);

        // Verify we can add more items
        for (int i = 5; i < 15; i++)
        {
            list.Add(i);
        }

        list.Count.Should().Be(15);
    }

    [Fact]
    public void EnsureCapacity_NegativeOrZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        using TestArrayBackedList<int> list = new();

        Action actNegative = () => list.EnsureCapacity(-1);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actZero = () => list.EnsureCapacity(0);
        actZero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CopyTo_CopiesAllElements()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            2,
            3
        };

        int[] array = new int[5];
        list.CopyTo(array, 1);

        array[0].Should().Be(0);
        array[1].Should().Be(1);
        array[2].Should().Be(2);
        array[3].Should().Be(3);
        array[4].Should().Be(0);
    }

    [Fact]
    public void CopyTo_WithRangeParameters_CopiesCorrectElements()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            2,
            3,
            4
        };

        int[] array = new int[3];
        list.CopyTo(1, array, 0, 2);

        array[0].Should().Be(2);
        array[1].Should().Be(3);
        array[2].Should().Be(0);
    }

    [Fact]
    public void Enumeration_WorksCorrectly()
    {
        using TestArrayBackedList<int> list = new()
        {
            1,
            2,
            3
        };

        int index = 0;
        foreach (int item in list)
        {
            item.Should().Be(index + 1);
            index++;
        }

        index.Should().Be(3);
    }

    [Fact]
    public void Dispose_ClearsArrayAndCount()
    {
        TestArrayBackedList<int> list = new()
        {
            1,
            2
        };

        list.Dispose();

        list.Count.Should().Be(0);
        list.Empty.Should().BeTrue();
    }
}
