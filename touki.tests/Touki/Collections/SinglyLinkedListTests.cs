// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original license follows:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Touki.Collections;

[TestClass]
public class SinglyLinkedListTests
{
    [TestMethod]
    public void AddFirst()
    {
        SinglyLinkedList<int> list = new();

        list.Count.Should().Be(0);
        list.First.Should().BeNull();
        list.Last.Should().BeNull();

        list.AddFirst(1);
        list.Count.Should().Be(1);
        list.First.Should().NotBeNull();
        list.Last.Should().NotBeNull();
        list.Last.Should().BeSameAs(list.First);
        list.First!.Next.Should().BeNull();
        list.First.Value.Should().Be(1);

        list.AddFirst(2);
        list.Count.Should().Be(2);
        list.First.Should().NotBeNull();
        list.Last.Should().NotBeNull();
        list.Last.Should().NotBeSameAs(list.First);
        list.Last.Should().BeSameAs(list.First.Next);
        list.Last!.Next.Should().BeNull();
        list.First.Value.Should().Be(2);
        list.Last.Value.Should().Be(1);
    }

    [TestMethod]
    public void AddLast()
    {
        SinglyLinkedList<int> list = new();

        list.Count.Should().Be(0);
        list.First.Should().BeNull();
        list.Last.Should().BeNull();

        list.AddLast(1);
        list.Count.Should().Be(1);
        list.First.Should().NotBeNull();
        list.Last.Should().NotBeNull();
        list.Last.Should().BeSameAs(list.First);
        list.First!.Next.Should().BeNull();
        list.First.Value.Should().Be(1);

        list.AddLast(2);
        list.Count.Should().Be(2);
        list.First.Should().NotBeNull();
        list.Last.Should().NotBeNull();
        list.Last.Should().NotBeSameAs(list.First);
        list.Last.Should().BeSameAs(list.First.Next);
        list.Last!.Next.Should().BeNull();
        list.First.Value.Should().Be(1);
        list.Last.Value.Should().Be(2);
    }

