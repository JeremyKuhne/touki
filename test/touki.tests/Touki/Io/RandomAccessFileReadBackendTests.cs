// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using Microsoft.Win32.SafeHandles;
using touki.perf;

namespace Touki.Io;

[TestClass]
public class RandomAccessFileReadBackendTests
{
    [TestMethod]
    public async Task Read_InterleavedSyncAndAsync_ReturnsExactBytesWithoutChangingHandleMode()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "backend.bin");
        const int Length = 3 * 65536 + 37;
        LargeFileReadPattern.Create(path, Length, blocks: false);
        using SafeFileHandle handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
        using RandomAccessFileReadBackend backend = new(handle, ownsHandle: false);
        byte[] actual = new byte[7777];
        byte[] expected = new byte[actual.Length];
        for (int i = 0; i < 32; i++)
        {
            long offset = i * 31;
            backend.Read(actual, offset).Should().Be(actual.Length);
            LargeFileReadPattern.Fill(expected, offset, Length, blocks: false);
            actual.Should().Equal(expected);
            offset += 65539;
            int count = await backend.ReadAsync(actual, offset, CancellationToken.None)
                .ConfigureAwait(continueOnCapturedContext: false);

            count.Should().Be(actual.Length);
            LargeFileReadPattern.Fill(expected, offset, Length, blocks: false);
            actual.Should().Equal(expected);
        }

        handle.IsAsync.Should().BeTrue();
        backend.Read(actual, Length).Should().Be(0);
    }

#if !DEBUG
    [TestMethod]
    public void Read_WarmRepeatedSynchronousReads_DoesNotAllocate()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "allocation.bin");
        LargeFileReadPattern.Create(path, 65536, blocks: false);
        using SafeFileHandle handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
        using RandomAccessFileReadBackend backend = new(handle, ownsHandle: false);
        byte[] buffer = new byte[65536];
        backend.Read(buffer, 0).Should().Be(buffer.Length);
        int total = 0;
        using (MemoryWatch.Create)
        {
            for (int i = 0; i < 100; i++)
            {
                total += backend.Read(buffer, 0);
            }
        }

        total.Should().Be(100 * buffer.Length);
    }
#endif

    [TestMethod]
    public void Read_ClosedBorrowedHandle_ThrowsWithoutClosingOtherResources()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "closed.bin");
        LargeFileReadPattern.Create(path, 8, blocks: false);
        using SafeFileHandle handle = File.OpenHandle(path, options: FileOptions.Asynchronous);
        using RandomAccessFileReadBackend backend = new(handle, ownsHandle: false);
        handle.Dispose();
        Action read = () => backend.Read(new byte[8], 0);
        read.Should().Throw<ObjectDisposedException>();
        Action empty = () => backend.Read([], 0);
        empty.Should().Throw<ObjectDisposedException>();
    }
}
#endif
