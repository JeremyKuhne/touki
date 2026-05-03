// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Algorithm adapted from dotnet/runtime ArraySortHelper (MIT licensed).
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Collections/Generic/ArraySortHelper.cs

namespace Touki;

public static partial class SpanExtensions
{
    extension<T>(Span<T> keys)
    {
        /// <summary>
        ///  Sorts the elements in the entire <see cref="Span{T}"/> using the
        ///  <see cref="IComparable{T}"/> implementation of each element of the span.
        /// </summary>
        public void Sort() => SortInternal<T, ComparerComparer<T>>(keys, default);

        /// <summary>
        ///  Sorts the elements in the entire span using the specified <paramref name="comparison"/>.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="comparison"/> is <see langword="null"/>.</exception>
        public void Sort(Comparison<T> comparison)
        {
            ArgumentNullException.ThrowIfNull(comparison);
            SortInternal<T, ComparisonComparer<T>>(keys, new(comparison));
        }
    }

    extension<T, TComparer>(Span<T> keys) where TComparer : IComparer<T>?
    {
        /// <summary>
        ///  Sorts the elements in the entire <see cref="Span{T}"/> using the specified <paramref name="comparer"/>.
        /// </summary>
        public void Sort(TComparer comparer) =>
            SortInternal<T, IComparerComparer<T>>(keys, new(comparer ?? (IComparer<T>)Comparer<T>.Default));
    }

    extension<TKey, TValue>(Span<TKey> keys)
    {
        /// <summary>
        ///  Sorts a pair of spans (one containing the keys and the other containing the corresponding items)
        ///  based on the keys in the first span using the <see cref="IComparable{T}"/> implementation of each
        ///  key.
        /// </summary>
        /// <exception cref="ArgumentException">
        ///  <paramref name="items"/> is shorter than <paramref name="keys"/>.
        /// </exception>
        public void Sort(Span<TValue> items)
        {
            ValidateItems(keys, items);
            SortInternal<TKey, TValue, ComparerComparer<TKey>>(keys, items, default);
        }

        /// <summary>
        ///  Sorts a pair of spans using the specified <paramref name="comparison"/>.
        /// </summary>
        public void Sort(Span<TValue> items, Comparison<TKey> comparison)
        {
            ArgumentNullException.ThrowIfNull(comparison);
            ValidateItems(keys, items);
            SortInternal<TKey, TValue, ComparisonComparer<TKey>>(keys, items, new(comparison));
        }
    }

    extension<TKey, TValue, TComparer>(Span<TKey> keys) where TComparer : IComparer<TKey>?
    {
        /// <summary>
        ///  Sorts a pair of spans using the specified <paramref name="comparer"/>.
        /// </summary>
        public void Sort(Span<TValue> items, TComparer comparer)
        {
            ValidateItems(keys, items);
            SortInternal<TKey, TValue, IComparerComparer<TKey>>(
                keys,
                items,
                new(comparer ?? (IComparer<TKey>)Comparer<TKey>.Default));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateItems<TKey, TValue>(Span<TKey> keys, Span<TValue> items)
    {
        if (items.Length < keys.Length)
        {
            throw new ArgumentException("Items span is shorter than the keys span.", nameof(items));
        }
    }

    // -----------------------------------------------------------------------
    //  Comparer abstractions (struct types so the JIT can inline Compare)
    // -----------------------------------------------------------------------

    private interface IComparerImpl<T>
    {
        int Compare(T x, T y);
    }

    private readonly struct ComparerComparer<T> : IComparerImpl<T>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y) => Comparer<T>.Default.Compare(x, y);
    }

