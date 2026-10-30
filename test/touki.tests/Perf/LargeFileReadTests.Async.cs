// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
namespace touki.perf;

public partial class LargeFileReadTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(17)]
    [DataRow(65537)]
    [DataRow(1048593)]
    public async Task ReadExactlyAsync_VariedLengths_MatchesEveryByte(int length)
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "async.bin");
        LargeFileReadPattern.Create(path, length, blocks: false);
        LargeFileReadReference[] references =
        [
            LargeFileReadReference.TunedAsyncHandle,
            LargeFileReadReference.LargeFileStream
        ];

        foreach (LargeFileReadReference reference in references)
        {
            foreach (LargeFileReadScenario scenario in Enum.GetValues<LargeFileReadScenario>())
            {
                using LargeFileReadReferenceReader reader = new(path, reference, scenario);
                byte[] actual = new byte[7777];
                byte[] expected = new byte[actual.Length];
                for (long offset = 0; offset < length;)
                {
                    int count = (int)Math.Min(actual.Length, length - offset);
                    await reader.ReadExactlyAsync(offset, actual, count).ConfigureAwait(continueOnCapturedContext: false);
                    LargeFileReadPattern.Fill(expected.AsSpan(0, count), offset, length, blocks: false);
                    actual.AsSpan(0, count).SequenceEqual(expected.AsSpan(0, count)).Should().BeTrue();
                    offset += count;
                }

                long[] offsets = [Math.Max(0, length - 17), 0, Math.Max(0, length / 2 - 3)];
                foreach (long offset in offsets)
                {
                    int count = (int)Math.Min(actual.Length, length - offset);
                    await reader.ReadExactlyAsync(offset, actual, count).ConfigureAwait(continueOnCapturedContext: false);
                    LargeFileReadPattern.Fill(expected.AsSpan(0, count), offset, length, blocks: false);
                    actual.AsSpan(0, count).SequenceEqual(expected.AsSpan(0, count)).Should().BeTrue();
                }
            }
        }
    }

    [TestMethod]
    public async Task RunAsync_AllScenarios_MatchesSynchronousTraceAndBackendVolume()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "trace.bin");
        LargeFileReadPattern.Create(path, 2 * LargeFileReadPattern.WindowSize + 133, blocks: true);
        LargeFileReadReference[] references =
        [
            LargeFileReadReference.TunedAsyncHandle,
            LargeFileReadReference.LargeFileStream
        ];

        foreach (LargeFileReadReference reference in references)
        {
            foreach (LargeFileReadScenario scenario in Enum.GetValues<LargeFileReadScenario>())
            {
                using LargeFileReadReferenceReader sync = new(path, reference, scenario);
                using LargeFileReadReferenceReader asyncReader = new(path, reference, scenario);
                long expected = LargeFileReadReplay.Run(sync, scenario);
                long actual = await LargeFileReadReplay.RunAsync(asyncReader, scenario)
                    .ConfigureAwait(continueOnCapturedContext: false);

                actual.Should().Be(expected);
                asyncReader.BackendBytesRead.Should().Be(sync.BackendBytesRead);
                asyncReader.RequestsIssued.Should().Be(sync.RequestsIssued);
            }
        }
    }

    [TestMethod]
    public async Task ReadExactlyAsync_PreCancelledEmptyRead_ThrowsBeforeIo()
    {
        using TempFolder folder = new();
        string path = Path.Join(folder.TempPath, "empty.bin");
        LargeFileReadPattern.Create(path, 0, blocks: false);
        using LargeFileReadReferenceReader reader = new(
            path,
            LargeFileReadReference.TunedAsyncHandle,
            LargeFileReadScenario.Sequential);

        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Func<Task> read = async () => await reader.ReadExactlyAsync(0, [], 0, cancellation.Token)
            .ConfigureAwait(continueOnCapturedContext: false);

        OperationCanceledException exception = (await read.Should().ThrowAsync<OperationCanceledException>()
            .ConfigureAwait(continueOnCapturedContext: false)).Which;

        exception.CancellationToken.Should().Be(cancellation.Token);
        reader.RequestsIssued.Should().Be(0);
    }
}
#endif
