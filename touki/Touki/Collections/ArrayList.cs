// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

/// <summary>
///  Simple array backed list implementation. Does not allow nulls.
/// </summary>
public sealed class ArrayList<T> : ArrayBackedList<T> where T : notnull
{
    private const int DefaultInitialCapacity = 4;

    /// <summary>
    ///  Constructs a new instance of the <see cref="ArrayList{T}"/> class with the default initial capacity.
    /// </summary>
    public ArrayList() : this(DefaultInitialCapacity)
    {
    }

    /// <summary>
    ///  Constructs a new instance of the <see cref="ArrayList{T}"/> class with the specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">The initial capacity of the list.</param>
    public ArrayList(int initialCapacity)
        : base(initialCapacity > 0 ? new T[initialCapacity] : [])
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
    }

    /// <inheritdoc/>
    protected override T[] GetNewArray(int minimumCapacity)
    {
#if NET
        return GC.AllocateUninitializedArray<T>(minimumCapacity);
#else
        return new T[minimumCapacity];
#endif
    }

    /// <inheritdoc/>
    protected override void ReturnArray(T[] array)
    {
        // Nothing to do, let the GC reclaim it.
    }
}
