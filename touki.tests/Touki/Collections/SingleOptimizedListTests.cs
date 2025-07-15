// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

public class SingleOptimizedListTests
{
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
        accessor.BackingList.Should().BeEmpty();
    }

    [Fact]
    public void Clear_StateTransition_MultipleToEmpty()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];

        var accessorBefore = list.TestAccessor();
        accessorBefore.HasItem.Should().BeTrue();
        accessorBefore.BackingList.Should().NotBeNull();

        list.Clear();

        list.Count.Should().Be(0);
        var accessorAfter = list.TestAccessor();
        accessorAfter.HasItem.Should().BeFalse();
        accessorAfter.BackingList.Should().BeEmpty();
    }

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
    public void Contains_FindsExistingItems()
    {
        // Empty list
        using SingleOptimizedList<int> emptyList = [];
        emptyList.Contains(1).Should().BeFalse();

        // Single item
        using SingleOptimizedList<int> singleList = [42];
        singleList.Contains(42).Should().BeTrue();
        singleList.Contains(1).Should().BeFalse();

        // Multiple items
        using SingleOptimizedList<int> multiList = [1, 2, 3];
        multiList.Contains(1).Should().BeTrue();
        multiList.Contains(2).Should().BeTrue();
        multiList.Contains(3).Should().BeTrue();
        multiList.Contains(4).Should().BeFalse();
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
    public void CopyTo_SingleItem_CopiesCorrectly()
    {
        using SingleOptimizedList<int> list = [42];

        int[] array = new int[2];
        list.CopyTo(array, 1);

        array[0].Should().Be(0);
        array[1].Should().Be(42);
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
    public void ICollection_IsReadOnly_ReturnsFalse()
    {
        using SingleOptimizedList<int> list = [];
        ICollection<int> collection = list;
        collection.IsReadOnly.Should().BeFalse();
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
    public void Insert_AtEnd_AppendsProperly()
    {
        using SingleOptimizedList<int> list = [1];
        list.Insert(1, 2);

        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(2);

        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.BackingList.Should().NotBeNull();
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
    public void Insert_InvalidIndex_ThrowsException()
    {
        using SingleOptimizedList<int> list = [1, 2];

        Action actNegative = () => list.Insert(-1, 0);
        actNegative.Should().Throw<ArgumentOutOfRangeException>();

        Action actTooLarge = () => list.Insert(3, 0);
        actTooLarge.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Insert_StateTransition_SingleToMultiple()
    {
        using SingleOptimizedList<int> list = [42];

        var accessorBefore = list.TestAccessor();
        accessorBefore.HasItem.Should().BeTrue();
        accessorBefore.Item.Should().Be(42);
        accessorBefore.BackingList.Should().BeNull();

        list.Insert(0, 41);

        list.Count.Should().Be(2);
        list[0].Should().Be(41);
        list[1].Should().Be(42);

        var accessorAfter = list.TestAccessor();
        accessorAfter.HasItem.Should().BeTrue();
        accessorAfter.BackingList.Should().NotBeNull();
        accessorAfter.BackingList!.Count.Should().Be(2);
    }

    [Fact]
    public void Remove_ExistingItem_RemovesCorrectly()
    {
        // Single item case
        using SingleOptimizedList<int> list1 = [42];
        bool result1 = list1.Remove(42);

        result1.Should().BeTrue();
        list1.Count.Should().Be(0);

        // Multiple items case
        using SingleOptimizedList<int> list2 = [1, 2, 3];
        bool result2 = list2.Remove(2);

        result2.Should().BeTrue();
        list2.Count.Should().Be(2);
        list2[0].Should().Be(1);
        list2[1].Should().Be(3);

        // Non-existent item
        using SingleOptimizedList<int> list3 = [1, 2, 3];
        bool result3 = list3.Remove(4);

        result3.Should().BeFalse();
        list3.Count.Should().Be(3);
    }

    [Fact]
    public void RemoveAll_RemovesMatchingItems()
    {
        // Single item matching predicate
        using SingleOptimizedList<int> list1 = [42];
        int removed1 = list1.RemoveAll(x => x > 40);

        removed1.Should().Be(1);
        list1.Count.Should().Be(0);

        // Multiple items with some matching
        using SingleOptimizedList<int> list2 = [1, 2, 3, 4, 5];
        int removed2 = list2.RemoveAll(x => x % 2 == 0);

        removed2.Should().Be(2);
        list2.Count.Should().Be(3);
        list2[0].Should().Be(1);
        list2[1].Should().Be(3);
        list2[2].Should().Be(5);

        // No matches
        using SingleOptimizedList<int> list3 = [1, 3, 5];
        int removed3 = list3.RemoveAll(x => x % 2 == 0);

        removed3.Should().Be(0);
        list3.Count.Should().Be(3);
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
    public void RemoveAt_MultipleItems_RemovesCorrectItem()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];

        list.RemoveAt(1);

        list.Count.Should().Be(2);
        list[0].Should().Be(1);
        list[1].Should().Be(3);
    }

    [Fact]
    public void RemoveAt_MultipleItemsDownToOne_MaintainsBackingList()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];

        list.RemoveAt(0);
        list.RemoveAt(0);

        list.Count.Should().Be(1);
        list[0].Should().Be(3);

        var accessor = list.TestAccessor();
        accessor.HasItem.Should().BeTrue();
        accessor.BackingList.Should().NotBeNull();
        accessor.BackingList!.Count.Should().Be(1);
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
    public void RemoveAt_StateTransition_SingleToEmpty()
    {
        using SingleOptimizedList<int> list = [42];

        var accessorBefore = list.TestAccessor();
        accessorBefore.HasItem.Should().BeTrue();
        accessorBefore.Item.Should().Be(42);
        accessorBefore.BackingList.Should().BeNull();

        list.RemoveAt(0);

        list.Count.Should().Be(0);
        var accessorAfter = list.TestAccessor();
        accessorAfter.HasItem.Should().BeFalse();
        accessorAfter.BackingList.Should().BeNull();
    }

    [Fact]
    public void UnsafeValues_EmptyList_ReturnsEmptySpan()
    {
        using SingleOptimizedList<int> list = [];
        list.UnsafeValues.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void UnsafeValues_MultipleItems_ReturnsCorrectSpan()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];
        Span<int> values = list.UnsafeValues;

        values.Length.Should().Be(3);
        values[0].Should().Be(1);
        values[1].Should().Be(2);
        values[2].Should().Be(3);
    }

    [Fact]
    public void UnsafeValues_SingleItem_ReturnsCorrectSpan()
    {
        using SingleOptimizedList<int> list = [42];
        Span<int> values = list.UnsafeValues;

        values.Length.Should().Be(1);
        values[0].Should().Be(42);
    }

    [Fact]
    public void Values_EmptyList_ReturnsEmptySpan()
    {
        using SingleOptimizedList<int> list = [];
        list.Values.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Values_MultipleItems_ReturnsCorrectSpan()
    {
        using SingleOptimizedList<int> list = [1, 2, 3];
        ReadOnlySpan<int> values = list.Values;

        values.Length.Should().Be(3);
        values[0].Should().Be(1);
        values[1].Should().Be(2);
        values[2].Should().Be(3);
    }

    [Fact]
    public void Values_SingleItem_ReturnsCorrectSpan()
    {
        using SingleOptimizedList<int> list = [42];
        ReadOnlySpan<int> values = list.Values;

        values.Length.Should().Be(1);
        values[0].Should().Be(42);
    }
}
