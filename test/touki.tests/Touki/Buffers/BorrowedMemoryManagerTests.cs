// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;

namespace Touki.Buffers;

[TestClass]
public class BorrowedMemoryManagerTests
{
    [TestMethod]
    public unsafe void Pin_ValidIndex_ExposesCallerMemory()
    {
        byte[] bytes = [10, 20, 30];
        fixed (byte* pointer = bytes)
        {
            using BorrowedMemoryManager manager = new(pointer, bytes.Length);
            using MemoryHandle handle = manager.Pin(1);

            ((byte*)handle.Pointer)[0].Should().Be((byte)20);
            manager.GetSpan().SequenceEqual(bytes).Should().BeTrue();
            manager.Unpin();
        }
    }
}
