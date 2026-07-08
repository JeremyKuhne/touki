// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.IO.MemoryMappedFiles;

namespace Touki.Io;

/// <summary>
///  A <see cref="MemoryManager{T}"/> that exposes a read-only memory-mapped view of a file as
///  <see cref="System.Memory{T}"/> / <see cref="ReadOnlyMemory{T}"/> without copying its contents.
/// </summary>
/// <remarks>
///  <para>
///   The mapped view has a fixed address for its lifetime, so <see cref="Pin"/> and <see cref="Unpin"/>
///   are trivial; <see cref="MemoryManager{T}"/> is only used to turn the native view pointer into a
///   <see cref="ReadOnlyMemory{T}"/>. The view keeps the mapping alive, so the file and mapping handles
///   are released as soon as the view exists; only the view is held for the life of this manager.
///  </para>
///  <para>
///   Dispose the manager - directly, or through the owner returned by
///   <see cref="MemoryManager{T}.Memory"/> - to unmap the view. Do not use the memory after disposing.
///  </para>
/// </remarks>
public sealed unsafe class MappedMemoryManager : MemoryManager<byte>
{
    private readonly MemoryMappedViewAccessor _accessor;
    private readonly byte* _pointer;
    private readonly int _length;
    private bool _disposed;

    private MappedMemoryManager(MemoryMappedViewAccessor accessor, int length)
    {
        _accessor = accessor;
        _length = length;

        byte* pointer = null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
        _pointer = pointer + accessor.PointerOffset;
    }

    /// <summary>
    ///  Creates a <see cref="MappedMemoryManager"/> over a read-only memory mapping of the file at
    ///  <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path of the file to map.</param>
    /// <returns>A manager that owns the mapping until it is disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">
    ///  The file is empty or larger than <see cref="int.MaxValue"/> bytes.
    /// </exception>
    public static MappedMemoryManager CreateFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        long length = stream.Length;
        if (length is <= 0 or > int.MaxValue)
        {
            throw new IOException($"'{path}' is empty or too large to memory-map.");
        }

        using MemoryMappedFile file = MemoryMappedFile.CreateFromFile(
            stream,
            mapName: null,
            capacity: 0,
            MemoryMappedFileAccess.Read,
            HandleInheritability.None,
            leaveOpen: true);
        MemoryMappedViewAccessor accessor = file.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
        try
        {
            return new MappedMemoryManager(accessor, (int)length);
        }
        catch
        {
            accessor.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public override Span<byte> GetSpan() => new(_pointer, _length);

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0) => new(_pointer + elementIndex);

    /// <inheritdoc/>
    public override void Unpin()
    {
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _accessor.Dispose();
    }
}
