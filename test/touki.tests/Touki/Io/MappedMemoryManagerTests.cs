// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Buffers;
using System.IO.MemoryMappedFiles;

namespace Touki.Io;

[TestClass]
public class MappedMemoryManagerTests
{
    [TestMethod]
    public void CreateFromFile_MapsFileContents()
    {
        byte[] bytes = [10, 20, 30, 40, 50];
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        manager.Memory.Length.Should().Be(bytes.Length);
        manager.Memory.Span.SequenceEqual(bytes).Should().BeTrue();
        manager.GetSpan().SequenceEqual(bytes).Should().BeTrue();
    }

    [TestMethod]
    public void CreateFromFile_NullPath_ThrowsArgumentNullException()
    {
        // Intentionally pass null to exercise path validation.
    #pragma warning disable CS8625
        Action act = () => _ = MappedMemoryManager.CreateFromFile(path: null);
    #pragma warning restore CS8625
        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void CreateFromFile_EmptyFile_ThrowsIOException()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "empty.bin");
        System.IO.File.WriteAllBytes(path, []);

        Action act = () => _ = MappedMemoryManager.CreateFromFile(path);
        act.Should().Throw<System.IO.IOException>();
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        byte[] bytes = [1, 2, 3];
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);
        ((IDisposable)manager).Dispose();
        ((IDisposable)manager).Dispose();
    }

    [TestMethod]
    public void GetSpan_AfterDispose_ThrowsObjectDisposedException()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "data.bin");
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
        string path = Path.Join(folder.TempPath, "data.bin");
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
        string path = Path.Join(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [1, 2, 3]);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        Action act = () => manager.Pin(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Pin_IndexBeyondLength_ThrowsArgumentOutOfRangeException()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "data.bin");
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
        string path = Path.Join(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, bytes);

        using MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);

        using (MemoryHandle handle = manager.Pin(1))
        {
            ((byte*)handle.Pointer)[0].Should().Be((byte)20);
        }

        // Pinning at the end (elementIndex == length) is allowed and yields an end pointer.
        using MemoryHandle endHandle = manager.Pin(bytes.Length);
    }

    [TestMethod]
    public unsafe void Pin_ManagerOtherwiseUnreachable_KeepsMappingAlive()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [10, 20, 30]);
        (MemoryHandle handle, WeakReference manager) = PinAndAbandon(path);

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            manager.IsAlive.Should().BeTrue();
            ((byte*)handle.Pointer)[0].Should().Be((byte)20);
        }
        finally
        {
            if (manager.Target is IDisposable disposable)
            {
                disposable.Dispose();
            }

            handle.Dispose();
        }
    }

    [TestMethod]
    public void Finalize_ManagerAbandoned_ReleasesMappedView()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "data.bin");
        System.IO.File.WriteAllBytes(path, [10, 20, 30]);
        SafeHandle viewHandle = CreateAndAbandon(path);

        try
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            viewHandle.IsClosed.Should().BeTrue();
        }
        finally
        {
            viewHandle.Dispose();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (MemoryHandle Handle, WeakReference Manager) PinAndAbandon(string path)
    {
        MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);
        return (manager.Pin(1), new(manager));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static SafeHandle CreateAndAbandon(string path)
    {
        MappedMemoryManager manager = MappedMemoryManager.CreateFromFile(path);
        MappedViewLease lease = manager.TestAccessor.Dynamic._lease;
        MemoryMappedViewAccessor accessor = lease.TestAccessor.Dynamic._accessor;
        return accessor.SafeMemoryMappedViewHandle;
    }
}