    private readonly struct ComparisonComparer<T>(Comparison<T> comparison) : IComparerImpl<T>
    {
        private readonly Comparison<T> _comparison = comparison;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y) => _comparison(x, y);
    }

    private readonly struct IComparerComparer<T>(IComparer<T> comparer) : IComparerImpl<T>
    {
        private readonly IComparer<T> _comparer = comparer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(T x, T y) => _comparer.Compare(x, y);
    }

    // -----------------------------------------------------------------------
    //  Single-span IntroSort
    // -----------------------------------------------------------------------

    private static void SortInternal<T, TComparer>(Span<T> keys, TComparer comparer) where TComparer : struct, IComparerImpl<T>
    {
        if (keys.Length < 2)
        {
            return;
        }

        IntroSort(keys, 2 * (BitOperationsLog2(keys.Length) + 1), comparer);
    }

    private static int BitOperationsLog2(int value)
    {
        // Floor(log2(value)). value >= 1.
        int result = 0;
        while (value > 1)
        {
            value >>= 1;
            result++;
        }

        return result;
    }

    private const int IntrosortSizeThreshold = 16;

    private static void IntroSort<T, TComparer>(Span<T> keys, int depthLimit, TComparer comparer)
        where TComparer : struct, IComparerImpl<T>
    {
        while (keys.Length > 1)
        {
            int len = keys.Length;
            if (len <= IntrosortSizeThreshold)
            {
                if (len == 2)
                {
                    SwapIfGreater(keys, comparer, 0, 1);
                    return;
                }

                if (len == 3)
                {
                    SwapIfGreater(keys, comparer, 0, 1);
                    SwapIfGreater(keys, comparer, 0, 2);
                    SwapIfGreater(keys, comparer, 1, 2);
                    return;
                }

                InsertionSort(keys, comparer);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSort(keys, comparer);
                return;
            }

            depthLimit--;

            int p = PickPivotAndPartition(keys, comparer);
            IntroSort(keys[(p + 1)..], depthLimit, comparer);
            keys = keys[..p];
        }
    }

    private static int PickPivotAndPartition<T, TComparer>(Span<T> keys, TComparer comparer)
        where TComparer : struct, IComparerImpl<T>
    {
        int hi = keys.Length - 1;
        int middle = hi >> 1;

        // Sort lo, mid, hi.
        SwapIfGreater(keys, comparer, 0, middle);
        SwapIfGreater(keys, comparer, 0, hi);
        SwapIfGreater(keys, comparer, middle, hi);

        T pivot = keys[middle];
        Swap(keys, middle, hi - 1);
        int left = 0;
        int right = hi - 1;

        while (left < right)
        {
            while (comparer.Compare(keys[++left], pivot) < 0)
            {
            }

            while (comparer.Compare(pivot, keys[--right]) < 0)
            {
            }

            if (left >= right)
            {
                break;
            }

            Swap(keys, left, right);
        }

        if (left != hi - 1)
        {
            Swap(keys, left, hi - 1);
        }

        return left;
    }

    private static void HeapSort<T, TComparer>(Span<T> keys, TComparer comparer)
        where TComparer : struct, IComparerImpl<T>
    {
        int n = keys.Length;
        for (int i = n >> 1; i >= 1; i--)
        {
            DownHeap(keys, i, n, comparer);
        }

        for (int i = n; i > 1; i--)
        {
            Swap(keys, 0, i - 1);
            DownHeap(keys, 1, i - 1, comparer);
        }
    }

    private static void DownHeap<T, TComparer>(Span<T> keys, int i, int n, TComparer comparer)
        where TComparer : struct, IComparerImpl<T>
    {
        T d = keys[i - 1];
        while (i <= n >> 1)
        {
            int child = 2 * i;
            if (child < n && comparer.Compare(keys[child - 1], keys[child]) < 0)
            {
                child++;
            }

            if (!(comparer.Compare(d, keys[child - 1]) < 0))
            {
                break;
            }

            keys[i - 1] = keys[child - 1];
            i = child;
        }

        keys[i - 1] = d;
    }

    private static void InsertionSort<T, TComparer>(Span<T> keys, TComparer comparer)
        where TComparer : struct, IComparerImpl<T>
    {
        for (int i = 0; i < keys.Length - 1; i++)
        {
            T t = keys[i + 1];
            int j = i;
            while (j >= 0 && comparer.Compare(t, keys[j]) < 0)
            {
                keys[j + 1] = keys[j];
                j--;
            }

            keys[j + 1] = t;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapIfGreater<T, TComparer>(Span<T> keys, TComparer comparer, int i, int j)
        where TComparer : struct, IComparerImpl<T>
    {
        if (comparer.Compare(keys[i], keys[j]) > 0)
        {
            Swap(keys, i, j);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap<T>(Span<T> span, int i, int j)
    {
        T temp = span[i];
        span[i] = span[j];
        span[j] = temp;
    }

    // -----------------------------------------------------------------------
    //  Paired-span IntroSort (sorts items in tandem with keys)
    // -----------------------------------------------------------------------

    private static void SortInternal<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> items, TComparer comparer)
        where TComparer : struct, IComparerImpl<TKey>
    {
        if (keys.Length < 2)
        {
            return;
        }

        IntroSort(keys, items, 2 * (BitOperationsLog2(keys.Length) + 1), comparer);
    }

    private static void IntroSort<TKey, TValue, TComparer>(
        Span<TKey> keys,
        Span<TValue> items,
        int depthLimit,
        TComparer comparer) where TComparer : struct, IComparerImpl<TKey>
    {
        while (keys.Length > 1)
        {
            int len = keys.Length;
            if (len <= IntrosortSizeThreshold)
            {
                if (len == 2)
                {
                    SwapIfGreaterWithItems(keys, items, comparer, 0, 1);
                    return;
                }

                if (len == 3)
                {
                    SwapIfGreaterWithItems(keys, items, comparer, 0, 1);
                    SwapIfGreaterWithItems(keys, items, comparer, 0, 2);
                    SwapIfGreaterWithItems(keys, items, comparer, 1, 2);
                    return;
                }

                InsertionSort(keys, items, comparer);
                return;
            }

            if (depthLimit == 0)
            {
                HeapSort(keys, items, comparer);
                return;
            }

            depthLimit--;

            int p = PickPivotAndPartition(keys, items, comparer);
            IntroSort(keys[(p + 1)..], items[(p + 1)..keys.Length], depthLimit, comparer);
            keys = keys[..p];
            items = items[..p];
        }
    }

    private static int PickPivotAndPartition<TKey, TValue, TComparer>(
        Span<TKey> keys,
        Span<TValue> items,
        TComparer comparer) where TComparer : struct, IComparerImpl<TKey>
    {
        int hi = keys.Length - 1;
        int middle = hi >> 1;

        SwapIfGreaterWithItems(keys, items, comparer, 0, middle);
        SwapIfGreaterWithItems(keys, items, comparer, 0, hi);
        SwapIfGreaterWithItems(keys, items, comparer, middle, hi);

        TKey pivot = keys[middle];
        Swap(keys, items, middle, hi - 1);
        int left = 0;
        int right = hi - 1;

        while (left < right)
        {
            while (comparer.Compare(keys[++left], pivot) < 0)
            {
            }

            while (comparer.Compare(pivot, keys[--right]) < 0)
            {
            }

            if (left >= right)
            {
                break;
            }

            Swap(keys, items, left, right);
        }

        if (left != hi - 1)
        {
            Swap(keys, items, left, hi - 1);
        }

        return left;
    }

    private static void HeapSort<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> items, TComparer comparer)
        where TComparer : struct, IComparerImpl<TKey>
    {
        int n = keys.Length;
        for (int i = n >> 1; i >= 1; i--)
        {
            DownHeap(keys, items, i, n, comparer);
        }

        for (int i = n; i > 1; i--)
        {
            Swap(keys, items, 0, i - 1);
            DownHeap(keys, items, 1, i - 1, comparer);
        }
    }

    private static void DownHeap<TKey, TValue, TComparer>(
        Span<TKey> keys,
        Span<TValue> items,
        int i,
        int n,
        TComparer comparer) where TComparer : struct, IComparerImpl<TKey>
    {
        TKey d = keys[i - 1];
        TValue dValue = items[i - 1];
        while (i <= n >> 1)
        {
            int child = 2 * i;
            if (child < n && comparer.Compare(keys[child - 1], keys[child]) < 0)
            {
                child++;
            }

            if (!(comparer.Compare(d, keys[child - 1]) < 0))
            {
                break;
            }

            keys[i - 1] = keys[child - 1];
            items[i - 1] = items[child - 1];
            i = child;
        }

        keys[i - 1] = d;
        items[i - 1] = dValue;
    }

    private static void InsertionSort<TKey, TValue, TComparer>(Span<TKey> keys, Span<TValue> items, TComparer comparer)
        where TComparer : struct, IComparerImpl<TKey>
    {
        for (int i = 0; i < keys.Length - 1; i++)
        {
            TKey t = keys[i + 1];
            TValue tValue = items[i + 1];
            int j = i;
            while (j >= 0 && comparer.Compare(t, keys[j]) < 0)
            {
                keys[j + 1] = keys[j];
                items[j + 1] = items[j];
                j--;
            }

            keys[j + 1] = t;
            items[j + 1] = tValue;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SwapIfGreaterWithItems<TKey, TValue, TComparer>(
        Span<TKey> keys,
        Span<TValue> items,
        TComparer comparer,
        int i,
        int j) where TComparer : struct, IComparerImpl<TKey>
    {
        if (comparer.Compare(keys[i], keys[j]) > 0)
        {
            Swap(keys, items, i, j);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Swap<TKey, TValue>(Span<TKey> keys, Span<TValue> items, int i, int j)
    {
        TKey tk = keys[i];
        keys[i] = keys[j];
        keys[j] = tk;
        TValue tv = items[i];
        items[i] = items[j];
        items[j] = tv;
    }
}
