// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Io;

internal ref struct ReversePathSegmentEnumerator
{
    private readonly ReadOnlySpan<char> _firstPath;
    private readonly ReadOnlySpan<char> _secondPath;
    private ReadOnlySpan<char> _currentSegment;
    private int _position;
    private bool _inSecondPath;

    public ReversePathSegmentEnumerator(
        ReadOnlySpan<char> firstPath,
        ReadOnlySpan<char> secondPath)
    {
        _firstPath = firstPath;
        _secondPath = secondPath;
        _inSecondPath = !_secondPath.IsEmpty;
        _position = _inSecondPath ? _secondPath.Length : _firstPath.Length;
    }

    public readonly ReadOnlySpan<char> Current => _currentSegment;

    public bool MovePrevious()
    {
        while (true)
        {
            ReadOnlySpan<char> path = _inSecondPath ? _secondPath : _firstPath;
            int end = _position;
            while (end > 0 && path[end - 1] == Path.DirectorySeparatorChar)
            {
                end--;
            }

            if (end == 0)
            {
                if (_inSecondPath)
                {
                    _inSecondPath = false;
                    _position = _firstPath.Length;
                    continue;
                }

                _currentSegment = default;
                return false;
            }

            int separatorIndex = path[..end].LastIndexOf(Path.DirectorySeparatorChar);
            int start = separatorIndex + 1;
            _currentSegment = path[start..end];
            _position = separatorIndex < 0 ? 0 : separatorIndex;
            return true;
        }
    }
}