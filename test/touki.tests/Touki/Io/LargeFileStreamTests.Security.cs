// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using System.Buffers;
using System.ComponentModel;
using touki.perf;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Touki.Io;

public partial class LargeFileStreamTests
{
    [TestMethod]
    [DataRow(data: false)]
    [DataRow(data: true)]
    public unsafe void Read_NtfsSparseFile_CrossesTwoAndFourGiBBoundaries(bool useSeparateHandle)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            Assert.Inconclusive("Sparse-file validation requires Windows and NTFS.");
            return;
        }

        using TempFolder folder = new();
        string root = Path.GetPathRoot(folder.TempPath) ?? throw new InvalidOperationException("No volume root.");
        DriveInfo drive = new(root);
        if (drive.DriveFormat != "NTFS" || drive.AvailableFreeSpace < 16 << 20)
        {
            Assert.Inconclusive("Sparse-file validation requires NTFS and 16 MiB free space.");
        }

        string path = Path.Join(folder.TempPath, "sparse.bin");
        long length = (1L << 32) + 256;
        long[] boundaries = [1L << 31, 1L << 32];
        using (FileStream file = new(path, FileMode.CreateNew, FileAccess.ReadWrite))
        {
            uint returned;
            if (!PInvoke.DeviceIoControl(
                (HANDLE)file.SafeFileHandle.DangerousGetHandle(),
                PInvoke.FSCTL_SET_SPARSE,
                lpInBuffer: null,
                nInBufferSize: 0,
                lpOutBuffer: null,
                nOutBufferSize: 0,
                &returned,
                lpOverlapped: null))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            file.SetLength(length);
            foreach (long boundary in boundaries)
            {
                byte[] pattern = new byte[128];
                LargeFileReadPattern.Fill(pattern, boundary - 64, length, blocks: false);
                file.Position = boundary - 64;
                file.Write(pattern);
            }
        }

        using LargeFileStream stream = LargeFileStream.Open(path, new()
        {
            ChunkSize = Window,
            UseSeparateSynchronousHandle = useSeparateHandle
        });

        foreach (long boundary in boundaries)
        {
            stream.Position = boundary - 64;
            byte[] actual = new byte[128];
            byte[] expected = new byte[actual.Length];
            stream.ReadExactly(actual);
            LargeFileReadPattern.Fill(expected, boundary - 64, length, blocks: false);
            actual.Should().Equal(expected);
            stream.Position.Should().Be(boundary + 64);
        }

        stream.Position = (1L << 32) + 8;
        using LargeFileChunk tail = stream.ReadChunk();
        tail.FileOffset.Should().Be((1L << 32) + 8);
        tail.Memory.Length.Should().Be(248);
        tail.IsLast.Should().BeTrue();
        byte[] expectedPrefix = new byte[56];
        LargeFileReadPattern.Fill(expectedPrefix, tail.FileOffset, length, blocks: false);
        tail.Memory[..56].ToArray().Should().Equal(expectedPrefix);
    }

    [TestMethod]
    public void Pin_BoundaryIndices_RejectsOutsideLease()
    {
        using TestFileReadBackend backend = new(Window);
        using LargeFileStream stream = Open(backend);
        stream.ReadByte();
        using LargeFileChunk chunk = stream.ReadChunk();
        PinAndDispose(chunk, chunk.Memory.Length);
        Action negative = () => PinAndDispose(chunk, -1);
        Action beyond = () => PinAndDispose(chunk, chunk.Memory.Length + 1);
        negative.Should().Throw<ArgumentOutOfRangeException>();
        beyond.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void Unpin_StaleLease_DoesNotReleaseAnotherOwnersPin()
    {
        using TestFileReadBackend backend = new(3 * Window);
        using LargeFileStream stream = Open(backend);
        using LargeFileChunk first = stream.ReadChunk();
        first.Dispose();
        using LargeFileChunk next = stream.ReadChunk();
        using MemoryHandle pin = next.Pin();
        Action stale = first.Unpin;
        stale.Should().Throw<InvalidOperationException>();
        next.Memory.Length.Should().Be(Window);
    }

    [TestMethod]
    public void ReleaseOwner_StaleIdentity_DoesNotReturnReassignedSlot()
    {
        int returned = 0;
        LargeFileBufferSlot slot = new(Window, () => returned++);
        LargeFileStreamStatistics statistics = new();
        try
        {
            slot.TryReserve().Should().BeTrue();
            using LargeFileChunk first = slot.Lease(0, 8, 0, isLast: false, 0, statistics);
            first.Dispose();
            returned.Should().Be(1);
            slot.TryReserve().Should().BeTrue();
            using LargeFileChunk replacement = slot.Lease(0, 8, Window, isLast: false, 1, statistics);
            slot.ReleaseOwner(first);
            returned.Should().Be(1);
            replacement.Memory.Length.Should().Be(8);
            slot.TryReserve().Should().BeFalse();
        }
        finally
        {
            slot.ReleaseStream();
        }

        returned.Should().Be(2);
    }

    [TestMethod]
    public void Read_ThousandsOfSeeksAndLeases_KeepFixedSlotCapacity()
    {
        using TestFileReadBackend backend = new(1024L * Window);
        using LargeFileStream stream = Open(backend, outstanding: 3);
        stream.ReadByte();
        stream.Statistics.PeakBufferedBytes.Should().Be(Window);
        for (int i = 0; i < 2048; i++)
        {
            stream.Position = (i % 1024L) * Window;
            using LargeFileChunk chunk = stream.ReadChunk();
        }

        stream.Statistics.PeakBufferedBytes.Should().Be(4 * Window);
        stream.Statistics.PeakBufferedBytes.Should().BeLessThanOrEqualTo(stream.Options.MaxBufferedBytes);
    }
}
#endif
