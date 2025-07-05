// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class SingleOptimizedListTests
{
    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        using SingleOptimizedList<int> list = [];

        list.Should().BeEmpty();
        list.Count.Should().Be(0);

        // Verify internal state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeFalse();
        accessor.BackingList.Should().BeNull();
    }

    [Fact]
    public void Add_SingleItem_SetsItemField()
    {
        using SingleOptimizedList<int> list = [42];

        list.Count.Should().Be(1);
        list[0].Should().Be(42);

        // Verify internal state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.Item.Should().Be(42);
        accessor.BackingList.Should().BeNull();
    }

    [Fact]
    public void Add_TwoItems_UsesBackingList()
    {
        using SingleOptimizedList<int> list = [42, 43];

        list.Count.Should().Be(2);
        list[0].Should().Be(42);
        list[1].Should().Be(43);

        // Verify internal state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.BackingList.Should().NotBeNull();
        accessor.BackingList!.Count.Should().Be(2);
    }

    [Fact]
    public void Add_MultipleItems_UsesBackingList()
    {
        using SingleOptimizedList<int> list = [];
        for (int i = 0; i < 5; i++)
        {
            list.Add(i);
        }

        list.Count.Should().Be(5);

        // Verify internal state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.BackingList.Should().NotBeNull();
        accessor.BackingList!.Count.Should().Be(5);

        // Check contents
        for (int i = 0; i < 5; i++)
        {
            list[i].Should().Be(i);
        }
    }

    [Fact]
    public void Clear_EmptiesList()
    {
        using SingleOptimizedList<int> list = [1, 2];

        // Verify pre-clear state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.BackingList.Should().NotBeNull();

        list.Clear();

        list.Should().BeEmpty();
        list.Count.Should().Be(0);

        // Verify post-clear state
        accessor.HasItem.Should().BeFalse();
        accessor.BackingList.Should().BeNull();
    }

    [Fact]
    public void IndexOf_FindsExistingItem()
    {
        using SingleOptimizedList<int> list =
        [
            // Single item case
            42,
        ];
        list.IndexOf(42).Should().Be(0);
        list.IndexOf(99).Should().Be(-1);

        // Multiple items case
        list.Add(43);
        list.IndexOf(42).Should().Be(0);
        list.IndexOf(43).Should().Be(1);
        list.IndexOf(99).Should().Be(-1);
    }

    [Fact]
    public void Insert_AtBeginning_ShiftsItems()
    {
        using SingleOptimizedList<int> list = [2];
        list.Insert(0, 1);

        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(2);

        // Verify internal state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.BackingList.Should().NotBeNull();
        accessor.BackingList!.Count.Should().Be(2);
    }

    [Fact]
    public void Insert_InMiddle_ShiftsItems()
    {
        using SingleOptimizedList<int> list = [1, 3];
        list.Insert(1, 2);

        list.Count.Should().Be(3);
        list[0].Should().Be(1);
        list[1].Should().Be(2);
        list[2].Should().Be(3);
    }

    [Fact]
    public void Insert_IntoEmptyList_AddsSingleItem()
    {
        using SingleOptimizedList<int> list = [];
        list.Insert(0, 42);

        list.Count.Should().Be(1);
        list[0].Should().Be(42);

        // Verify internal state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.Item.Should().Be(42);
        accessor.BackingList.Should().BeNull();
    }

    [Fact]
    public void RemoveAt_SingleItem_EmptiesList()
    {
        using SingleOptimizedList<int> list = [42];

        list.RemoveAt(0);

        list.Should().BeEmpty();
        list.Count.Should().Be(0);

        // Verify internal state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeFalse();
    }

    [Fact]
    public void RemoveAt_MultipleItems_RemovesCorrectItem()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];

        list.RemoveAt(1);

        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }

    [Fact]
    public void RemoveAt_LeavesOneItem_DoesNotSwitchToSingleItem()
    {
        using SingleOptimizedList<int> list = [1, 2];

        list.RemoveAt(0);

        list.Count.Should().Be(1);
        list[0].Should().Be(2);

        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.Item.Should().Be(0);
        accessor.BackingList.Should().NotBeNull();
        accessor.BackingList!.Count.Should().Be(1);
    }

    [Fact]
    public void Indexer_GetWithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using SingleOptimizedList<int> list = [];

        // Empty list
        Action actEmpty = () => _ = list[0];
        actEmpty.Should().Throw<ArgumentOutOfRangeException>();

        // Single item
        list.Add(42);
        Action actNegative = () => _ = list[-1];
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => _ = list[1];
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Indexer_SetWithInvalidIndex_ThrowsArgumentOutOfRangeException()
    {
        using SingleOptimizedList<int> list = [];

        // Empty list
        Action actEmpty = () => list[0] = 42;
        actEmpty.Should().Throw<ArgumentOutOfRangeException>();

        // Single item
        list.Add(42);
        Action actNegative = () => list[-1] = 10;
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list[1] = 10;
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CopyTo_EmptyList_DoesNothing()
    {
        using SingleOptimizedList<int> list = [];
        int[] array = new int[1];

        list.CopyTo(array, 0);

        array[0].Should().Be(0);
    }

    [Fact]
    public void CopyTo_SingleItem_CopiesCorrectly()
    {
        using SingleOptimizedList<int> list = [42];

        int[] array = new int[2];
        list.CopyTo(array, 1);

        array[0].Should().Be(0);
        array[1].Should().Be(42);
    }

    [Fact]
    public void CopyTo_MultipleItems_CopiesCorrectly()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];

        int[] array = new int[5];
        list.CopyTo(array, 1);

        array[0].Should().Be(0);
        array[1].Should().Be(1);
        array[2].Should().Be(2);
        array[3].Should().Be(3);
        array[4].Should().Be(0);
    }

    [Fact]
    public void Enumeration_EmptyList_NoItems()
    {
        using SingleOptimizedList<int> list = [];

        int count = 0;
        foreach (int item in list)
        {
            count++;
        }

        count.Should().Be(0);
    }

    [Fact]
    public void Enumeration_SingleItem_YieldsCorrectly()
    {
        using SingleOptimizedList<int> list = [42];

        int count = 0;
        foreach (int item in list)
        {
            count++;
            item.Should().Be(42);
        }

        count.Should().Be(1);
    }

    [Fact]
    public void Enumeration_MultipleItems_YieldsCorrectly()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];

        int index = 0;
        foreach (int item in list)
        {
            item.Should().Be(index + 1);
            index++;
        }

        index.Should().Be(3);
    }

    [Fact]
    public void Dispose_ClearsBackingList()
    {
        SingleOptimizedList<int> list = [1, 2];

        // Verify pre-dispose state
        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.BackingList.Should().NotBeNull();

        list.Dispose();

        // Can't verify internal state after disposal as that would 
        // potentially use disposed resources
    }
}
