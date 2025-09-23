// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Collections;

/// <summary>
///  Empty, read-only list.
/// </summary>
public sealed class EmptyList<T> : ContiguousList<T> where T : notnull
{
    private EmptyList()
    {
    }

    /// <summary>
    ///  Gets the singleton instance of the <see cref="EmptyList{T}"/> class.
    /// </summary>
    public static EmptyList<T> Instance { get; } = new();

    /// <inheritdoc/>
    public override T this[int index]
    {
        get => throw new ArgumentOutOfRangeException(nameof(index));
        set => throw new ArgumentOutOfRangeException(nameof(index));
    }

    /// <inheritdoc/>
    public override bool IsReadOnly => true;

    /// <inheritdoc/>
    public override Span<T> UnsafeValues => [];

    /// <inheritdoc/>
    public override ReadOnlySpan<T> Values => [];

    /// <inheritdoc/>
    public override int Count => 0;

    /// <inheritdoc/>
    public override void Add(T item) => throw new NotImplementedException();

    /// <inheritdoc/>
    public override void Clear() { }

    /// <inheritdoc/>
    public override void CopyTo(T[] array, int arrayIndex) => ArgumentOutOfRangeException.ThrowIfNotEqual(arrayIndex, 0);

    /// <inheritdoc/>
    public override void CopyTo(Array array, int index) => ArgumentOutOfRangeException.ThrowIfNotEqual(index, 0);

    /// <inheritdoc/>
    public override int IndexOf(T item) => -1;

    /// <inheritdoc/>
    public override void Insert(int index, T item) => throw new InvalidOperationException();

    /// <inheritdoc/>
    public override void RemoveAt(int index) => throw new InvalidOperationException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) { }
}
