// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;
using System.Threading;

namespace Touki.Collections;

public abstract partial class ListBase<T> where T : notnull
{
    /// <summary>
    ///  Heap-based enumerator for iterating over the list.
    /// </summary>
    public class Enumerator<TItem> : IEnumerator<TItem>
        where TItem : notnull
    {
        private readonly ListBase<TItem> _list;
        private int _index;

        /// <summary>
        ///  Constructs a new instance of the <see cref="Enumerator{TItem}"/> class.
        /// </summary>
        /// <param name="list">The list to enumerate.</param>
        public Enumerator(ListBase<TItem> list)
        {
            _list = list;
            _index = -1;
            Interlocked.Increment(ref _list._enumerationCount);
        }

        /// <inheritdoc/>
        public TItem Current => _list[_index];

        /// <inheritdoc/>
        object? IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_index < _list.Count - 1)
            {
                _index++;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public void Reset() => _index = -1;

        /// <inheritdoc/>
        public void Dispose()
        {
            _index = -1;
            Interlocked.Decrement(ref _list._enumerationCount);
        }
    }
}
