// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Original License Information:
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Touki.Text;

/// <summary>
///  ChunkEnumerator supports both the IEnumerable and IEnumerator pattern so foreach
///  works (see GetChunks). It needs to be public (so the compiler can use it
///  when building a foreach statement) but users typically don't use it explicitly.
/// </summary>
/// <remarks>
///  <para>
///   On .NET Core this type is nested in StringBuilder, which we cannot do. As such,
///   the full type name has to be different.
///  </para>
/// </remarks>
public struct ChunkEnumerator
{
    // Accessor type to get at the private fields of StringBuilder. We could use reflection, but
    // there would be significant overhead to call through the FieldInfo APIs. Using Unsafe.As
    // as on .NET Framework we don't expect the initial fields to change.
    private class StringBuilderAccessor
    {
#pragma warning disable IDE1006 // Naming Styles
#pragma warning disable CS0649 // Field is never assigned to
        internal char[] m_ChunkChars = [];           // The characters in this block
        internal StringBuilder? m_ChunkPrevious;     // Link to the block logically before this block
        internal int m_ChunkLength;                  // The index in m_ChunkChars that represent the end of the block
        internal int m_ChunkOffset;                  // The logial offset (sum of all characters in previous blocks)
        internal int m_MaxCapacity = 0;
#pragma warning restore IDE1006
#pragma warning restore CS0649
    }

    private readonly StringBuilder _firstChunk;  // The first Stringbuilder chunk (which is the end of the logical string)
    private StringBuilder? _currentChunk;        // The chunk that this enumerator is currently returning (Current).
    private readonly ManyChunkInfo? _manyChunks; // Only used for long string builders with many chunks (see constructor)

    /// <summary>
    ///  Implement IEnumerable.GetEnumerator() to return 'this' as the IEnumerator
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)] // Only here to make foreach work
    public readonly ChunkEnumerator GetEnumerator() { return this; }

    /// <summary>
    ///  Implements the IEnumerator pattern.
    /// </summary>
    public bool MoveNext()
    {
        if (_currentChunk == _firstChunk)
        {
            return false;
        }

        if (_manyChunks != null)
        {
            return _manyChunks.MoveNext(ref _currentChunk);
        }

        StringBuilder next = _firstChunk;
        while (Unsafe.As<StringBuilderAccessor>(next).m_ChunkPrevious is StringBuilder previous && previous != _currentChunk)
        {
            next = previous;
        }

        _currentChunk = next;
        return true;
    }

    /// <summary>
    ///  Implements the IEnumerator pattern.
    /// </summary>
    public readonly ReadOnlyMemory<char> Current
    {
        get
        {
            if (_currentChunk is null)
            {
                ThrowHelper.ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen();
            }

            var accessor = Unsafe.As<StringBuilderAccessor>(_currentChunk);

            return new ReadOnlyMemory<char>(accessor.m_ChunkChars, 0, accessor.m_ChunkLength);
        }
    }

    internal ChunkEnumerator(StringBuilder stringBuilder)
    {
        _firstChunk = stringBuilder;
        _currentChunk = null;   // MoveNext will find the last chunk if we do this.
        _manyChunks = null;

        // There is a performance-vs-allocation tradeoff. Because the chunks
        // are a linked list with each chunk pointing to its PREDECESSOR, walking
        // the list FORWARD is not efficient. If there are few chunks (< 8) we
        // simply scan from the start each time, and tolerate the N*N behavior.
        // However above this size, we allocate an array to hold reference to all
        // the chunks and we can be efficient for large N.
        int chunkCount = ChunkCount(stringBuilder);
        if (8 < chunkCount)
        {
            _manyChunks = new ManyChunkInfo(stringBuilder, chunkCount);
        }
    }

    private static int ChunkCount(StringBuilder? stringBuilder)
    {
        int ret = 0;
        while (stringBuilder is not null)
        {
            ret++;
            stringBuilder = Unsafe.As<StringBuilderAccessor>(stringBuilder).m_ChunkPrevious;
        }

        return ret;
    }

    /// <summary>
    ///  Used to hold all the chunks indexes when you have many chunks.
    /// </summary>
    private sealed class ManyChunkInfo
    {
        private readonly StringBuilder[] _chunks;    // These are in normal order (first chunk first)
        private int _chunkPos;

        public bool MoveNext(ref StringBuilder? current)
        {
            int pos = ++_chunkPos;
            if (_chunks.Length <= pos)
            {
                return false;
            }
            current = _chunks[pos];
            return true;
        }

        public ManyChunkInfo(StringBuilder stringBuilder, int chunkCount)
        {
            _chunks = new StringBuilder[chunkCount];
            while (0 <= --chunkCount)
            {
                Debug.Assert(stringBuilder is not null);
                _chunks[chunkCount] = stringBuilder!;
                stringBuilder = Unsafe.As<StringBuilderAccessor>(stringBuilder).m_ChunkPrevious!;
            }

            _chunkPos = -1;
        }
    }
}
