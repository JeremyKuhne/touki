// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

public class SpanExtensionsSortTests
{
    // -----------------------------------------------------------------------
    //  Sort<T>(Span<T>)
    // -----------------------------------------------------------------------

    [Test]
    public void Sort_Empty_NoOp()
    {
        Span<int> span = [];
        span.Sort();
        span.Length.Should().Be(0);
    }

    [Test]
    public void Sort_SingleElement_NoOp()
    {
        Span<int> span = [42];
        span.Sort();
        span[0].Should().Be(42);
    }

    [Test]
    public void Sort_TwoElements_Sorts()
    {
        Span<int> span = [2, 1];
        span.Sort();
        span.ToArray().Should().Equal(1, 2);
    }

    [Test]
    public void Sort_ThreeElements_Sorts()
    {
        Span<int> span = [3, 1, 2];
        span.Sort();
        span.ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void Sort_AlreadySorted_StaysSorted()
    {
        Span<int> span = [1, 2, 3, 4, 5];
        span.Sort();
        span.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Test]
    public void Sort_ReverseSorted_Sorts()
    {
        Span<int> span = [5, 4, 3, 2, 1];
        span.Sort();
        span.ToArray().Should().Equal(1, 2, 3, 4, 5);
    }

    [Test]
    public void Sort_WithDuplicates_Sorts()
    {
        Span<int> span = [3, 1, 2, 1, 3, 2];
        span.Sort();
        span.ToArray().Should().Equal(1, 1, 2, 2, 3, 3);
    }

    [Test]
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

    [Test]
    public void Sort_AllEqual_NoChange()
    {
        Span<int> span = [5, 5, 5, 5, 5];
        span.Sort();
        span.ToArray().Should().Equal(5, 5, 5, 5, 5);
    }

    [Test]
    public void Sort_Strings_UsesIComparable()
    {
        Span<string> span = ["banana", "apple", "cherry"];
        span.Sort();
        span.ToArray().Should().Equal("apple", "banana", "cherry");
    }

    // -----------------------------------------------------------------------
    //  Sort<T>(Span<T>, Comparison<T>)
    // -----------------------------------------------------------------------

    [Test]
    public void Sort_Comparison_Descending()
    {
        Span<int> span = [3, 1, 2, 5, 4];
        span.Sort((a, b) => b - a);
        span.ToArray().Should().Equal(5, 4, 3, 2, 1);
    }

    [Test]
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

    [Test]
    public void Sort_Comparer_Works()
    {
        Span<int> span = [3, 1, 2];
        span.Sort(Comparer<int>.Default);
        span.ToArray().Should().Equal(1, 2, 3);
    }

    [Test]
    public void Sort_NullComparer_UsesDefault()
    {
        Span<int> span = [3, 1, 2];
        span.Sort((IComparer<int>?)null);
        span.ToArray().Should().Equal(1, 2, 3);
    }

    // -----------------------------------------------------------------------
    //  Sort<TKey,TValue>(Span<TKey>, Span<TValue>)
    // -----------------------------------------------------------------------

    [Test]
    public void Sort_KeysItems_Basic()
    {
        Span<int> keys = [3, 1, 2];
        Span<string> items = ["three", "one", "two"];
        keys.Sort(items);
        keys.ToArray().Should().Equal(1, 2, 3);
        items.ToArray().Should().Equal("one", "two", "three");
    }

    [Test]
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

    [Test]
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

    [Test]
    public void Sort_KeysItems_Comparison()
    {
        Span<int> keys = [3, 1, 2];
        Span<string> items = ["three", "one", "two"];
        keys.Sort(items, (a, b) => b - a);
        keys.ToArray().Should().Equal(3, 2, 1);
        items.ToArray().Should().Equal("three", "two", "one");
    }

    [Test]
    public void Sort_KeysItems_Comparer_NullUsesDefault()
    {
        Span<int> keys = [3, 1, 2];
        Span<int> items = [30, 10, 20];
        keys.Sort(items, (IComparer<int>?)null);
        keys.ToArray().Should().Equal(1, 2, 3);
        items.ToArray().Should().Equal(10, 20, 30);
    }

    // -----------------------------------------------------------------------
    //  Larger / worst-case shape coverage. Reverse-sorted is a known pattern
    //  that exercises deeper IntroSort recursion and partition paths.
    // -----------------------------------------------------------------------

    [Test]
    public void Sort_LargeReverseSorted_ProducesSortedResult()
    {
        int[] data = new int[2048];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = data.Length - i;
        }

        Span<int> span = data;
        span.Sort();

        for (int i = 1; i < data.Length; i++)
        {
            data[i].Should().BeGreaterThanOrEqualTo(data[i - 1]);
        }
    }

    [Test]
    public void Sort_KeysItems_LargeReverseSorted_ProducesSortedResult()
    {
        int[] keys = new int[2048];
        int[] values = new int[2048];
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i] = keys.Length - i;
            values[i] = i;
        }

        ((Span<int>)keys).Sort((Span<int>)values);

        for (int i = 1; i < keys.Length; i++)
        {
            keys[i].Should().BeGreaterThanOrEqualTo(keys[i - 1]);
        }
    }
}
