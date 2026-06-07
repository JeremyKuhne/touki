// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Tracing.Providers;

[TestClass]
public sealed class AllocationProviderTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static StackSampleSource LoadAlloc() =>
        new AllocationProvider().Read(FixturePath("alloc.nettrace"));

    [TestMethod]
    public void Read_AllocFixture_CarriesTheAllocationMetric()
    {
        StackSampleSource source = LoadAlloc();

        source.Metric.Should().Be(MetricInfo.Allocations);
        source.Metric.Unit.Should().Be("bytes");
        source.Samples.Should().NotBeEmpty("the GcVerbose trace carries GCAllocationTick stacks");
    }

    [TestMethod]
    public void Read_AllocFixture_WeightsStacksByBytes()
    {
        StackSampleSource source = LoadAlloc();

        // Each sample is weighted by the bytes allocated since the previous tick,
        // which the runtime emits at roughly a 100 KB cadence - so every weight is
        // a positive, sizable byte count, not a 1 ms CPU unit.
        source.Samples.Should().OnlyContain(s => s.Weight > 0);
        source.Samples.Max(static s => s.Weight).Should().BeGreaterThan(1000);
    }

    [TestMethod]
    public void InclusiveTime_AllocFixture_CreditsTheAllocatingDriverMethod()
    {
        FoldingAggregator aggregator = new(LoadAlloc());

        RankingResult result = aggregator.InclusiveTime("", FrameNames.DefaultFoldPatterns, 50);

        result.ScopeWeight.Should().BeGreaterThan(0, "the scope total is the bytes allocated");

        // The benchmark's allocation loop is on every allocation stack, so it must
        // be credited with allocation bytes in the inclusive ranking.
        result.Rows.Should().Contain(
            r => r.Frame.Contains("AllocLoop", StringComparison.Ordinal),
            "the allocating benchmark frames should appear in the allocation ranking");
    }

    [TestMethod]
    public void SelfTime_AllocFixture_RanksAllocationSitesByBytes()
    {
        FoldingAggregator aggregator = new(LoadAlloc());

        RankingResult result = aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 25);

        result.Rows.Should().NotBeEmpty();
        // The ranking is ordered by bytes (the allocation metric), highest first.
        result.Rows[0].Weight.Should().BeGreaterThan(0);
        for (int i = 1; i < result.Rows.Count; i++)
        {
            result.Rows[i].Weight.Should().BeLessThanOrEqualTo(result.Rows[i - 1].Weight);
        }
    }
}
