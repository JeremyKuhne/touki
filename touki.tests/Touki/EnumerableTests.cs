// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki;

public class EnumerableTests
{
    // Test implementation of Enumerable<T>
    private class TestEnumerable : EnumerableBase<int>
    {
        private readonly int[] _items;
        private int _index = -1;

        public TestEnumerable(params int[] items)
        {
            _items = items;
        }

        public override bool MoveNext()
        {
            _index++;
            if (_index < _items.Length)
            {
                Current = _items[_index];
                return true;
            }

            return false;
        }

        public override void Reset()
        {
            // Override the base implementation to support Reset
            _index = -1;
            Current = default;
        }

        // Track disposal for testing
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
        }
    }

    // Test implementation that throws on MoveNext
    private class ThrowingEnumerable : EnumerableBase<int>
    {
        public override bool MoveNext() => throw new InvalidOperationException("Test exception");

        protected override void Dispose(bool disposing) { }
    }

    [Fact]
    public void EnumeratorAndEnumerableAreTheSameInstance()
    {
        TestEnumerable enumerable = new(1, 2, 3);
        IEnumerator<int> enumerator = enumerable.GetEnumerator();

        enumerator.Should().BeSameAs(enumerable);
    }

    [Fact]
    public void NonGenericGetEnumerator_ReturnsThisInstance()
    {
        TestEnumerable enumerable = new(1, 2, 3);
        IEnumerable nonGenericEnumerable = (IEnumerable)enumerable;
        IEnumerator enumerator = nonGenericEnumerable.GetEnumerator();

        enumerator.Should().BeSameAs(enumerable);
    }

    [Fact]
    public void MoveNext_ReturnsTrue_AndUpdatesCurrent_WhenThereAreMoreItems()
    {
        TestEnumerable enumerable = new(42, 84);

        bool hasFirst = enumerable.MoveNext();
        hasFirst.Should().BeTrue();
        enumerable.Current.Should().Be(42);

        bool hasSecond = enumerable.MoveNext();
        hasSecond.Should().BeTrue();
        enumerable.Current.Should().Be(84);
    }

    [Fact]
    public void MoveNext_ReturnsFalse_WhenNoMoreItemsAvailable()
    {
        TestEnumerable enumerable = new(42);

        bool hasFirst = enumerable.MoveNext();
        hasFirst.Should().BeTrue();

        bool hasSecond = enumerable.MoveNext();
        hasSecond.Should().BeFalse();
    }

    [Fact]
    public void MoveNext_ReturnsFalse_WhenEnumerableIsEmpty()
    {
        TestEnumerable enumerable = new();

        bool hasItem = enumerable.MoveNext();

        hasItem.Should().BeFalse();
    }

    [Fact]
    public void Current_AfterCreation_ReturnsDefault()
    {
        TestEnumerable enumerable = new(42);

        // Before any MoveNext calls, Current should be default
        enumerable.Current.Should().Be(default);
    }

    [Fact]
    public void Enumerable_WorksWithForeach()
    {
        TestEnumerable enumerable = new(1, 2, 3);
        List<int> items = [];

        foreach (int item in enumerable)
        {
            items.Add(item);
        }

        items.Should().Equal([1, 2, 3]);
    }

    [Fact]
    public void Enumerable_WithNoItems_ProducesEmptyCollection()
    {
        TestEnumerable enumerable = new();
        List<int> items = [];

        foreach (int item in enumerable)
        {
            items.Add(item);
        }

        items.Should().BeEmpty();
    }

    [Fact]
    public void NonGenericCurrent_ReturnsSameAsGenericCurrent()
    {
        TestEnumerable enumerable = new(42);
        IEnumerator nonGenericEnumerator = (IEnumerator)enumerable;

        enumerable.MoveNext();

        object? nonGenericCurrent = nonGenericEnumerator.Current;
        nonGenericCurrent.Should().Be(enumerable.Current);
    }

    [Fact]
    public void Reset_SetsCurrentToDefault_AndRestartsEnumeration()
    {
        TestEnumerable enumerable = new(1, 2, 3);

        // Consume a few items
        enumerable.MoveNext();
        enumerable.MoveNext();
        enumerable.Current.Should().Be(2);

        // Reset and verify we start from the beginning
        enumerable.Reset();
        enumerable.Current.Should().Be(default);

        // Verify we can enumerate from the beginning
        enumerable.MoveNext();
        enumerable.Current.Should().Be(1);
    }

    [Fact]
    public void Reset_ThrowsNotSupported_WhenUsingBaseImplementation()
    {
        ThrowingEnumerable enumerable = new();

        Action reset = () => ((IEnumerator)enumerable).Reset();

        reset.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Dispose_Properly_DisposesTheEnumerator()
    {
        TestEnumerable enumerable = new(1, 2, 3);

        enumerable.Dispose();

        enumerable.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Enumerable_CanBeUsedMultipleTimes()
    {
        TestEnumerable enumerable = new(1, 2, 3);

        // First enumeration
        List<int> firstPass = [];
        foreach (int item in enumerable)
        {
            firstPass.Add(item);
        }

        // Reset for second enumeration
        enumerable.Reset();

        // Second enumeration
        List<int> secondPass = [];
        foreach (int item in enumerable)
        {
            secondPass.Add(item);
        }

        firstPass.Should().Equal([1, 2, 3]);
        secondPass.Should().Equal([1, 2, 3]);
    }
}
