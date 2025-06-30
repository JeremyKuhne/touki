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
    private readonly ArraySegment<T> _segment;
    private int _index;
    private T _current;

    /// <summary>
    ///  Initializes a new instance of the <see cref="ArraySegmentEnumerator{T}"/> struct.
    /// </summary>
    /// <param name="segment">The segment to enumerate.</param>
    internal ArraySegmentEnumerator(ArraySegment<T> segment)
    {
        ArgumentNull.ThrowIfNull(segment.Array, nameof(segment));
        _segment = segment;
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
        if ((uint)_index < (uint)_segment.Count)
        {
            // .NET Framework doesn't have a public indexer method, would otherwise have to cast.
            _current = _segment.Array![_index + _segment.Offset];
            _index++;
            return true;
        }

        _index = _segment.Count + 1;
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
