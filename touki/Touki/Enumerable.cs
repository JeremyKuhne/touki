// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki;

/// <summary>
///  Simple enumerable base class.
/// </summary>
public abstract class Enumerable<T> : DisposableBase, IEnumerable<T>, IEnumerator<T>
{
    /// <inheritdoc cref="IEnumerator.MoveNext"/>
    public abstract bool MoveNext();

    /// <inheritdoc cref="IEnumerator.Reset"/>
    public virtual void Reset() => throw new NotSupportedException();

    /// <inheritdoc cref="IEnumerator{T}.Current"/>
    public T Current { get; protected set; } = default!;
    object? IEnumerator.Current => Current;

    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator"/>
    public IEnumerator<T> GetEnumerator() => this;
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
