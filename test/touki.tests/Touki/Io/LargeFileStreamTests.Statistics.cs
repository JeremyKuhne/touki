// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

#if NET
namespace Touki.Io;

public partial class LargeFileStreamTests
{
    [TestMethod]
    [DataRow(data: false)]
    [DataRow(data: true)]
    public async Task Read_TimingPolicy_DoesNotChangeExactCounters(bool collectTimings)
    {
        using TestFileReadBackend backend = new(3 * Window) { MaxReadSize = Window / 2 };
        using LargeFileStream stream = Open(backend, collectTimings: collectTimings);
        using (LargeFileChunk first = await stream.ReadChunkAsync().ConfigureAwait(continueOnCapturedContext: false))
        {
            first.Memory.Length.Should().Be(Window);
        }

        using (LargeFileChunk second = stream.ReadChunk())
        {
            second.Memory.Length.Should().Be(Window);
        }

        stream.Statistics.TimingsEnabled.Should().Be(collectTimings);
        stream.Options.CollectTimings.Should().Be(collectTimings);
        stream.Statistics.BackendBytesRead.Should().Be(2 * Window);
        stream.Statistics.BytesDelivered.Should().Be(2 * Window);
        stream.Statistics.RequestsIssued.Should().Be(4);
        stream.Statistics.ShortReads.Should().Be(2);
        stream.Statistics.MaxChunksOutstanding.Should().Be(1);
        stream.Statistics.MaxInFlight.Should().Be(1);
        if (collectTimings)
        {
            stream.Statistics.TimeWaitingForIo.Should().BeGreaterThan(TimeSpan.Zero);
            stream.Statistics.TimeChunksHeld.Should().BeGreaterThan(TimeSpan.Zero);
        }
        else
        {
            stream.Statistics.TimeWaitingForIo.Should().Be(TimeSpan.Zero);
            stream.Statistics.TimeChunksHeld.Should().Be(TimeSpan.Zero);
        }

        stream.Dispose();
        stream.Statistics.RequestsIssued.Should().Be(4);
        stream.Statistics.TimingsEnabled.Should().Be(collectTimings);
    }
}
#endif
