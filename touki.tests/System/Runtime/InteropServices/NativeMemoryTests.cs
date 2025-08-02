// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace System.Runtime.InteropServices;

public unsafe class NativeMemoryTests
{
    [Fact]
    public void Alloc_ZeroSize_ReturnsValidPointer()
    {
        void* ptr = NativeMemory.Alloc(0);
        (ptr is null).Should().BeFalse("Pointer should not be null");
        NativeMemory.Free(ptr);
    }

    [Fact]
    public void Alloc_ValidSize_ReturnsUsableMemory()
    {
        nuint size = 100;
        void* ptr = NativeMemory.Alloc(size);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Write to the memory to ensure it's usable
        byte* bytePtr = (byte*)ptr;
        for (nuint i = 0; i < size; i++)
        {
            bytePtr[i] = (byte)(i % 256);
        }

        // Verify the writes
        for (nuint i = 0; i < size; i++)
        {
            bytePtr[i].Should().Be((byte)(i % 256));
        }

        NativeMemory.Free(ptr);
    }

    [Fact]
    public void Alloc_ElementCountAndSize_ReturnsValidPointer()
    {
        nuint elementCount = 10;
        nuint elementSize = 4;
        void* ptr = NativeMemory.Alloc(elementCount, elementSize);
        (ptr is null).Should().BeFalse("Pointer should not be null");
        NativeMemory.Free(ptr);
    }

    [Fact]
    public void Alloc_ZeroElementCountOrSize_ReturnsValidPointer()
    {
        void* ptr1 = NativeMemory.Alloc(0, 4);
        (ptr1 is null).Should().BeFalse("Pointer should not be null");
        NativeMemory.Free(ptr1);

        void* ptr2 = NativeMemory.Alloc(4, 0);
        (ptr2 is null).Should().BeFalse("Pointer should not be null");
        NativeMemory.Free(ptr2);

        void* ptr3 = NativeMemory.Alloc(0, 0);
        (ptr3 is null).Should().BeFalse("Pointer should not be null");
        NativeMemory.Free(ptr3);
    }

    [Fact]
    public void AllocZeroed_ValidSize_ReturnsZeroedMemory()
    {
        nuint size = 100;
        void* ptr = NativeMemory.AllocZeroed(size);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Verify the memory is zeroed
        byte* bytePtr = (byte*)ptr;
        for (nuint i = 0; i < size; i++)
        {
            bytePtr[i].Should().Be(0);
        }

        NativeMemory.Free(ptr);
    }

    [Fact]
    public void AllocZeroed_ElementCountAndSize_ReturnsZeroedMemory()
    {
        nuint elementCount = 10;
        nuint elementSize = 4;
        void* ptr = NativeMemory.AllocZeroed(elementCount, elementSize);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Verify the memory is zeroed
        byte* bytePtr = (byte*)ptr;
        for (nuint i = 0; i < elementCount * elementSize; i++)
        {
            bytePtr[i].Should().Be(0);
        }

        NativeMemory.Free(ptr);
    }

    [Fact]
    public void Free_NullPointer_DoesNotThrow()
    {
        Action action = () => NativeMemory.Free(null);
        action.Should().NotThrow();
    }

    [Fact]
    public void Realloc_NullPointer_AllocatesNewMemory()
    {
        nuint size = 100;
        void* ptr = NativeMemory.Realloc(null, size);
        (ptr is null).Should().BeFalse("Pointer should not be null");
        NativeMemory.Free(ptr);
    }

    [Fact]
    public void Realloc_ValidPointer_PreservesData()
    {
        nuint initialSize = 10;
        nuint newSize = 20;

        void* ptr = NativeMemory.Alloc(initialSize);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Write initial data
        byte* bytePtr = (byte*)ptr;
        for (nuint i = 0; i < initialSize; i++)
        {
            bytePtr[i] = (byte)(i % 256);
        }

        // Reallocate
        void* newPtr = NativeMemory.Realloc(ptr, newSize);
        (newPtr is null).Should().BeFalse("Pointer should not be null");

        // Verify initial data is preserved
        byte* newBytePtr = (byte*)newPtr;
        for (nuint i = 0; i < initialSize; i++)
        {
            newBytePtr[i].Should().Be((byte)(i % 256));
        }

        NativeMemory.Free(newPtr);
    }

