// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Collections;

namespace Touki.Collections;

/// <summary>
///  Base struct for enumerators that can be used in an optimized `foreach` loop.
/// </summary>
public readonly ref struct ValueEnumerator<TEnumerator, TValue>
    where TEnumerator : struct, IEnumerator<TValue>, IDisposable
#if NET9_0_OR_GREATER
    // Needs .NET 9 or greater
    , allows ref struct
#endif
{
    // When TEnumerator is a value type, the JIT will devirtualize the calls to it (no boxing).
    private readonly TEnumerator _enumerator;

    /// <summary>
    ///  Constructs a new instance of the <see cref="ValueEnumerator{TEnumerator, TValue}"/> struct.
    /// </summary>
    public ValueEnumerator(TEnumerator enumerator)
    {
        _enumerator = enumerator;
    }

    /// <inheritdoc cref="IEnumerator{TValue}.Current"/>
    public readonly TValue Current => _enumerator.Current;

    /// <inheritdoc cref="IEnumerator.MoveNext()"/>
    public readonly bool MoveNext() => _enumerator.MoveNext();

    /// <inheritdoc cref="IEnumerator.Reset()"/>
    public void Reset() => _enumerator.Reset();
}
