// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;

namespace Touki.Collections;

/// <summary>
///  A list implementation that uses <see cref="ArrayPool{T}"/> for its backing storage. Does not allow nulls.
/// </summary>
/// <remarks>
///  <para>
///   Disposing will return the internal array to the shared array pool, allowing it to be reused.
///  </para>
/// </remarks>
public sealed class ArrayPoolList<T> : ArrayBackedList<T> where T : notnull
{
    private readonly int _minimumCapacity;

    private const int DefaultMinimumCapacity = 64;

    /// <inheritdoc cref="ArrayPoolList{T}(int)"/>
    public ArrayPoolList() : this(DefaultMinimumCapacity)
    {
    }

    /// <summary>
    ///  Initializes a new instance of the <see cref="ArrayPoolList{T}"/> class.
    /// </summary>
    /// <param name="minimumCapacity">The initial capacity of the list.</param>
    public ArrayPoolList(int minimumCapacity = DefaultMinimumCapacity)
        : base([])
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumCapacity);
        _minimumCapacity = minimumCapacity;
    }

    /// <inheritdoc/>
    protected override T[] GetNewArray(int minimumCapacity)
    {
        if (minimumCapacity <= 0)
        {
            minimumCapacity = _minimumCapacity;
        }

        return ArrayPool<T>.Shared.Rent(minimumCapacity);
    }

    /// <inheritdoc/>
    protected override void ReturnArray(T[] array)
    {
        if (array is not null)
        {
            ArrayPool<T>.Shared.Return(array, clearArray: TypeInfo<T>.IsReferenceOrContainsReferences());
        }
    }
}
