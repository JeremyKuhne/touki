// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki;

/// <summary>
///  Fast stack based <see cref="Span{T}"/> writer.
/// </summary>
public unsafe ref struct SpanWriter<T>(Span<T> span) where T : unmanaged
{
    private Span<T> _unwritten = span;

    /// <summary>
    ///  Gets the original span that the writer was created with.
    /// </summary>
    public Span<T> Span { get; } = span;

    /// <summary>
    ///  Gets or sets the current position of the writer within the span.
    /// </summary>
    /// <value>
    ///  The zero-based position of the writer. Setting this value repositions the writer.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when the value is negative or greater than <see cref="Length"/>.
    /// </exception>
    public int Position
    {
        readonly get => Span.Length - _unwritten.Length;
        set => _unwritten = Span[value..];
    }

    /// <summary>
    ///  Returns <see langword="true"/> if there is no remaining space in the span.
    /// </summary>
    public readonly bool End => _unwritten.IsEmpty;

    /// <summary>
    ///  Gets the total length of the original span.
    /// </summary>
    public readonly int Length => Span.Length;

    /// <summary>
    ///  Try to write the given value.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <returns><see langword="true"/> if the value was successfully written; otherwise, <see langword="false"/>.</returns>
    public bool TryWrite(T value)
    {
        bool success = false;

        if (!_unwritten.IsEmpty)
        {
            success = true;
            _unwritten[0] = value;
            UnsafeAdvance(1);
        }

        return success;
    }

    /// <summary>
    ///  Try to write the given values.
    /// </summary>
    /// <param name="values">The values to write.</param>
    /// <returns><see langword="true"/> if all values were successfully written; otherwise, <see langword="false"/>.</returns>
    public bool TryWrite(ReadOnlySpan<T> values)
    {
        bool success = false;

        if (_unwritten.Length >= values.Length)
        {
            success = true;
            values.CopyTo(_unwritten);
            UnsafeAdvance(values.Length);
        }

        return success;
    }

    /// <summary>
    ///  Try to write the given value <paramref name="count"/> times.
    /// </summary>
    /// <param name="count">The number of times to write the value.</param>
    /// <param name="value">The value to write.</param>
    /// <returns><see langword="true"/> if all values were successfully written; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public bool TryWrite(int count, T value)
    {
        bool success = false;

        if (_unwritten.Length >= count)
        {
            success = true;
            _unwritten[..count].Fill(value);
            UnsafeAdvance(count);
        }

        return success;
    }

    /// <summary>
    ///  Advance the writer by the given <paramref name="count"/>.
    /// </summary>
    /// <param name="count">The number of positions to advance.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="count"/> is negative or would advance past the end of the span.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count) => _unwritten = _unwritten[count..];

    /// <summary>
    ///  Rewind the writer by the given <paramref name="count"/>.
    /// </summary>
    /// <param name="count">The number of positions to rewind.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///  Thrown when <paramref name="count"/> is negative or would rewind past the beginning of the span.
    /// </exception>
    public void Rewind(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        _unwritten = Span[(Span.Length - _unwritten.Length - count)..];
    }

    /// <summary>
    ///  Reset the writer to the beginning of the span.
    /// </summary>
    public void Reset() => _unwritten = Span;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UnsafeAdvance(int count)
    {
        Debug.Assert((uint)count <= (uint)_unwritten.Length);
        UncheckedSlice(ref _unwritten, count, _unwritten.Length - count);
    }

    /*
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UncheckedSliceTo(ref Span<T> span, int length)
    {
        Debug.Assert((uint)length <= (uint)span.Length);
        span = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(span), length);
    }
    */

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UncheckedSlice(ref Span<T> span, int start, int length)
    {
        Debug.Assert((uint)start <= (uint)span.Length && (uint)length <= (uint)(span.Length - start));
#if NETFRAMEWORK
        span = span.Slice(start, length);
#else
        span = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint)(uint)start), length);
#endif
    }
}
