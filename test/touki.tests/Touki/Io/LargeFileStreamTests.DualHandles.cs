// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
using Microsoft.Win32.SafeHandles;
using touki.perf;

namespace Touki.Io;

public partial class LargeFileStreamTests
{
    [TestMethod]
    public async Task Open_SeparateSynchronousHandle_PreservesAsyncModeAndInterleavedReads()
    {
        RequireDualHandlePlatform();
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "dual.bin");
        const int Length = 3 * Window + 17;
        LargeFileReadPattern.Create(path, Length, blocks: false);
        using SafeFileHandle original = File.OpenHandle(path, options: FileOptions.Asynchronous);
        using LargeFileStream stream = LargeFileStream.Open(original, ownsHandle: false, new()
        {
            UseSeparateSynchronousHandle = true,
            AccessPattern = LargeFileAccessPattern.Sparse
        });

        byte[] actual = new byte[72];
        byte[] expected = new byte[actual.Length];
        foreach (long offset in new long[] { 0, Window + 3, 5, Length - actual.Length })
        {
            stream.Position = offset;
            stream.ReadExactly(actual);
            LargeFileReadPattern.Fill(expected, offset, Length, blocks: false);
            actual.Should().Equal(expected);
            stream.Position = offset;
            await stream.ReadExactlyAsync(actual).ConfigureAwait(continueOnCapturedContext: false);
            actual.Should().Equal(expected);
        }

        stream.Options.UseSeparateSynchronousHandle.Should().BeTrue();
        original.IsAsync.Should().BeTrue();
        stream.Dispose();
        original.IsClosed.Should().BeFalse();
    }

    [TestMethod]
    public void Open_SeparateHandleWithExclusiveOriginal_ReportsSharingViolationWithoutOwnershipTransfer()
    {
        RequireDualHandlePlatform();
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "exclusive.bin");
        LargeFileReadPattern.Create(path, 8, blocks: false);
        using SafeFileHandle original = File.OpenHandle(
            path,
            share: FileShare.None,
            options: FileOptions.Asynchronous);

        Action open = () =>
        {
            using LargeFileStream stream = LargeFileStream.Open(original, ownsHandle: true, new()
            {
                UseSeparateSynchronousHandle = true
            });
        };

        open.Should().Throw<IOException>().Which.HResult.Should().Be(unchecked((int)0x80070020));
        original.IsClosed.Should().BeFalse();
        using LargeFileStream compatible = LargeFileStream.Open(original, ownsHandle: false);
        compatible.ReadByte().Should().Be(0);
    }

    [TestMethod]
    public void Open_SeparateHandlePathSharingFailure_ClosesTheNewOriginal()
    {
        RequireDualHandlePlatform();
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "exclusive.bin");
        LargeFileReadPattern.Create(path, 8, blocks: false);
        Action open = () =>
        {
            using LargeFileStream stream = LargeFileStream.Open(path, new()
            {
                Share = FileShare.None,
                UseSeparateSynchronousHandle = true
            });
        };

        open.Should().Throw<IOException>().Which.HResult.Should().Be(unchecked((int)0x80070020));
        using FileStream exclusive = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        exclusive.ReadByte().Should().Be(0);
    }

    [TestMethod]
    public void Open_SeparateHandles_DisposalClosesBothWithoutLeakingFileLocks()
    {
        RequireDualHandlePlatform();
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "dispose.bin");
        LargeFileReadPattern.Create(path, 8, blocks: false);
        using (LargeFileStream stream = LargeFileStream.Open(path, new() { UseSeparateSynchronousHandle = true }))
        {
            stream.ReadByte().Should().Be(0);
        }

        using FileStream exclusive = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        exclusive.Length.Should().Be(8);
    }

    [TestMethod]
    public void Open_SeparateHandles_RenameAndReplacementDoesNotChangeTheReadFile()
    {
        RequireDualHandlePlatform();
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "original.bin");
        string renamed = Path.Join(folder.TempPath, "renamed.bin");
        LargeFileReadPattern.Create(path, Window, blocks: false);
        using SafeFileHandle original = File.OpenHandle(
            path,
            share: FileShare.Read | FileShare.Delete,
            options: FileOptions.Asynchronous);

        File.Move(path, renamed);
        File.WriteAllBytes(path, [255]);
        using LargeFileStream stream = LargeFileStream.Open(original, ownsHandle: false, new()
        {
            UseSeparateSynchronousHandle = true
        });

        stream.Length.Should().Be(Window);
        stream.ReadByte().Should().Be(0);
    }

    [TestMethod]
    public async Task Read_StatisticsObservedConcurrently_PublishExactMonotonicTotals()
    {
        using TestFileReadBackend backend = new(2 * Window);
        using LargeFileStream stream = Open(backend);
        byte[] buffer = new byte[51];
        Task writer = Task.Run(() =>
        {
            for (int i = 0; i < 1000; i++)
            {
                stream.Read(buffer).Should().Be(buffer.Length);
            }
        });

        long previous = 0;
        while (!writer.IsCompleted)
        {
            long delivered = stream.Statistics.BytesDelivered;
            delivered.Should().BeGreaterThanOrEqualTo(previous);
            previous = delivered;
            await Task.Yield();
        }

        await writer.ConfigureAwait(continueOnCapturedContext: false);
        stream.Statistics.BytesDelivered.Should().Be(51000);
        stream.Statistics.BackendBytesRead.Should().Be(Window);
        stream.Statistics.RequestsIssued.Should().Be(1);
        stream.Statistics.MaxInFlight.Should().Be(1);
        stream.Position.Should().Be(51000);
    }

    private static void RequireDualHandlePlatform()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            Assert.Inconclusive("Separate synchronous handles require Windows 10 or later.");
        }
    }
}
#endif
