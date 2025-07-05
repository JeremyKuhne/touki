// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

/// <summary>
///  List implementation for scenarios when you frequently have a single item in the list. Grows to an
///  ArrayPool backed list when more than one item is added.
/// </summary>
/// <remarks>
///  <para>
///   Make sure to dispose this class when you are done with it to avoid leaking the backing list.
///  </para>
/// </remarks>
public sealed class SingleOptimizedList<T> : ListBase<T> where T : notnull
{
    private bool _hasItem;
    private T _item;
    private ArrayPoolList<T>? _backingList;

    /// <summary>
    ///  Constructs a new instance of the <see cref="SingleOptimizedList{T}"/> class.
    /// </summary>
    public SingleOptimizedList()
    {
        _hasItem = false;
        _item = default!;
        _backingList = null;
    }

    /// <inheritdoc/>
    public override T this[int index]
    {
        get
        {
            if (!_hasItem)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_hasItem && _backingList is null)
            {
                ArgumentOutOfRange.ThrowIfNotEqual(index, 0);
                return _item;
            }

            Debug.Assert(_backingList is not null);
            return _backingList![index];
        }
        set
        {
            if (!_hasItem)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (_hasItem && _backingList is null)
            {
                ArgumentOutOfRange.ThrowIfNotEqual(index, 0);
                _item = value;
                return;
            }

            Debug.Assert(_backingList is not null);
            _backingList![index] = value;
        }
    }

    /// <inheritdoc/>
    public override int Count => _backingList?.Count ?? (_hasItem ? 1 : 0);

    /// <inheritdoc/>
    public override void Add(T item)
    {
        if (!_hasItem)
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

        _backingList.Add(item);
    }

    /// <inheritdoc/>
    public override void Clear()
    {
        _hasItem = false;
        _item = default!;
        _backingList?.Dispose();
        _backingList = null;
    }

    /// <inheritdoc/>
    public override void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNull.ThrowIfNull(array);
        ArgumentOutOfRange.ThrowIfNegative(arrayIndex);

        if (array.Length - arrayIndex < Count)
        {
            throw new ArgumentException("Destination array is not long enough to copy all the items in the collection.");
        }

        if (_backingList is null && _hasItem)
        {
            array[arrayIndex] = _item;
            return;
        }

        _backingList?.CopyTo(array, arrayIndex);
    }

    /// <inheritdoc/>
    public override void CopyTo(Array array, int index)
    {
        ArgumentNull.ThrowIfNull(array);
        ArgumentOutOfRange.ThrowIfNegative(index);

        if (array.Length - index < Count)
        {
            throw new ArgumentException("Destination array is not long enough to copy all the items in the collection.");
        }

        if (_backingList is null && _hasItem)
        {
            array.SetValue(_item, index);
            return;
        }

        _backingList?.CopyTo(array, index);
    }

    /// <inheritdoc/>
    public override int IndexOf(T item) =>
        _hasItem && _backingList is null && _item.Equals(item) ? 0 : _backingList?.IndexOf(item) ?? -1;

    /// <inheritdoc/>
    public override void Insert(int index, T item)
    {
        ArgumentOutOfRange.ThrowIfLessThan(index, 0);

        // If we have no items at all
        if (!_hasItem && (_backingList is null || _backingList.Count == 0))
        {
            ArgumentOutOfRange.ThrowIfNotEqual(index, 0);
            _item = item;
            _hasItem = true;
            return;
        }

        // If we have just one item and no backing list
        if (_hasItem && _backingList is null)
        {
            ArgumentOutOfRange.ThrowIfGreaterThan(index, 1);

            _backingList = [];

            if (index == 0)
            {
                _backingList.Add(item);
                _backingList.Add(_item);
            }
            else // index == 1
            {
                _backingList.Add(_item);
                _backingList.Add(item);
            }

            return;
        }

        // We have a backing list
        Debug.Assert(_backingList is not null);
        _backingList!.Insert(index, item);
    }

    /// <inheritdoc/>
    public override void RemoveAt(int index)
    {
        ArgumentOutOfRange.ThrowIfNegative(index);

        // If we have just one item and no backing list
        if (_hasItem && _backingList is null)
        {
            ArgumentOutOfRange.ThrowIfNotEqual(index, 0);
            _hasItem = false;
            _item = default!;
            return;
        }

        // If we have a backing list
        if (_backingList is null || index >= _backingList.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _backingList.RemoveAt(index);

        // If we now have no items, mark as empty
        if (_backingList.Count == 0)
        {
            _backingList.Dispose();
            _backingList = null;
            _hasItem = false;
            _item = default!;
        }
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
    protected override IEnumerator<T> GetIEnumerableEnumerator()
    {
        if (_backingList is not null && _backingList.Count > 0)
        {
            foreach (T item in _backingList)
            {
                yield return item;
            }
        }
        else if (_hasItem)
        {
            yield return _item;
        }
    }
}
