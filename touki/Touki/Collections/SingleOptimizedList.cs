// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

/// <summary>
///  List implementation for scenarios where you frequently have a single item in the list. Grows to an
///  ArrayPool backed list when more than one item is added.
/// </summary>
/// <typeparam name="TItem">The type of item in the list.</typeparam>
/// <typeparam name="TList">The type of list to use as the backing store when more than one item is added.</typeparam>
/// <remarks>
///  <para>
///   Make sure to dispose this class when you are done with it to avoid leaking the backing list.
///  </para>
/// </remarks>
public sealed class SingleOptimizedList<TItem, TList> : ContiguousList<TItem>
    where TItem : notnull
    where TList : ContiguousList<TItem>, new()
{
    // Once the backing list has been created, always use it.

    private bool _hasItem;
    private TItem _item;
    private TList? _backingList;

    /// <summary>
    ///  Constructs a new instance of the <see cref="SingleOptimizedList{TItem, TList}"/> class.
    /// </summary>
    public SingleOptimizedList()
    {
        _hasItem = false;
        _item = default!;
        _backingList = null;
    }

    /// <inheritdoc/>
    public override TItem this[int index]
    {
        get
        {
            if (!_hasItem)
            {
                ArgumentOutOfRangeException.Throw(nameof(index));
            }

            if (_backingList is { } list)
            {
                return list[index];
            }

            ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);
            return _item;
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (!_hasItem)
            {
                ArgumentOutOfRangeException.Throw(nameof(index));
            }

            if (_backingList is { } list)
            {
                list[index] = value;
                return;
            }

            ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);
            _item = value;
        }
    }

    /// <inheritdoc/>
    public override int Count
    {
        get
        {
            int count = _backingList?.Count ?? 0;
            return count == 0 && _hasItem ? 1 : count;
        }
    }

    /// <inheritdoc/>
    public override void Add(TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!_hasItem && _backingList is null)
        {
            _item = item;
            _hasItem = true;
            return;
        }

        if (_backingList is null)
        {
            // If we have one item and no backing list, create a new backing list
            _backingList = [];
            _backingList.Add(_item);
            _item = default!;
        }

        _hasItem = true;
        _backingList.Add(item);
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        _hasItem = false;
        _item = default!;
        _backingList?.Clear();
    }

    /// <inheritdoc/>
    public override void CopyTo(TItem[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("Destination array is not long enough to copy all the items in the collection.");
        }

        if (!_hasItem)
        {
            Debug.Assert(Count == 0);
            return;
        }

        if (_backingList is null)
        {
            array[arrayIndex] = _item;
            return;
        }

        _backingList.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public override void CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(index);

        if (array.Length - index < Count)
        {
            throw new ArgumentException("Destination array is not long enough to copy all the items in the collection.");
        }

        if (!_hasItem)
        {
            Debug.Assert(Count == 0);
            return;
        }

        if (_backingList is null)
        {
            array.SetValue(_item, index);
            return;
        }

        _backingList.CopyTo(array, index);
    }

    /// <inheritdoc/>
    public override int IndexOf(TItem item)
    {
        if (!_hasItem)
        {
            return -1;
        }

        return _backingList is { } list && list.Count > 0
            ? list.IndexOf(item)
            : _item.Equals(item) ? 0 : -1;
    }

    /// <inheritdoc/>
    public override void Insert(int index, TItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)index, (uint)Count);

        if (!_hasItem)
        {
            // No items, just add it.
            Debug.Assert(index == 0);
            Add(item);
            return;
        }

        if (_backingList is { } list)
        {
            Debug.Assert(list.Count != 0);
            list.Insert(index, item);
            return;
        }

        Debug.Assert(index is 0 or 1);
        _backingList = [];
        if (index == 0)
        {
            _backingList.Add(item);
            _backingList.Add(_item);
        }
        else
        {
            _backingList.Add(_item);
            _backingList.Add(item);
        }

        _item = default!;
    }

    /// <inheritdoc/>
    public override void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)Count);

        if (_backingList is { } list)
        {
            Debug.Assert(list.Count != 0);
            list.RemoveAt(index);
            return;
        }

        _hasItem = false;
        _item = default!;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backingList?.Dispose();
        }
    }

    /// <inheritdoc/>
    public override ReadOnlySpan<TItem> Values
    {
        get
        {
            if (!_hasItem)
            {
                return [];
            }

            if (_backingList is null)
            {
#if NET
                return MemoryMarshal.CreateReadOnlySpan(ref _item, 1);
#else
                _backingList = [];
                _backingList.Add(_item);
                _item = default!;
#endif
            }

            return _backingList.Values;
        }
    }

    /// <inheritdoc/>
    public override Span<TItem> UnsafeValues
    {
        get
        {
            if (!_hasItem)
            {
                return [];
            }

            if (_backingList is null)
            {
#if NET
                return MemoryMarshal.CreateSpan(ref _item, 1);
#else
                _backingList = [];
                _backingList.Add(_item);
                _item = default!;
#endif
            }

            return _backingList.UnsafeValues;
        }
    }
}