    [Fact]
    public void Clear_ValidPointer_ClearsMemory()
    {
        nuint size = 100;
        void* ptr = NativeMemory.Alloc(size);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Initialize memory with non-zero values
        byte* bytePtr = (byte*)ptr;
        for (nuint i = 0; i < size; i++)
        {
            bytePtr[i] = 0xFF;
        }

        // Clear the memory
        NativeMemory.Clear(ptr, size);

        // Verify memory is cleared
        for (nuint i = 0; i < size; i++)
        {
            bytePtr[i].Should().Be(0);
        }

        NativeMemory.Free(ptr);
    }

    [Fact]
    public void Copy_ValidPointers_CopiesData()
    {
        nuint size = 100;
        void* source = NativeMemory.Alloc(size);
        void* destination = NativeMemory.Alloc(size);

        // Initialize source with data
        byte* sourcePtr = (byte*)source;
        for (nuint i = 0; i < size; i++)
        {
            sourcePtr[i] = (byte)(i % 256);
        }

        // Copy data from source to destination
        NativeMemory.Copy(source, destination, size);

        // Verify destination has the same data
        byte* destPtr = (byte*)destination;
        for (nuint i = 0; i < size; i++)
        {
            destPtr[i].Should().Be((byte)(i % 256));
        }

        NativeMemory.Free(source);
        NativeMemory.Free(destination);
    }

    [Fact]
    public void Fill_ValidPointer_FillsMemory()
    {
        nuint size = 100;
        byte value = 0xAA;
        void* ptr = NativeMemory.Alloc(size);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Fill the memory
        NativeMemory.Fill(ptr, size, value);

        // Verify memory is filled
        byte* bytePtr = (byte*)ptr;
        for (nuint i = 0; i < size; i++)
        {
            bytePtr[i].Should().Be(value);
        }

        NativeMemory.Free(ptr);
    }

    [Fact]
    public void AlignedAlloc_ValidAlignment_ReturnsAlignedPointer()
    {
        nuint size = 100;
        nuint alignment = 16;
        void* ptr = NativeMemory.AlignedAlloc(size, alignment);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Verify pointer is aligned
        nuint address = (nuint)ptr;
        (address % alignment).Should().Be((nuint)0);

        NativeMemory.AlignedFree(ptr);
    }

    [Fact]
    public void AlignedAlloc_NonPowerOfTwoAlignment_ThrowsArgumentException()
    {
        nuint size = 100;
        nuint alignment = 3; // Not a power of 2

        Action action = () => NativeMemory.AlignedAlloc(size, alignment);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AlignedFree_NullPointer_DoesNotThrow()
    {
        Action action = () => NativeMemory.AlignedFree(null);
        action.Should().NotThrow();
    }

    [Fact]
    public void AlignedRealloc_ValidPointer_PreservesData()
    {
        nuint initialSize = 10;
        nuint newSize = 20;
        nuint alignment = 16;

        void* ptr = NativeMemory.AlignedAlloc(initialSize, alignment);
        (ptr is null).Should().BeFalse("Pointer should not be null");

        // Write initial data
        byte* bytePtr = (byte*)ptr;
        for (nuint i = 0; i < initialSize; i++)
        {
            bytePtr[i] = (byte)(i % 256);
        }

        // Reallocate
        void* newPtr = NativeMemory.AlignedRealloc(ptr, newSize, alignment);
        (newPtr is null).Should().BeFalse("Pointer should not be null");

        // Verify pointer is aligned
        nuint address = (nuint)newPtr;
        (address % alignment).Should().Be((nuint)0);

        // Verify initial data is preserved
        byte* newBytePtr = (byte*)newPtr;
        for (nuint i = 0; i < initialSize; i++)
        {
            newBytePtr[i].Should().Be((byte)(i % 256));
        }

        NativeMemory.AlignedFree(newPtr);
    }

    [Fact]
    public void AlignedRealloc_NonPowerOfTwoAlignment_ThrowsArgumentException()
    {
        nuint size = 100;
        nuint newSize = 200;
        nuint alignment = 3; // Not a power of 2
        void* ptr = NativeMemory.Alloc(size);

        Action action = () => NativeMemory.AlignedRealloc(ptr, newSize, alignment);
        action.Should().Throw<ArgumentException>();

        NativeMemory.Free(ptr);
    }
}
