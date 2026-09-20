// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.IO.MemoryMappedFiles;
using System.Threading;

namespace Touki.Io;

/// <summary>
///  Owns an acquired memory-mapped view pointer and releases it during disposal or finalization.
/// </summary>
internal sealed unsafe class MappedViewLease : DisposableBase.Finalizable
{
    private MemoryMappedViewAccessor? _accessor;

    /// <summary>
    ///  Initializes a lease that owns <paramref name="accessor"/> and its acquired pointer.
    /// </summary>
    /// <param name="accessor">The mapped view to own.</param>
    internal MappedViewLease(MemoryMappedViewAccessor accessor)
    {
        byte* pointer = null;
        try
        {
            long pointerOffset = accessor.PointerOffset;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            _accessor = accessor;
            Pointer = pointer + pointerOffset;
        }
        catch
        {
            accessor.Dispose();
            throw;
        }
    }

    /// <summary>
    ///  The adjusted pointer to the first mapped byte.
    /// </summary>
    internal byte* Pointer { get; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeCore();
            return;
        }

        try
        {
            DisposeCore();
        }
        catch
        {
        }
    }

    private void DisposeCore()
    {
        if (Interlocked.Exchange(ref _accessor, value: null) is not { } accessor)
        {
            return;
        }

        try
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        }
        finally
        {
            accessor.Dispose();
        }
    }
}