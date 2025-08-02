// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Numerics;
using Touki.Framework.Resources;
using Touki;

namespace System.Runtime.InteropServices;

/// <summary>This class contains methods that are mainly used to manage native memory.</summary>
public static unsafe partial class NativeMemory
{
    private const string Ucrtbase = "ucrtbase.dll";

    /// <summary>Allocates a block of memory of the specified size, in elements.</summary>
    /// <param name="elementCount">The count, in elements, of the block to allocate.</param>
    /// <param name="elementSize">The size, in bytes, of each element in the allocation.</param>
    /// <returns>A pointer to the allocated block of memory.</returns>
    /// <exception cref="OutOfMemoryException">Allocating <paramref name="elementCount" /> * <paramref name="elementSize" /> bytes of memory failed.</exception>
    /// <remarks>
    ///  <para>This method allows <paramref name="elementCount" /> and/or <paramref name="elementSize" /> to be <c>0</c> and will return a valid pointer that should not be dereferenced and that should be passed to free to avoid memory leaks.</para>
    ///  <para>This method is a thin wrapper over the C <c>malloc</c> API.</para>
    /// </remarks>
    public static void* Alloc(nuint elementCount, nuint elementSize) => Alloc(checked(elementCount * elementSize));

    /// <summary>Allocates an aligned block of memory of the specified size and alignment, in bytes.</summary>
    /// <param name="byteCount">The size, in bytes, of the block to allocate.</param>
    /// <param name="alignment">The alignment, in bytes, of the block to allocate. This must be a power of <c>2</c>.</param>
    /// <returns>A pointer to the allocated aligned block of memory.</returns>
    /// <exception cref="ArgumentException"><paramref name="alignment" /> is not a power of two.</exception>
    /// <exception cref="OutOfMemoryException">Allocating <paramref name="byteCount" /> of memory with <paramref name="alignment" /> failed.</exception>
    /// <remarks>
    ///  <para>This method allows <paramref name="byteCount" /> to be <c>0</c> and will return a valid pointer that should not be dereferenced and that should be passed to free to avoid memory leaks.</para>
    ///  <para>This method is a thin wrapper over the C <c>aligned_alloc</c> API or a platform dependent aligned allocation API such as <c>_aligned_malloc</c> on Win32.</para>
    ///  <para>This method is not compatible with <see cref="Free" /> or <see cref="Realloc" />, instead <see cref="AlignedFree" /> or <see cref="AlignedRealloc" /> should be called.</para>
    /// </remarks>
    public static void* AlignedAlloc(nuint byteCount, nuint alignment)
    {
        if (!BitOperations.IsPow2(alignment))
        {
            // The C standard doesn't define what a valid alignment is, however Windows and POSIX The Windows implementation requires a power of 2
            ThrowHelper.ThrowArgument(SRF.Argument_AlignmentMustBePow2);
        }

        // Unlike the C standard and POSIX, Windows does not requires size to be a multiple of alignment. However, we do want an "empty" allocation for zero
        void* result = _aligned_malloc((byteCount != 0) ? byteCount : 1, alignment);

        if (result is null)
        {
            ThrowHelper.ThrowOutOfMemory();
        }

        return result;
    }

    /// <summary>Frees an aligned block of memory.</summary>
    /// <param name="ptr">A pointer to the aligned block of memory that should be freed.</param>
    /// <remarks>
    ///  <para>This method does nothing if <paramref name="ptr" /> is <see langword="null"/>.</para>
    ///  <para>This method is a thin wrapper over the C <c>free</c> API or a platform dependent aligned free API such as <c>_aligned_free</c> on Win32.</para>
    /// </remarks>
    public static void AlignedFree(void* ptr)
    {
        if (ptr is not null)
        {
            _aligned_free(ptr);
        }
    }

