// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki.Collections;

/// <summary>
///  Enumerates the elements of an <see cref="ArrayPoolList{T}"/>.
/// </summary>
public struct ArraySegmentEnumerator<T> : IEnumerator<T>
{
    // The segment is decomposed into its parts rather than stored as an ArraySegment<T> field. On .NET Framework
    // ArraySegment<T>.Array/Offset/Count are not readonly members, so accessing them through a readonly struct
    // field forces a defensive copy of the whole segment on every access - in the per-element MoveNext hot path.
    // Storing the array reference and two ints sidesteps that entirely (and is free on modern .NET as well).
    private readonly T[] _array;
    private readonly int _offset;
    private readonly int _count;
    private int _index;
    private T _current;

    /// <summary>
    ///  Initializes a new instance of the <see cref="ArraySegmentEnumerator{T}"/> struct.
    /// </summary>
    /// <param name="segment">The segment to enumerate.</param>
    internal ArraySegmentEnumerator(ArraySegment<T> segment)
    {
        ArgumentNullException.ThrowIfNull(segment.Array, nameof(segment));
        _array = segment.Array;
        _offset = segment.Offset;
        _count = segment.Count;
        _index = 0;
        _current = default!;
    }

    /// <summary>
    ///  Gets the element at the current position of the enumerator.
    /// </summary>
    public readonly T Current => _current;

    /// <inheritdoc/>
    readonly object? IEnumerator.Current => _current;

    /// <inheritdoc/>
    public readonly void Dispose() { }

    /// <summary>
    ///  Advances the enumerator to the next element of the array segment.
    /// </summary>
    /// <returns>
    ///  <see langword="true"/> if the enumerator was successfully advanced to the next element;
    ///  <see langword="false"/> if the enumerator has passed the end of the array segment.
    /// </returns>
    public bool MoveNext()
    {
        if ((uint)_index < (uint)_count)
        {
            _current = _array[_index + _offset];
            _index++;
            return true;
        }

        _index = _count + 1;
        _current = default!;
        return false;
    }

    /// <summary>
    ///  Sets the enumerator to its initial position, which is before the first element in the list.
    /// </summary>
    public void Reset()
    {
        _index = 0;
        _current = default!;
    }
}
