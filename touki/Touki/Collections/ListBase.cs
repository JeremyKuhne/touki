// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki.Collections;

/// <summary>
///  Base class for implementing lists that do not allow null values.
/// </summary>
/// <remarks>
///  <para>
///   The goal here is to avoid boilerplate non generic code.
///  </para>
/// </remarks>
public abstract class ListBase<T> : DisposableBase, IList<T>, IReadOnlyList<T>, IList where T : notnull
{
    /// <inheritdoc/>
    public abstract T this[int index] { get; set; }

    /// <inheritdoc/>
    public abstract int Count { get; }

    /// <inheritdoc/>
    public virtual bool IsReadOnly => false;

    /// <inheritdoc/>
    public abstract void Add(T item);

    /// <inheritdoc/>
    public abstract void Clear();

    /// <inheritdoc/>
    public abstract bool Contains(T item);

    /// <inheritdoc/>
    public abstract void CopyTo(T[] array, int arrayIndex);

    /// <inheritdoc/>
    public abstract int IndexOf(T item);

    /// <inheritdoc/>
    public abstract void Insert(int index, T item);

    /// <inheritdoc/>
    public abstract bool Remove(T item);

    /// <inheritdoc/>
    public abstract void RemoveAt(int index);

    /// <inheritdoc/>
    public abstract void CopyTo(Array array, int index);

    object? IList.this[int index]
    {
        get => this[index];
        set
        {
            if (value is not T item)
            {
                throw new ArgumentException("Invalid item type.", nameof(value));
            }

            this[index] = item;
        }
    }

    int IList.Add(object? value)
    {
        ArgumentNull.ThrowIfNull(value);

        if (value is not T item)
        {
            throw new ArgumentException("Invalid item type.", nameof(value));
        }

        Add(item);
        return Count - 1;
    }

    int IList.IndexOf(object? value) => value is T item ? IndexOf(item) : -1;

    void IList.Insert(int index, object? value)
    {
        ArgumentNull.ThrowIfNull(value);

        if (value is not T item)
        {
            throw new ArgumentException("Invalid item type.", nameof(value));
        }

        Insert(index, item);
    }

    void IList.Remove(object? value)
    {
        if (value is T item)
        {
            Remove(item);
        }
    }

    bool IList.IsFixedSize => false;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => this;

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetIEnumerableEnumerator();

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    /// <remarks>
    ///  <para>
    ///   This is necessary to allow the derived classes to provide a pattern matched value type enumerator.
    ///  </para>
    /// </remarks>
    protected abstract IEnumerator<T> GetIEnumerableEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetIEnumerableEnumerator();

    bool IList.Contains(object? value) => value is T item && Contains(item);
}
