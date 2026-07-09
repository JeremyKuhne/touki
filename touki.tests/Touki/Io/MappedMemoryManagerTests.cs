// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;

namespace Touki.Io;

[TestClass]
public class MappedMemoryManagerTests
{
    [TestMethod]
    public void CreateFromFile_MapsFileContents()
    {
        byte[] bytes = [10, 20, 30, 40, 50];
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        manager.Memory.Length.Should().Be(bytes.Length);
        manager.Memory.Span.SequenceEqual(bytes).Should().BeTrue();
        manager.GetSpan().SequenceEqual(bytes).Should().BeTrue();
    }

    [TestMethod]
    public void CreateFromFile_NullPath_ThrowsArgumentNullException()
    {
        Action act = () => _ = MappedMemoryManager.CreateFromFile(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CreateFromFile_EmptyFile_ThrowsIOException()
    {
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "empty.bin");
        System.IO.File.WriteAllBytes(path, []);

        Action act = () => _ = MappedMemoryManager.CreateFromFile(path);
        act.Should().Throw<System.IO.IOException>();
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        byte[] bytes = [1, 2, 3];
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);
        ((IDisposable)manager).Dispose();
        ((IDisposable)manager).Dispose();
    }

    [TestMethod]
    public void GetSpan_AfterDispose_ThrowsObjectDisposedException()
    {
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [1, 2, 3]);

        MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);
        ((IDisposable)manager).Dispose();

        Action act = () => { _ = manager.GetSpan().Length; };
        act.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public void Pin_AfterDispose_ThrowsObjectDisposedException()
    {
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [1, 2, 3]);

        MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);
        ((IDisposable)manager).Dispose();

        Action act = () => manager.Pin();
        act.Should().Throw<ObjectDisposedException>();
    }

    [TestMethod]
    public void Pin_NegativeIndex_ThrowsArgumentOutOfRangeException()
    {
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [1, 2, 3]);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        Action act = () => manager.Pin(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Pin_IndexBeyondLength_ThrowsArgumentOutOfRangeException()
    {
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [1, 2, 3]);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        Action act = () => manager.Pin(4);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public unsafe void Pin_ValidIndex_PinsAtOffset()
    {
        byte[] bytes = [10, 20, 30];
        using TempFolder folder = new();
        string path = System.IO.Path.Combine(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        using (MemoryHandle handle = manager.Pin(1))
        {
            ((byte*)handle.Pointer)[0].Should().Be((byte)20);
        }

        // Pinning at the end (elementIndex == length) is allowed and yields an end pointer.
        using MemoryHandle endHandle = manager.Pin(bytes.Length);
    }
}
