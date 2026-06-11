// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class FoldingAggregatorClassifyTests
{
    private static FoldingAggregator Engine(IReadOnlyList<SampleStack> samples) =>
        new(new StackSampleSource(MetricInfo.Cpu, samples));

    private static Dictionary<string, double> ByCategory(ClassifyResult result)
    {
        Dictionary<string, double> map = new(StringComparer.Ordinal);
        foreach (CategoryRow row in result.Categories)
        {
            map[row.Category] = row.Weight;
        }

        return map;
    }

    [TestMethod]
    public void Classify_BucketsLeavesByRuntimeWorkCategory()
    {
        // Stacks are outermost-first; the leaf (last frame) is the self-time site.
        FoldingAggregator engine = Engine(
        [
            new SampleStack(["MyApp.Run", "WKS::gc_heap::plan_phase"], 10.0, "1"),  // gc
            new SampleStack(["MyApp.Run", "JIT_MemCpy"], 6.0, "1"),                 // copying
            new SampleStack(["MyApp.Run", "ntdll!memset"], 4.0, "1"),               // zeroing
            new SampleStack(["MyApp.Run", "MyApp.Work"], 5.0, "1")                  // other (managed)
        ]);

        ClassifyResult result = engine.Classify("");

        result.ScopeWeight.Should().Be(25.0);
        Dictionary<string, double> byCategory = ByCategory(result);
        byCategory[FrameCategories.Gc].Should().Be(10.0);
        byCategory[FrameCategories.Copying].Should().Be(6.0);
        byCategory[FrameCategories.Other].Should().Be(5.0);
        byCategory[FrameCategories.Zeroing].Should().Be(4.0);
    }

    [TestMethod]
    public void Classify_RanksCategoriesByWeightWithPercentages()
    {
        FoldingAggregator engine = Engine(
        [
            new SampleStack(["MyApp.Run", "WKS::gc_heap::plan_phase"], 30.0, "1"),
            new SampleStack(["MyApp.Run", "JIT_MemCpy"], 10.0, "1")
        ]);

        ClassifyResult result = engine.Classify("");

        // Highest weight first; percentages are of the scoped total (40).
        result.Categories[0].Category.Should().Be(FrameCategories.Gc);
        result.Categories[0].PercentOfScope.Should().BeApproximately(75.0, 0.001);
        result.Categories[1].Category.Should().Be(FrameCategories.Copying);
        result.Categories[1].PercentOfScope.Should().BeApproximately(25.0, 0.001);
    }

    [TestMethod]
    public void Classify_FoldsTheSyntheticMarkerToTheRealLeafBelowIt()
    {
        // The synthetic CPU_TIME marker is the leaf, but it is marker-folded, so the
        // sample classifies by the real frame beneath it (the GC frame) rather than
        // collapsing every marker sample into one meaningless bucket.
        FoldingAggregator engine = Engine(
        [
            new SampleStack(["MyApp.Run", "WKS::gc_heap::plan_phase", "CPU_TIME"], 8.0, "1")
        ]);

        ClassifyResult result = engine.Classify("");

        ByCategory(result).Should().ContainKey(FrameCategories.Gc);
        ByCategory(result)[FrameCategories.Gc].Should().Be(8.0);
    }

    [TestMethod]
    public void Classify_DoesNotFoldTheJitHelperThunks()
    {
        // Unlike the default fold list, classify must NOT fold Memmove / WriteBarrier /
        // JIT_ helpers - those ARE the work being classified. A memcpy helper leaf stays
        // the leaf and classifies as copying rather than folding into its managed caller.
        FoldingAggregator engine = Engine(
        [
            new SampleStack(["MyApp.Run", "JIT_MemCpy"], 7.0, "1")
        ]);

        ClassifyResult result = engine.Classify("");

        ByCategory(result).Should().ContainKey(FrameCategories.Copying);
        ByCategory(result).Should().NotContainKey(FrameCategories.Other);
    }
}
