// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;
using System.Threading;

namespace Touki.Collections;

public abstract partial class ListBase<T> where T : notnull
{
    /// <summary>
    ///  Stack-based enumerator for iterating over the list.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Defined as a ref struct to help avoid accidental boxing.
    ///  </para>
    /// </remarks>
    public ref struct Enumerator
    {
        private readonly ListBase<T> _list;
        private int _index;

        /// <summary>
        ///  Constructs a new instance of the <see cref="Enumerator"/> struct.
        /// </summary>
        /// <param name="list">The list to enumerate.</param>
        public Enumerator(ListBase<T> list)
        {
            _list = list;
            _index = -1;
            Interlocked.Increment(ref _list._enumerationCount);
        }

        /// <inheritdoc cref="IEnumerator.Current"/>
        public readonly T Current => _list[_index];

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext()
        {
            if (_index < _list.Count - 1)
            {
                _index++;
                return true;
            }

            return false;
        }

        /// <inheritdoc cref="IEnumerator.Reset"/>
        public void Reset() => _index = -1;

        /// <inheritdoc cref="IDisposable.Dispose"/>
        public void Dispose()
        {
            _index = -1;
            Interlocked.Decrement(ref _list._enumerationCount);
        }
    }
}
