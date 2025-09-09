// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

/// <summary>
///  Base list implementation that uses an array as its backing storage. Does not allow nulls.
/// </summary>
public abstract class ArrayBackedList<T> : ContiguousList<T> where T : notnull
{
    private T[] _items = [];
    private int _count;

    /// <summary>
    ///  Constructs a new instance of the <see cref="ArrayBackedList{T}"/> class with an initial array.
    /// </summary>
    /// <param name="backingArray">
    ///  The initial backing array to use. This array should not be <see langword="null"/> but can be empty.
    /// </param>
    protected ArrayBackedList(T[] backingArray)
    {
        ArgumentNull.ThrowIfNull(backingArray);
        _items = backingArray;
    }

    /// <inheritdoc/>
    public override int Count => _count;

    /// <summary>
    ///  Returns <see langword="true"/> if the list is empty, otherwise <see langword="false"/>.
    /// </summary>
    public bool Empty => _count == 0;

    /// <inheritdoc/>
    public override void Clear()
    {
        if (_count > 0 && TypeInfo<T>.IsReferenceOrContainsReferences())
        {
            Array.Clear(_items, 0, _count);
        }

        _count = 0;
    }

    /// <inheritdoc/>
    public override int IndexOf(T item) => _count == 0 ? -1 : Array.IndexOf(_items, item, 0, _count);

    /// <inheritdoc/>
    public override void Add(T item)
    {
        ArgumentNull.ThrowIfNull(item);

        if (_count == _items.Length)
        {
            EnsureCapacity(_count + 1);
        }

        _items[_count++] = item;
    }

    /// <inheritdoc/>
    public override void Insert(int index, T item)
    {
        ArgumentOutOfRange.ThrowIfNegative(index);
        ArgumentOutOfRange.ThrowIfGreaterThan(index, _count);
        ArgumentNull.ThrowIfNull(item);

        if (_count == _items.Length)
        {
            EnsureCapacity(_count + 1);
        }

        if (index < _count)
        {
            Array.Copy(_items, index, _items, index + 1, _count - index);
        }

        _items[index] = item;
        _count++;
    }

    /// <inheritdoc/>
    public override void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _count--;
        if (index < _count)
        {
            Array.Copy(_items, index + 1, _items, index, _count - index);
        }

        // Clear the last element to avoid potential memory leaks
        _items[_count] = default!;
    }

    /// <inheritdoc/>
    public override int RemoveAll(Predicate<T> match)
    {
        ArgumentNull.ThrowIfNull(match);

        int freeIndex = 0;   // the first free slot in items array

        // Find the first item which needs to be removed.
        while (freeIndex < _count && !match(_items[freeIndex]))
        {
            freeIndex++;
        }

        if (freeIndex >= _count)
        {
            return 0;
        }

        int current = freeIndex + 1;
        while (current < _count)
        {
            // Find the first item which needs to be kept.
            while (current < _count && match(_items[current]))
            {
                current++;
            }

            if (current < _count)
            {
                // Copy item to the free slot.
                _items[freeIndex++] = _items[current++];
            }
        }

        if (TypeInfo<T>.IsReferenceOrContainsReferences())
        {
            // Clear the elements so that the gc can reclaim the references.
            Array.Clear(_items, freeIndex, _count - freeIndex);
        }

        int result = _count - freeIndex;
        _count = freeIndex;
        return result;
    }

    /// <summary>
    ///  Ensures that the list can hold at least the specified number of elements.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <returns>The new capacity of the list.</returns>
    public int EnsureCapacity(int capacity)
    {
        ArgumentOutOfRange.ThrowIfNegativeOrZero(capacity);

        return capacity <= _items.Length
            ? _items.Length
            : Grow(capacity);
    }

    private int Grow(int capacity)
    {
        int newCapacity = checked(_items.Length * 2);
        if (newCapacity < capacity)
        {
            newCapacity = capacity;
        }

        T[] newArray = GetNewArray(newCapacity);

        if (_count > 0)
        {
            Array.Copy(_items, 0, newArray, 0, _count);
        }

        if (_items.Length > 0)
        {
            ReturnArrayInternal(_items);
        }

        _items = newArray;
        return _items.Length;
    }

    /// <inheritdoc/>
    public override T this[int index]
    {
        get
        {
            ArgumentOutOfRange.ThrowIfNegative(index);
            ArgumentOutOfRange.ThrowIfGreaterThanOrEqual(index, _count);
            return _items[index];
        }
        set
        {
            ArgumentOutOfRange.ThrowIfNegative(index);
            ArgumentOutOfRange.ThrowIfGreaterThanOrEqual(index, _count);
            _items[index] = value;
        }
    }

    /// <summary>
    ///  Override to provide the logic for obtaining the needed backing array.
    /// </summary>
    /// <param name="minimumCapacity">The array needs to be at leas this size.</param>
    protected abstract T[] GetNewArray(int minimumCapacity);

    private void ReturnArrayInternal(T[] array)
    {
        if (Enumerating)
        {
            ThrowHelper.ThrowInvalidOperation("Cannot return array while enumerating.");
        }

        ReturnArray(array);
    }

    /// <summary>
    ///  Override to provide the logic for dealing with an array that is no longer needed
    /// </summary>
    /// <param name="array">The array to return. May be empty.</param>
    protected abstract void ReturnArray(T[] array);

    /// <summary>
    ///  Copies all elements of the list to the specified array.
    /// </summary>
    /// <param name="array">The array to copy to.</param>
    public void CopyTo(T[] array) => CopyTo(array, 0);

    /// <inheritdoc/>
    public override void CopyTo(T[] array, int arrayIndex)
    {
        // Delegate error checking to Array.Copy.
        Array.Copy(_items, 0, array, arrayIndex, _count);
    }

    /// <inheritdoc/>
    public override void CopyTo(Array array, int index)
    {
        if ((array is not null) && (array.Rank != 1))
        {
            ThrowHelper.ThrowArgument(nameof(array), "Multidimensional arrays are not supported.");
        }

        // Delegate other error checking to Array.Copy.
        Array.Copy(_items, 0, array!, index, _count);
    }

    /// <summary>
    ///  Copies a range of elements from the list to the specified array.
    /// </summary>
    /// <param name="index">The index in the list at which copying begins.</param>
    /// <param name="array">The array to copy to.</param>
    /// <param name="arrayIndex">The index in the array at which copying begins.</param>
    /// <param name="count">The number of elements to copy.</param>
    public void CopyTo(int index, T[] array, int arrayIndex, int count)
    {
        // Delegate error checking to Array.Copy.
        Array.Copy(_items, index, array, arrayIndex, count);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReturnArrayInternal(_items);
            _items = [];
            _count = 0;
        }
    }

    /// <inheritdoc/>
    public override Span<T> UnsafeValues => new(_items, 0, _count);

    /// <inheritdoc/>
    public override ReadOnlySpan<T> Values => new(_items, 0, _count);
}
