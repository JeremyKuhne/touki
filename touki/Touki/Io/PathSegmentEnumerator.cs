// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

/// <summary>
///  Allows walking segments of a path. Can combine two path segments into a virtual path to help avoid allocating.
/// </summary>
public ref struct PathSegmentEnumerator
{
    private readonly ReadOnlySpan<char> _firstPath;
    private readonly ReadOnlySpan<char> _secondPath;
    private ReadOnlySpan<char> _currentSegment;
    private int _position;
    private readonly bool _needsSeparator;

    /// <summary>
    ///  Constructs a virtual path from a single segment.
    /// </summary>
    public PathSegmentEnumerator(ReadOnlySpan<char> path) : this(path, [])
    {
    }

    /// <inheritdoc cref="PathSegmentEnumerator(ReadOnlySpan{char})"/>
    public PathSegmentEnumerator(string path) : this(path.AsSpan(), [])
    {
    }

    /// <inheritdoc cref="PathSegmentEnumerator(ReadOnlySpan{char}, ReadOnlySpan{char})"/>
    public PathSegmentEnumerator(string firstPath, string secondPath) : this(firstPath.AsSpan(), secondPath.AsSpan())
    {
    }

    /// <summary>
    ///  Constructs a virtual path from two segments.
    /// </summary>
    public PathSegmentEnumerator(ReadOnlySpan<char> firstPath, ReadOnlySpan<char> secondPath)
    {
        _firstPath = firstPath;
        _secondPath = secondPath;

        Length = _firstPath.Length + _secondPath.Length;
        _needsSeparator = _firstPath.Length != 0
            && _secondPath.Length != 0
            && (_firstPath[^1] != Path.DirectorySeparatorChar && _secondPath[0] != Path.DirectorySeparatorChar);

        if (_needsSeparator)
        {
            Length += 1;
        }
    }

    /// <summary>
    ///  Length of the virtual path.
    /// </summary>
    public int Length { get; private set; }

    /// <summary>
    ///  Gets the character at the given index.
    /// </summary>
    public readonly char this[int index]
    {
        get
        {
            if (index < _firstPath.Length)
            {
                return _firstPath[index];
            }

            if (_needsSeparator && index == _firstPath.Length)
            {
                return Path.DirectorySeparatorChar;
            }

            int secondPathIndex = index - _firstPath.Length;
            if (_needsSeparator)
            {
                secondPathIndex--;
            }

            return _secondPath[secondPathIndex];
        }
    }

    /// <summary>
    ///  Moves to the next segment (between <see cref="Path.DirectorySeparatorChar"/>)
    ///  in the virtual path, returns <see langword="false"/> if there are no more segments.
    /// </summary>
    public bool MoveNext()
    {
        // Total logical length assumes a separator between paths
        int totalLogicalLength = _firstPath.Length + _secondPath.Length;

        // If we've reached the end, there are no more segments
        if (_position >= totalLogicalLength)
        {
            _currentSegment = default;
            return false;
        }

        // Determine which span we're in and get the remaining portion
        ReadOnlySpan<char> remainingSpan;

        if (_position < _firstPath.Length)
        {
            // We're in the first path
            remainingSpan = _firstPath[_position..];
        }
        else
        {
            // We're in the second path
            int secondPathPosition = _position - _firstPath.Length;
            remainingSpan = _secondPath[secondPathPosition..];
        }

        // Find the next separator using IndexOf
        int separatorIndex = remainingSpan.IndexOf(Path.DirectorySeparatorChar);

        if (separatorIndex == 0)
        {
            // Found separator at start, skip it and try again
            _position++;
            return MoveNext();
        }

        if (separatorIndex == -1)
        {
            // No separator found, take the rest of the span
            _currentSegment = remainingSpan;
            _position += remainingSpan.Length;
        }
        else
        {
            // Take up to the separator
            _currentSegment = remainingSpan[..separatorIndex];
            _position += separatorIndex + 1; // +1 to skip the separator
        }

        return true;
    }

    /// <summary>
    ///  Returns the current path segment, if any.
    /// </summary>
    public readonly ReadOnlySpan<char> Current => _currentSegment;

    /// <summary>
    ///  <see langword="true"/> if there are no more segments to process.
    /// </summary>
    public readonly bool End => _position >= (_firstPath.Length + _secondPath.Length);
}
