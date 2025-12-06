// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class ArrayPoolListTests
{
    [Fact]
    public void Constructor_DefaultCapacity_InitializesCorrectly()
    {
        using ArrayPoolList<int> list = [];

        list.Should().BeEmpty();
        list.Empty.Should().BeTrue();
        list.IsReadOnly.Should().BeFalse();
    }

    [Fact]
    public void Constructor_CustomCapacity_InitializesCorrectly()
    {
        using ArrayPoolList<int> list = new(minimumCapacity: 100);

        list.Should().BeEmpty();
        list.Empty.Should().BeTrue();
    }

    [Fact]
    public void Constructor_NegativeCapacity_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => new ArrayPoolList<int>(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Add_SingleItem_IncrementsCount()
    {
        using ArrayPoolList<int> list = [42];

        list.Should().ContainSingle();
        list[0].Should().Be(42);
        list.Empty.Should().BeFalse();
    }

    [Fact]
    public void Add_MultipleItems_IncrementsCount()
    {
        using ArrayPoolList<int> list = [];
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
        using ArrayPoolList<int> list = [42];

        Action actNegative = () => _ = list[-1];
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => _ = list[1];
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_SetWithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using ArrayPoolList<int> list = [42];

        Action actNegative = () => list[-1] = 10;
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list[1] = 10;
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_SetWithValidIndex_UpdatesItem()
    {
        using ArrayPoolList<int> list = [42];
        list[0] = 100;

        list[0].Should().Be(100);
    }

    [Fact]
    public void Insert_AtBeginning_ShiftsItems()
    {
        using ArrayPoolList<int> list = [2, 3];
        list.Insert(0, 1);

        list.Count.Should().Be(3);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        list[2].Should().Be(3);
    }

    [Fact]
    public void Insert_AtMiddle_ShiftsItems()
    {
        using ArrayPoolList<int> list = [1, 3];
        list.Insert(1, 2);

        list.Count.Should().Be(3);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        list[2].Should().Be(3);
    }

    [Fact]
    public void Insert_AtEnd_AppendsList()
    {
        using ArrayPoolList<int> list = [1, 2];
        list.Insert(2, 3);

        list.Count.Should().Be(3);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        list[2].Should().Be(3);
    }

    [Fact]
    public void Insert_WithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using ArrayPoolList<int> list = [1];

        Action actNegative = () => list.Insert(-1, 0);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list.Insert(2, 0);
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RemoveAt_ValidIndex_RemovesItem()
    {
        using ArrayPoolList<int> list = [1, 2, 3];

        list.RemoveAt(1);

        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }

    [Fact]
    public void RemoveAt_InvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using ArrayPoolList<int> list = [1];

        Action actNegative = () => list.RemoveAt(-1);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list.RemoveAt(1);
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Remove_ExistingItem_RemovesAndReturnsTrue()
    {
        using ArrayPoolList<int> list = [1, 2, 3];

        bool result = list.Remove(2);

        result.Should().BeTrue();
        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }

    [Fact]
    public void Remove_NonExistingItem_ReturnsFalse()
    {
        using ArrayPoolList<int> list = [1, 3];

        bool result = list.Remove(2);

        result.Should().BeFalse();
        list.Count.Should().Be(2);
    }

    [Fact]
    public void Contains_ExistingItem_ReturnsTrue()
    {
        using ArrayPoolList<int> list = [1, 2, 3];
        list.Should().Contain(2);
    }

    [Fact]
    public void Contains_NonExistingItem_ReturnsFalse()
    {
        using ArrayPoolList<int> list = [1, 3];
        list.Should().NotContain(2);
    }

    [Fact]
    public void IndexOf_ExistingItem_ReturnsCorrectIndex()
    {
        using ArrayPoolList<int> list = [1, 2, 3];
        list.IndexOf(2).Should().Be(1);
    }

    [Fact]
    public void IndexOf_NonExistingItem_ReturnsNegativeOne()
    {
        using ArrayPoolList<int> list = [1, 3];
        list.IndexOf(2).Should().Be(-1);
    }

    [Fact]
    public void Clear_RemovesAllItems()
    {
        using ArrayPoolList<int> list = [1, 2, 3];

        list.Clear();

        list.Should().BeEmpty();
        list.Empty.Should().BeTrue();
    }

    [Fact]
    public void CopyTo_CopiesAllElements()
    {
        using ArrayPoolList<int> list = [1, 2, 3];

        int[] array = new int[5];
        list.CopyTo(array, 1);

        array[0].Should().Be(0);
        array[1].Should().Be(1);
        array[2].Should().Be(2);
        array[3].Should().Be(3);
        array[4].Should().Be(0);
    }

    [Fact]
    public void CopyTo_WithNullArray_ThrowsArgumentNullException()
    {
        using ArrayPoolList<int> list = [1];
        Action act = () => list.CopyTo(null!, 0);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CopyTo_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        using ArrayPoolList<int> list = [1];

        int[] array = new int[1];
        Action act = () => list.CopyTo(array, -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CopyTo_WithInsufficientSpace_ThrowsArgumentException()
    {
        using ArrayPoolList<int> list = [1, 2];

        int[] array = new int[1];
        Action act = () => list.CopyTo(array, 0);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enumeration_WorksCorrectly()
    {
        using ArrayPoolList<int> list = [1, 2, 3];

        int index = 0;
        foreach (int item in list)
        {
            item.Should().Be(index + 1);
            index++;
        }

        index.Should().Be(3);
    }

    [Fact]
    public void EnsureCapacity_NegativeOrZeroCapacity_ThrowsArgumentOutOfRangeException()
    {
        using ArrayPoolList<int> list = [];

        Action actNegative = () => list.EnsureCapacity(-1);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actZero = () => list.EnsureCapacity(0);
        actZero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EnsureCapacity_GrowsCapacity()
    {
        using ArrayPoolList<int> list = new(10);
        for (int i = 0; i < 10; i++)
        {
            list.Add(i);
        }

        // This should grow the capacity
        list.Add(10);

        list.Count.Should().Be(11);
    }

    [Fact]
    public void Dispose_CleansUpResources()
    {
        ArrayPoolList<int> list = [1, 2];

        list.Dispose();

        int[] array = list.TestAccessor.Dynamic._items;
        array.Should().BeEmpty();
    }

    [Fact]
    public void Enumerator_Reset_StartsFromBeginning()
    {
        using ArrayPoolList<int> list = [1, 2];

        using var enumerator = list.GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(1);

        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Should().Be(1);
    }

    [Fact]
    public void Enumerator_MovePastEnd_ReturnsFalse()
    {
        using ArrayPoolList<int> list = [1];

        using var enumerator = list.GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void ArrayPoolList_WithReferenceType_HandlesCorrectly()
    {
        using ArrayPoolList<string> list = ["one", "two", "three"];

        list.Count.Should().Be(3);
        list[0].Should().Be("one");
        list[1].Should().Be("two");
        list[2].Should().Be("three");

        list.RemoveAt(1);
        list.Count.Should().Be(2);
        list[0].Should().Be("one");
        list[1].Should().Be("three");
    }
}
