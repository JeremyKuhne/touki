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
public abstract partial class ListBase<T> : DisposableBase, IList<T>, IReadOnlyList<T>, IList
    where T : notnull
{
    private int _enumerationCount;

    /// <summary>
    ///  Returns <see langword="true"/> if the list is currently being enumerated, otherwise <see langword="false"/>.
    /// </summary>
    protected bool Enumerating
    {
        get
        {
            Debug.Assert(_enumerationCount >= 0);
            return _enumerationCount > 0;
        }
    }

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
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <inheritdoc cref="Contains(T)"/>
    /// <inheritdoc cref="IndexOf(T, IEqualityComparer{T})"/>
    public bool Contains(T item, IEqualityComparer<T> comparer) => IndexOf(item, comparer) >= 0;

    /// <inheritdoc/>
    public abstract void CopyTo(T[] array, int arrayIndex);

    /// <inheritdoc/>
    public abstract int IndexOf(T item);

    /// <inheritdoc cref="IndexOf(T)"/>
    /// <param name="comparer">The comparer to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="comparer"/> is null.</exception>
    public virtual int IndexOf(T item, IEqualityComparer<T> comparer)
    {
        // .NET 10 is getting a number of extensions for IEqualityComparer for spans, which will
        // allow optimizing this method to use those extensions in derived classes.
        //
        // https://github.com/dotnet/runtime/issues/28934

        ArgumentNullException.ThrowIfNull(comparer);

        for (int i = 0; i < Count; i++)
        {
            if (comparer.Equals(this[i], item))
            {
                return i;
            }
        }

        return -1;
    }

    /// <inheritdoc/>
    public abstract void Insert(int index, T item);

    /// <inheritdoc/>
    public virtual bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index >= 0)
        {
            RemoveAt(index);
            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public abstract void RemoveAt(int index);

    /// <inheritdoc cref="List{T}.RemoveAll(Predicate{T})"/>
    public virtual int RemoveAll(Predicate<T> match)
    {
        ArgumentNullException.ThrowIfNull(match);
        int removedCount = 0;
        for (int i = Count - 1; i >= 0; i--)
        {
            if (match(this[i]))
            {
                RemoveAt(i);
                removedCount++;
            }
        }

        return removedCount;
    }

    /// <inheritdoc/>
    public abstract void CopyTo(Array array, int index);

    /// <inheritdoc/>
    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        Debug.Assert(
            !DebugOnly.CallerIsInToukiAssembly(),
            "We should be attempting to not use this method, but it is required for the interface.");

        return new Enumerator<T>(this);
    }

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public Enumerator GetEnumerator() => new Enumerator(this);

    #region Non generic interface methods
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
        ArgumentNullException.ThrowIfNull(value);

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
        ArgumentNullException.ThrowIfNull(value);

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

    IEnumerator IEnumerable.GetEnumerator()
    {
        Debug.Assert(
            !DebugOnly.CallerIsInToukiAssembly(),
            "We should be attempting to not use this method, but it is required for the interface.");

        return new Enumerator<T>(this);
    }

    bool IList.Contains(object? value) => value is T item && Contains(item);
    #endregion
}