    [TestMethod]
    public void MoveToFront()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3, 4, 5);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.MoveCurrentToFront();

        // Should be moved back in front
        enumerator.Current.Should().BeNull();
        list.Count.Should().Be(5);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(1);

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(2);
        enumerator.MoveCurrentToFront();
        enumerator.Current.Value.Should().Be(1);

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(3);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(4);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(5);

        enumerator.MoveCurrentToFront();
        enumerator.Current.Value.Should().Be(4);

        enumerator.MoveNext().Should().BeFalse();
        enumerator.Current.Should().BeNull();
        list!.First!.Value.Should().Be(5);

        list.WalkToList().Should().Equal(5, 2, 1, 3, 4);
        list.EnumerateToList().Should().Equal(5, 2, 1, 3, 4);
    }

    [TestMethod]
    public void MoveToFront_InvalidOperations()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(1);

        var enumerator = list.GetEnumerator();
        Assert.Throws<InvalidOperationException>(enumerator.MoveCurrentToFront);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.MoveCurrentToFront();
        Assert.Throws<InvalidOperationException>(enumerator.MoveCurrentToFront);
    }

    [TestMethod]
    public void RemoveCurrent()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3, 4, 5);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();

        enumerator.RemoveCurrent();
        list.Count.Should().Be(4);
        enumerator.Current.Should().BeNull();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(2);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(3);
        enumerator.RemoveCurrent();
        list.Count.Should().Be(3);
        enumerator.Current.Value.Should().Be(2);

        enumerator.MoveNext().Should().BeTrue();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(5);
        enumerator.RemoveCurrent();
        list.Count.Should().Be(2);
        enumerator.Current.Value.Should().Be(4);
        enumerator.MoveNext().Should().BeFalse();
        enumerator.Current.Should().BeNull();

        list.WalkToList().Should().Equal(2, 4);
        list.EnumerateToList().Should().Equal(2, 4);
    }

    [TestMethod]
    public void RemoveCurrent_InvalidOperations()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(1);
        list.AddLast(2);

        var enumerator = list.GetEnumerator();
        Assert.Throws<InvalidOperationException>(enumerator.RemoveCurrent);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.RemoveCurrent();
        Assert.Throws<InvalidOperationException>(enumerator.RemoveCurrent);
    }

    [TestMethod]
    public void Constructor_CreatesEmptyList()
    {
        SinglyLinkedList<string> list = new();

        list.Count.Should().Be(0);
        list.First.Should().BeNull();
        list.Last.Should().BeNull();
    }

    [TestMethod]
    public void AddFirst_ReturnsNode()
    {
        SinglyLinkedList<int> list = new();

        SinglyLinkedList<int>.Node node = list.AddFirst(42);

        node.Should().NotBeNull();
        node.Value.Should().Be(42);
        node.Should().BeSameAs(list.First);
    }

    [TestMethod]
    public void AddLast_ReturnsNode()
    {
        SinglyLinkedList<int> list = new();

        SinglyLinkedList<int>.Node node = list.AddLast(42);

        node.Should().NotBeNull();
        node.Value.Should().Be(42);
        node.Should().BeSameAs(list.Last);
    }

    [TestMethod]
    public void Node_ImplicitConversion_ReturnsValue()
    {
        SinglyLinkedList<string> list = new();
        SinglyLinkedList<string>.Node node = list.AddFirst("test");

        string value = node;

        value.Should().Be("test");
    }

    [TestMethod]
    public void Node_ImplicitConversion_NullNode_ReturnsDefault()
    {
        SinglyLinkedList<string>.Node? nullNode = null;

        string value = nullNode;

        value.Should().BeNull();
    }

    [TestMethod]
    public void Node_ValueProperty_CanBeSet()
    {
        SinglyLinkedList<int> list = new();
        SinglyLinkedList<int>.Node node = list.AddFirst(10);

        node.Value = 20;

        node.Value.Should().Be(20);
        ((int)node).Should().Be(20);
    }

    [TestMethod]
    public void Node_NextProperty_CanBeSet()
    {
        SinglyLinkedList<int> list = new();
        SinglyLinkedList<int>.Node node1 = list.AddFirst(1);
        SinglyLinkedList<int>.Node node2 = list.AddLast(2);

        node1.Next.Should().BeSameAs(node2);

        SinglyLinkedList<int>.Node newNode = new(3);
        node1.Next = newNode;

        node1.Next.Should().BeSameAs(newNode);
    }

    [TestMethod]
    public void GetEnumerator_EmptyList_MoveNextReturnsFalse()
    {
        SinglyLinkedList<int> list = new();

        var enumerator = list.GetEnumerator();

        enumerator.MoveNext().Should().BeFalse();
    }

    [TestMethod]
    public void GetEnumerator_SingleItem_EnumeratesCorrectly()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(42);

        var enumerator = list.GetEnumerator();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(42);
        enumerator.MoveNext().Should().BeFalse();
    }

    [TestMethod]
    public void GetEnumerator_MultipleItems_EnumeratesInOrder()
    {
        SinglyLinkedList<int> list = new();
        list.AddLast(1);
        list.AddLast(2);
        list.AddLast(3);

        var enumerator = list.GetEnumerator();
        List<int> values = [];

        while (enumerator.MoveNext())
        {
            values.Add(enumerator.Current.Value);
        }

        values.Should().Equal(1, 2, 3);
    }

    [TestMethod]
    public void Enumerator_Reset_ResetsToStart()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.Current.Value.Should().Be(2);

        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(1);
    }

    [TestMethod]
    public void Enumerator_Current_NotMovedReturnsDefault()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(1);

        var enumerator = list.GetEnumerator();

        enumerator.Current.Should().BeNull();
    }

    [TestMethod]
    public void Enumerator_Current_EndReturnsDefault()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(1);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();

        enumerator.Current.Should().BeNull();
    }

    [TestMethod]
    public void RemoveCurrent_FirstNode_UpdatesFirstAndLast()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(1);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        list.Count.Should().Be(0);
        list.First.Should().BeNull();
        list.Last.Should().BeNull();
    }

    [TestMethod]
    public void RemoveCurrent_MiddleNode_ConnectsPreviousToNext()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);
        var firstNode = list.First;
        var lastNode = list.Last;

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        list.Count.Should().Be(2);
        list.First.Should().BeSameAs(firstNode);
        list.Last.Should().BeSameAs(lastNode);
        firstNode!.Next.Should().BeSameAs(lastNode);
    }

    [TestMethod]
    public void RemoveCurrent_LastNode_UpdatesLast()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2);
        var firstNode = list.First;

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        list.Count.Should().Be(1);
        list.First.Should().BeSameAs(firstNode);
        list.Last.Should().BeSameAs(firstNode);
        firstNode!.Next.Should().BeNull();
    }

    [TestMethod]
    public void MoveCurrentToFront_FirstNode_ResetsPosition()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveCurrentToFront();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(1);
    }

    [TestMethod]
    public void MoveCurrentToFront_MiddleNode_MovesToFront()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        var middleNode = enumerator.Current;
        enumerator.MoveCurrentToFront();

        list.First.Should().BeSameAs(middleNode);
        list.First!.Value.Should().Be(2);
        list.First.Next!.Value.Should().Be(1);
    }

    [TestMethod]
    public void MoveCurrentToFront_LastNode_MovesToFrontAndUpdatesLast()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);
        var middleNode = list.First!.Next;

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.MoveNext();
        var lastNode = enumerator.Current;
        enumerator.MoveCurrentToFront();

        list.First.Should().BeSameAs(lastNode);
        list.Last.Should().BeSameAs(middleNode);
        list.Last!.Next.Should().BeNull();
    }

    [TestMethod]
    public void Enumerator_AfterRemoveOrMove_CurrentUpdated()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        var previousNode = enumerator.Current;
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        enumerator.Current.Should().BeSameAs(previousNode);
    }

    [TestMethod]
    public void List_WithNullValues_HandlesCorrectly()
    {
        SinglyLinkedList<string?> list = new();
        list.AddFirst(null);
        list.AddLast("test");
        list.AddLast(null);

        list.Count.Should().Be(3);
        list.First!.Value.Should().BeNull();
        list.Last!.Value.Should().BeNull();

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.Current.Value.Should().BeNull();
        enumerator.MoveNext();
        enumerator.Current.Value.Should().Be("test");
        enumerator.MoveNext();
        enumerator.Current.Value.Should().BeNull();
    }

    [TestMethod]
    public void Enumerator_Reset_AfterRemoveOrMove_ResetsCorrectly()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(1);
    }

    [TestMethod]
    public void Enumerator_MoveNext_AfterReset_StartsFromBeginning()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.MoveNext().Should().BeFalse();

        enumerator.Reset();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(1);
    }

    [TestMethod]
    public void RemoveCurrent_OnlyNode_LeavesEmptyList()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(42);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        list.Count.Should().Be(0);
        list.First.Should().BeNull();
        list.Last.Should().BeNull();
        enumerator.Current.Should().BeNull();
        enumerator.MoveNext().Should().BeFalse();
    }

    [TestMethod]
    public void MoveCurrentToFront_OnlyNode_ResetsEnumeratorPosition()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(42);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveCurrentToFront();

        enumerator.Current.Should().BeNull();
        list.Count.Should().Be(1);
        list.First!.Value.Should().Be(42);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(42);
    }

    [TestMethod]
    public void Enumerator_RemoveCurrent_ThenMoveNext_ContinuesFromCorrectPosition()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3, 4);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(3);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(4);
        enumerator.MoveNext().Should().BeFalse();
    }

    [TestMethod]
    public void Enumerator_MoveCurrentToFront_ThenMoveNext_ContinuesFromCorrectPosition()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3, 4);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveNext();
        enumerator.MoveCurrentToFront();

        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(3);
        enumerator.MoveNext().Should().BeTrue();
        enumerator.Current.Value.Should().Be(4);
        enumerator.MoveNext().Should().BeFalse();
    }

    [TestMethod]
    public void RemoveCurrent_ConsecutiveRemoves_ThrowsInvalidOperation()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        enumerator.Invoking(e => e.RemoveCurrent())
            .Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void MoveCurrentToFront_ConsecutiveMoves_ThrowsInvalidOperation()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveCurrentToFront();

        enumerator.Invoking(e => e.MoveCurrentToFront())
            .Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void RemoveCurrent_AfterMoveToFront_ThrowsInvalidOperation()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.MoveCurrentToFront();

        enumerator.Invoking(e => e.RemoveCurrent())
            .Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void MoveCurrentToFront_AfterRemoveCurrent_ThrowsInvalidOperation()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext();
        enumerator.RemoveCurrent();

        enumerator.Invoking(e => e.MoveCurrentToFront())
            .Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public void Enumerator_MultipleEnumerators_IndependentOperation()
    {
        SinglyLinkedList<int> list = new();
        list.AddAll(1, 2, 3);

        var enumerator1 = list.GetEnumerator();
        var enumerator2 = list.GetEnumerator();

        enumerator1.MoveNext();
        enumerator1.MoveNext();
        enumerator2.MoveNext();

        enumerator1.Current.Value.Should().Be(2);
        enumerator2.Current.Value.Should().Be(1);

        enumerator1.RemoveCurrent();
        list.Count.Should().Be(2);

        enumerator2.MoveNext().Should().BeTrue();
        enumerator2.Current.Value.Should().Be(3);
    }

    [TestMethod]
    public void AddFirst_MultipleValues_MaintainsOrder()
    {
        SinglyLinkedList<int> list = new();

        list.AddFirst(1);
        list.AddFirst(2);
        list.AddFirst(3);

        list.WalkToList().Should().Equal(3, 2, 1);
        list.Count.Should().Be(3);
        list.First!.Value.Should().Be(3);
        list.Last!.Value.Should().Be(1);
    }

    [TestMethod]
    public void AddLast_MultipleValues_MaintainsOrder()
    {
        SinglyLinkedList<int> list = new();

        list.AddLast(1);
        list.AddLast(2);
        list.AddLast(3);

        list.WalkToList().Should().Equal(1, 2, 3);
        list.Count.Should().Be(3);
        list.First!.Value.Should().Be(1);
        list.Last!.Value.Should().Be(3);
    }

    [TestMethod]
    public void Node_Constructor_SetsValueCorrectly()
    {
        SinglyLinkedList<string>.Node node = new("test");

        node.Value.Should().Be("test");
        node.Next.Should().BeNull();
    }

    [TestMethod]
    public void List_MixedAddOperations_MaintainsCorrectStructure()
    {
        SinglyLinkedList<int> list = new();

        list.AddLast(2);
        list.AddFirst(1);
        list.AddLast(3);
        list.AddFirst(0);

        list.WalkToList().Should().Equal(0, 1, 2, 3);
        list.Count.Should().Be(4);
        list.First!.Value.Should().Be(0);
        list.Last!.Value.Should().Be(3);
    }

    [TestMethod]
    public void Enumerator_FinishedState_MoveNextReturnsFalse()
    {
        SinglyLinkedList<int> list = new();
        list.AddFirst(1);

        var enumerator = list.GetEnumerator();
        enumerator.MoveNext().Should().BeTrue();
        enumerator.MoveNext().Should().BeFalse();
        enumerator.MoveNext().Should().BeFalse();
        enumerator.MoveNext().Should().BeFalse();
    }

    [TestMethod]
    public void WalkToList_EmptyList_ReturnsEmptyList()
    {
        SinglyLinkedList<int> list = new();

        List<int> result = list.WalkToList();

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void EnumerateToList_EmptyList_ReturnsEmptyList()
    {
        SinglyLinkedList<int> list = new();

        List<int> result = list.EnumerateToList();

        result.Should().BeEmpty();
    }

    [TestMethod]
    public void Node_ImplicitConversion_WithValueTypes_ReturnsCorrectDefault()
    {
        SinglyLinkedList<int>.Node? nullNode = null;

        int value = nullNode;

        value.Should().Be(0);
    }

    [TestMethod]
    public void List_LargeNumberOfItems_MaintainsPerformance()
    {
        SinglyLinkedList<int> list = new();
        const int itemCount = 1000;

        for (int i = 0; i < itemCount; i++)
        {
            list.AddLast(i);
        }

        list.Count.Should().Be(itemCount);
        list.First!.Value.Should().Be(0);
        list.Last!.Value.Should().Be(itemCount - 1);

        List<int> walked = list.WalkToList();
        walked.Should().HaveCount(itemCount);
        walked[0].Should().Be(0);
        walked[itemCount - 1].Should().Be(itemCount - 1);
    }
}

internal static class ListExtensions
{
    public static void AddAll<T>(this SinglyLinkedList<T> linkedList, params T[] values)
    {
        foreach (T value in values)
        {
            linkedList.AddLast(value);
        }
    }

    public static List<T> WalkToList<T>(this SinglyLinkedList<T> linkedList)
    {
        List<T> list = new(linkedList.Count);
        var node = linkedList.First;
        while (node is not null)
        {
            list.Add(node);
            node = node.Next;
        }

        return list;
    }

    public static List<T> EnumerateToList<T>(this SinglyLinkedList<T> linkedList)
    {
        List<T> list = new(linkedList.Count);
        var enumerator = linkedList.GetEnumerator();
        while (enumerator.MoveNext())
        {
            list.Add(enumerator.Current);
        }

        return list;
    }
}
