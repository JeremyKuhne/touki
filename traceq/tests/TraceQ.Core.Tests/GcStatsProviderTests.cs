// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing.Providers;

[TestClass]
public sealed class GcStatsProviderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // The allocation smoke trace is captured under the GC-verbose profile, so it
    // carries the GC events this provider reads - no separate fixture is needed.
    private static GcStatsResult LoadGcStats() =>
        new GcStatsProvider().Read(FixturePath("alloc.nettrace"));

    [TestMethod]
    public void Read_GcVerboseFixture_ReportsCollections()
    {
        GcStatsResult result = LoadGcStats();

        result.GcCount.Should().BeGreaterThan(0, "the alloc workload triggers collections under GC-verbose");
        result.Gcs.Should().HaveCount(result.GcCount);
        (result.Gen0Count + result.Gen1Count + result.Gen2Count).Should().Be(result.GcCount);
    }

    [TestMethod]
    public void Read_GcVerboseFixture_PauseSummaryIsConsistent()
    {
        GcStatsResult result = LoadGcStats();

        result.TotalPauseMs.Should().BeGreaterThan(0.0);
        result.MaxPauseMs.Should().BeGreaterThan(0.0);
        // The mean lies between zero and the max, and the max never exceeds the total.
        result.MeanPauseMs.Should().BeApproximately(result.TotalPauseMs / result.GcCount, 0.001);
        result.MaxPauseMs.Should().BeLessThanOrEqualTo(result.TotalPauseMs);
    }

    [TestMethod]
    public void Read_GcVerboseFixture_EveryRecordIsWellFormed()
    {
        GcStatsResult result = LoadGcStats();

        result.Gcs.Should().OnlyContain(g => g.Generation >= 0 && g.Generation <= 2);
        result.Gcs.Should().OnlyContain(g => g.PauseMs >= 0.0);
        result.Gcs.Should().OnlyContain(g => g.Kind.Length > 0);
        // The reported peak heap is at least the largest per-collection heap size.
        result.PeakHeapSizeMB.Should().BeGreaterThanOrEqualTo(result.Gcs.Max(static g => g.HeapSizeAfterMB));
    }

    [TestMethod]
    public void Read_MissingFile_ThrowsFileNotFound()
    {
        GcStatsProvider provider = new();

        Action act = () => provider.Read(FixturePath("does-not-exist.nettrace"));

        act.Should().Throw<FileNotFoundException>();
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(null)]
    public void Read_NullOrEmptyPath_ThrowsArgument(string? path)
    {
        GcStatsProvider provider = new();

        Action act = () => provider.Read(path!);

        act.Should().Throw<ArgumentException>();
    }
}
