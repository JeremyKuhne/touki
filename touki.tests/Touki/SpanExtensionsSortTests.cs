// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SpanExtensionsSortTests
{
    // -----------------------------------------------------------------------
    //  Sort<T>(Span<T>)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_Empty_NoOp()
    {
        Span<int> span = [];
        span.Sort();
        span.Length.Should().Be(0);
    }

    [Fact]
    public void Sort_SingleElement_NoOp()
    {
        Span<int> span = [42];
        span.Sort();
        span[0].Should().Be(42);
    }

    [Fact]
    public void Sort_TwoElements_Sorts()
    {
        Span<int> span = [2, 1];
        span.Sort();
        span.ToArray().Should().Equal(1, 2);
    }

    [Fact]
    public void Sort_ThreeElements_Sorts()
    {
        Span<int> span = [3, 1, 2];
        span.Sort();
        span.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Sort_AlreadySorted_StaysSorted()
    {
        Span<int> span = [1, 2, 3, 4, 5];
        span.Sort();
        span.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Sort_ReverseSorted_Sorts()
    {
        Span<int> span = [5, 4, 3, 2, 1];
        span.Sort();
        span.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void Sort_WithDuplicates_Sorts()
    {
        Span<int> span = [3, 1, 2, 1, 3, 2];
        span.Sort();
        span.ToArray().Should().Equal(1, 1, 2, 2, 3, 3);
    }

    [Fact]
    public void Sort_LargeRandom_Sorts()
    {
        // Force IntroSort into HeapSort/QuickSort paths. Deterministic LCG avoids CA5394.
        uint state = 12345;
        int[] data = new int[2000];
        for (int i = 0; i < data.Length; i++)
        {
            state = (state * 1664525u) + 1013904223u;
            data[i] = (int)(state % 10_000);
        }

        int[] expected = (int[])data.Clone();
        Array.Sort(expected);

        Span<int> span = data;
        span.Sort();

        span.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void Sort_AllEqual_NoChange()
    {
        Span<int> span = [5, 5, 5, 5, 5];
        span.Sort();
        span.ToArray().Should().Equal(5, 5, 5, 5, 5);
    }

    [Fact]
    public void Sort_Strings_UsesIComparable()
    {
        Span<string> span = ["banana", "apple", "cherry"];
        span.Sort();
        span.ToArray().Should().Equal("apple", "banana", "cherry");
    }

    // -----------------------------------------------------------------------
    //  Sort<T>(Span<T>, Comparison<T>)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_Comparison_Descending()
    {
        Span<int> span = [3, 1, 2, 5, 4];
        span.Sort((a, b) => b - a);
        span.ToArray().Should().Equal(5, 4, 3, 2, 1);
    }

    [Fact]
    public void Sort_NullComparison_Throws()
    {
        Action act = static () =>
        {
            Span<int> span = [1, 2];
            span.Sort((Comparison<int>)null!);
        };
        act.Should().Throw<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    //  Sort<T,TComparer>(Span<T>, TComparer)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_Comparer_Works()
    {
        Span<int> span = [3, 1, 2];
        span.Sort(Comparer<int>.Default);
        span.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Sort_NullComparer_UsesDefault()
    {
        Span<int> span = [3, 1, 2];
        span.Sort((IComparer<int>?)null);
        span.ToArray().Should().Equal(1, 2, 3);
    }

    // -----------------------------------------------------------------------
    //  Sort<TKey,TValue>(Span<TKey>, Span<TValue>)
    // -----------------------------------------------------------------------

    [Fact]
    public void Sort_KeysItems_Basic()
    {
        Span<int> keys = [3, 1, 2];
        Span<string> items = ["three", "one", "two"];
        keys.Sort(items);
        keys.ToArray().Should().Equal(1, 2, 3);
        items.ToArray().Should().Equal("one", "two", "three");
    }

    [Fact]
    public void Sort_KeysItems_LargeRandom()
    {
        uint state = 99;
        int[] keys = new int[500];
        int[] values = new int[500];
        for (int i = 0; i < keys.Length; i++)
        {
            state = (state * 1664525u) + 1013904223u;
            keys[i] = (int)state;
            values[i] = i;
        }

        int[] keysCopy = (int[])keys.Clone();
        int[] valuesCopy = (int[])values.Clone();
        Array.Sort(keysCopy, valuesCopy);

        ((Span<int>)keys).Sort((Span<int>)values);

        keys.Should().Equal(keysCopy);
        values.Should().Equal(valuesCopy);
    }

    [Fact]
    public void Sort_KeysItems_ItemsTooShort_Throws()
    {
        Action act = static () =>
        {
            Span<int> keys = [1, 2, 3];
            Span<int> items = new int[2];
            keys.Sort(items);
        };
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Sort_KeysItems_Comparison()
    {
        Span<int> keys = [3, 1, 2];
        Span<string> items = ["three", "one", "two"];
        keys.Sort(items, (a, b) => b - a);
        keys.ToArray().Should().Equal(3, 2, 1);
        items.ToArray().Should().Equal("three", "two", "one");
    }

    [Fact]
    public void Sort_KeysItems_Comparer_NullUsesDefault()
    {
        Span<int> keys = [3, 1, 2];
        Span<int> items = [30, 10, 20];
        keys.Sort(items, (IComparer<int>?)null);
        keys.ToArray().Should().Equal(1, 2, 3);
        items.ToArray().Should().Equal(10, 20, 30);
    }
}