    /// <summary>Reallocates an aligned block of memory of the specified size and alignment, in bytes.</summary>
    /// <param name="ptr">The previously allocated block of memory.</param>
    /// <param name="byteCount">The size, in bytes, of the block to allocate.</param>
    /// <param name="alignment">The alignment, in bytes, of the block to allocate. This must be a power of <c>2</c>.</param>
    /// <returns>A pointer to the reallocated aligned block of memory.</returns>
    /// <exception cref="ArgumentException"><paramref name="alignment" /> is not a power of two.</exception>
    /// <exception cref="OutOfMemoryException">Reallocating <paramref name="byteCount" /> of memory with <paramref name="alignment" /> failed.</exception>
    /// <remarks>
    ///  <para>This method acts as <see cref="AlignedAlloc" /> if <paramref name="ptr" /> is <see langword="null"/>.</para>
    ///  <para>This method allows <paramref name="byteCount" /> to be <c>0</c> and will return a valid pointer that should not be dereferenced and that should be passed to free to avoid memory leaks.</para>
    ///  <para>This method is a platform dependent aligned reallocation API such as <c>_aligned_realloc</c> on Win32.</para>
    ///  <para>This method is not compatible with <see cref="Free" /> or <see cref="Realloc" />, instead <see cref="AlignedFree" /> or <see cref="AlignedRealloc" /> should be called.</para>
    /// </remarks>
    public static void* AlignedRealloc(void* ptr, nuint byteCount, nuint alignment)
    {
        if (!BitOperations.IsPow2(alignment))
        {
            // The C standard doesn't define what a valid alignment is, however Windows and POSIX The Windows implementation requires a power of 2
            ThrowHelper.ThrowArgument(SRF.Argument_AlignmentMustBePow2);
        }

        // Unlike the C standard and POSIX, Windows does not requires size to be a multiple of alignment. However, we do want an "empty" allocation for zero
        void* result = _aligned_realloc(ptr, (byteCount != 0) ? byteCount : 1, alignment);

        if (result is null)
        {
            ThrowHelper.ThrowOutOfMemory();
        }

        return result;
    }

    /// <summary>Allocates a block of memory of the specified size, in bytes.</summary>
    /// <param name="byteCount">The size, in bytes, of the block to allocate.</param>
    /// <returns>A pointer to the allocated block of memory.</returns>
    /// <exception cref="OutOfMemoryException">Allocating <paramref name="byteCount" /> of memory failed.</exception>
    /// <remarks>
    ///  <para>This method allows <paramref name="byteCount" /> to be <c>0</c> and will return a valid pointer that should not be dereferenced and that should be passed to free to avoid memory leaks.</para>
    ///  <para>This method is a thin wrapper over the C <c>malloc</c> API.</para>
    /// </remarks>
    public static void* Alloc(nuint byteCount)
    {
        // The Windows implementation handles size == 0 as we expect
        void* result = malloc(byteCount);

        if (result is null)
        {
            ThrowHelper.ThrowOutOfMemory();
        }

        return result;
    }

    /// <summary>Allocates and zeroes a block of memory of the specified size, in bytes.</summary>
    /// <param name="byteCount">The size, in bytes, of the block to allocate.</param>
    /// <returns>A pointer to the allocated and zeroed block of memory.</returns>
    /// <exception cref="OutOfMemoryException">Allocating <paramref name="byteCount" /> of memory failed.</exception>
    /// <remarks>
    ///  <para>This method allows <paramref name="byteCount" /> to be <c>0</c> and will return a valid pointer that should not be dereferenced and that should be passed to free to avoid memory leaks.</para>
    ///  <para>This method is a thin wrapper over the C <c>calloc</c> API.</para>
    /// </remarks>
    public static void* AllocZeroed(nuint byteCount)
    {
        return AllocZeroed(byteCount, elementSize: 1);
    }

    /// <summary>Allocates and zeroes a block of memory of the specified size, in elements.</summary>
    /// <param name="elementCount">The count, in elements, of the block to allocate.</param>
    /// <param name="elementSize">The size, in bytes, of each element in the allocation.</param>
    /// <returns>A pointer to the allocated and zeroed block of memory.</returns>
    /// <exception cref="OutOfMemoryException">Allocating <paramref name="elementCount" /> * <paramref name="elementSize" /> bytes of memory failed.</exception>
    /// <remarks>
    ///  <para>This method allows <paramref name="elementCount" /> and/or <paramref name="elementSize" /> to be <c>0</c> and will return a valid pointer that should not be dereferenced and that should be passed to free to avoid memory leaks.</para>
    ///  <para>This method is a thin wrapper over the C <c>calloc</c> API.</para>
    /// </remarks>
    public static void* AllocZeroed(nuint elementCount, nuint elementSize)
    {
        // The Windows implementation handles num == 0 && size == 0 as we expect
        void* result = calloc(elementCount, elementSize);

        if (result is null)
        {
            ThrowHelper.ThrowOutOfMemory();
        }

        return result;
    }

    /// <summary>Frees a block of memory.</summary>
    /// <param name="ptr">A pointer to the block of memory that should be freed.</param>
    /// <remarks>
    ///  <para>This method does nothing if <paramref name="ptr" /> is <see langword="null"/>.</para>
    ///  <para>This method is a thin wrapper over the C <c>free</c> API.</para>
    /// </remarks>
    public static void Free(void* ptr)
    {
        if (ptr is not null)
        {
            free(ptr);
        }
    }

    /// <summary>Reallocates a block of memory to be the specified size, in bytes.</summary>
    /// <param name="ptr">The previously allocated block of memory.</param>
    /// <param name="byteCount">The size, in bytes, of the reallocated block.</param>
    /// <returns>A pointer to the reallocated block of memory.</returns>
    /// <exception cref="OutOfMemoryException">Reallocating <paramref name="byteCount" /> of memory failed.</exception>
    /// <remarks>
    ///  <para>This method acts as <see cref="Alloc(nuint)" /> if <paramref name="ptr" /> is <see langword="null"/>.</para>
    ///  <para>This method allows <paramref name="byteCount" /> to be <c>0</c> and will return a valid pointer that should not be dereferenced and that should be passed to free to avoid memory leaks.</para>
    ///  <para>This method is a thin wrapper over the C <c>realloc</c> API.</para>
    /// </remarks>
    public static void* Realloc(void* ptr, nuint byteCount)
    {
        // The Windows implementation treats size == 0 as Free, we want an "empty" allocation
        void* result = realloc(ptr, (byteCount != 0) ? byteCount : 1);

        if (result is null)
        {
            ThrowHelper.ThrowOutOfMemory();
        }

        return result;
    }

    /// <summary>Clears a block of memory.</summary>
    /// <param name="ptr">A pointer to the block of memory that should be cleared.</param>
    /// <param name="byteCount">The size, in bytes, of the block to clear.</param>
    /// <remarks>
    ///  <para>If this method is called with <paramref name="ptr" /> being <see langword="null"/> and <paramref name="byteCount" /> being <c>0</c>, it will be equivalent to a no-op.</para>
    ///  <para>The behavior when <paramref name="ptr" /> is <see langword="null"/> and <paramref name="byteCount" /> is greater than <c>0</c> is undefined.</para>
    /// </remarks>
    public static void Clear(void* ptr, nuint byteCount)
    {
        memset(ptr, 0, byteCount);
    }

    /// <summary>
    ///  Copies a block of memory from memory location <paramref name="source"/>
    ///  to memory location <paramref name="destination"/>.
    /// </summary>
    /// <param name="source">A pointer to the source of data to be copied.</param>
    /// <param name="destination">A pointer to the destination memory block where the data is to be copied.</param>
    /// <param name="byteCount">The size, in bytes, to be copied from the source location to the destination.</param>
    public static void Copy(void* source, void* destination, nuint byteCount)
    {
        memmove(destination, source, byteCount);
    }

    /// <summary>
    ///  Copies the byte <paramref name="value"/> to the first <paramref name="byteCount"/> bytes
    ///  of the memory located at <paramref name="ptr"/>.
    /// </summary>
    /// <param name="ptr">A pointer to the block of memory to fill.</param>
    /// <param name="byteCount">The number of bytes to be set to <paramref name="value"/>.</param>
    /// <param name="value">The value to be set.</param>
    public static void Fill(void* ptr, nuint byteCount, byte value)
    {
        memset(ptr, value, byteCount);
    }

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void* _aligned_malloc(nuint size, nuint alignment);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void _aligned_free(void* ptr);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void* _aligned_realloc(void* ptr, nuint size, nuint alignment);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void* calloc(nuint num, nuint size);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void free(void* ptr);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void* malloc(nuint size);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void* realloc(void* ptr, nuint new_size);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void* memset(void* dest, int c, nuint count);

    [DllImport(Ucrtbase, CallingConvention = CallingConvention.Cdecl)]
    private static extern void* memmove(void* dest, void* src, nuint count);
}
