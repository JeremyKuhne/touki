// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class FoldingAggregatorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static LoadedTrace LoadFolding() =>
        new TraceLoader().Load(FixturePath("folding.speedscope.json"));

    private static FoldingAggregator Engine(IReadOnlyList<SampleStack> samples) =>
        new(new StackSampleSource(MetricInfo.Cpu, samples));

    private static Dictionary<string, double> ByFrame(RankingResult result)
    {
        Dictionary<string, double> map = new(StringComparer.Ordinal);
        foreach (RankRow row in result.Rows)
        {
            map[row.Frame] = row.Weight;
        }

        return map;
    }

    [TestMethod]
    public void Load_SpeedscopeFixture_ReportsFormatDurationAndSamples()
    {
        LoadedTrace trace = LoadFolding();
        trace.Info.Format.Should().Be(TraceFormat.Speedscope);
        trace.Info.SampleCount.Should().Be(4);
        trace.Info.TotalWeight.Should().Be(25.0);
        trace.Info.SymbolResolutionRate.Should().Be(1.0);
    }

    [TestMethod]
    public void Source_LoadedTrace_CarriesTheCpuMetric()
    {
        LoadedTrace trace = LoadFolding();
        trace.Source.Metric.Should().Be(MetricInfo.Cpu);
        trace.Aggregator.Metric.Name.Should().Be("CPU");
        trace.Aggregator.Metric.Unit.Should().Be("ms");
    }

    [TestMethod]
    public void SelfTime_FoldsHelperLeavesIntoNearestRealLeaf()
    {
        RankingResult result = LoadFolding().Aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 25);
        result.ScopeWeight.Should().Be(25.0);

        Dictionary<string, double> self = ByFrame(result);
        // CPU_TIME folds into MyApp.Inner (10) plus the direct Inner sample (6) = 16.
        self["MyApp.Inner"].Should().Be(16.0);
        // WriteBarrier folds into MyApp.Work (4).
        self["MyApp.Work"].Should().Be(4.0);
        self["MyApp.Other"].Should().Be(5.0);
        self.Should().NotContainKey("CPU_TIME");
        self.Should().NotContainKey("WriteBarrier");
    }

    [TestMethod]
    public void SelfTime_TopFrameByMilliseconds_IsRankedFirst()
    {
        RankingResult result = LoadFolding().Aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 25);
        result.Rows[0].Frame.Should().Be("MyApp.Inner");
        result.Rows[0].PercentOfScope.Should().Be(64.0);
    }

    [TestMethod]
    public void SelfTime_TiedWeights_OrderedByFrameNameForDeterminism()
    {
        // Two leaves with identical weight must rank in a stable order; the contract
        // breaks the tie by ordinal frame name so output is deterministic across runs.
        List<SampleStack> samples =
        [
            new(["app!Zzz"], 5.0, "1"),
            new(["app!Aaa"], 5.0, "1")
        ];

        RankingResult result = Engine(samples).SelfTime("", FrameNames.DefaultFoldPatterns, 25);

        result.Rows[0].Frame.Should().Be("Aaa");
        result.Rows[1].Frame.Should().Be("Zzz");
    }

    [TestMethod]
    public void InclusiveTime_CreditsEachDistinctFrameOncePerSample()
    {
        RankingResult result = LoadFolding().Aggregator.InclusiveTime("", FrameNames.DefaultFoldPatterns, 25);
        result.ScopeWeight.Should().Be(25.0);

        Dictionary<string, double> incl = ByFrame(result);
        incl["Program.Main"].Should().Be(25.0);
        incl["MyApp.Work"].Should().Be(20.0);
        incl["MyApp.Inner"].Should().Be(16.0);
        incl["MyApp.Other"].Should().Be(5.0);
        incl.Should().NotContainKey("CPU_TIME");
        incl.Should().NotContainKey("WriteBarrier");
    }

    [TestMethod]
    public void CallersOf_ReportsImmediateCallerAndScopePercent()
    {
        CallersResult result = LoadFolding().Aggregator.CallersOf("MyApp.Inner", "", 25);
        result.TargetWeight.Should().Be(16.0);
        result.ScopeWeight.Should().Be(25.0);
        result.PercentOfScope.Should().Be(64.0);

        result.Callers.Should().ContainSingle();
        result.Callers[0].Caller.Should().Be("MyApp.Work");
        result.Callers[0].Weight.Should().Be(16.0);
        result.Callers[0].PercentOfTarget.Should().Be(100.0);
    }

    [TestMethod]
    public void CallersOf_MoreCallersThanTop_TruncatesToHeaviest()
    {
        // Three distinct callers of Target; top of 2 keeps only the two heaviest.
        List<SampleStack> samples =
        [
            new(["Ccc", "Target"], 1.0, "1"),
            new(["Bbb", "Target"], 2.0, "1"),
            new(["Aaa", "Target"], 3.0, "1")
        ];

        CallersResult result = Engine(samples).CallersOf("Target", "", top: 2);

        result.Callers.Should().HaveCount(2);
        result.Callers[0].Weight.Should().Be(3.0);
        result.Callers[1].Weight.Should().Be(2.0);
        // The 1 ms caller is dropped by the truncation.
        result.Callers.Should().NotContain(static c => c.Weight == 1.0);
    }

    [TestMethod]
    public void SelfTime_RootScoping_ExcludesSamplesWithoutRootFrame()
    {
        RankingResult result = LoadFolding().Aggregator.SelfTime("MyApp.Work", FrameNames.DefaultFoldPatterns, 25);
        // The MyApp.Other sample (5 ms) has no MyApp.Work frame and is excluded.
        result.ScopeWeight.Should().Be(20.0);

        Dictionary<string, double> self = ByFrame(result);
        self["MyApp.Inner"].Should().Be(16.0);
        self["MyApp.Work"].Should().Be(4.0);
        self.Should().NotContainKey("MyApp.Other");
    }

    [TestMethod]
    public void InclusiveTime_RootScoping_StartsFromRootFrame()
    {
        RankingResult result = LoadFolding().Aggregator.InclusiveTime("MyApp.Work", FrameNames.DefaultFoldPatterns, 25);
        result.ScopeWeight.Should().Be(20.0);

        Dictionary<string, double> incl = ByFrame(result);
        incl["MyApp.Work"].Should().Be(20.0);
        incl["MyApp.Inner"].Should().Be(16.0);
        // Program.Main is outside the root scope and must not be credited.
        incl.Should().NotContainKey("Program.Main");
    }

    private static Dictionary<string, double> ByLocation(LineRankingResult result)
    {
        Dictionary<string, double> map = new(StringComparer.Ordinal);
        foreach (LineRow row in result.Rows)
        {
            map[row.Location] = row.Weight;
        }

        return map;
    }

    [TestMethod]
    public void HotLines_AttributesLeafSamplesToSourceLineFoldingHelperLeaves()
    {
        // Two samples land directly on Run at two different lines; a third lands
        // on a folded WriteBarrier leaf whose real caller is Run at the first line.
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Run"], 3.0, "1", ["Engine.cs:20"]),
            new(["app!Run", "app!WriteBarrier"], 4.0, "1", ["Engine.cs:10", "Helpers.cs:99"])
        ];

        LineRankingResult result = Engine(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 25);

        result.ScopeWeight.Should().Be(12.0);
        result.MethodFilter.Should().Be("Run");

        Dictionary<string, double> byLine = ByLocation(result);
        // Line 10 collects the direct 5 ms plus the 4 ms folded back from WriteBarrier.
        byLine["Engine.cs:10"].Should().Be(9.0);
        byLine["Engine.cs:20"].Should().Be(3.0);
        byLine.Should().NotContainKey("Helpers.cs:99");

        result.Rows[0].Location.Should().Be("Engine.cs:10");
        result.Rows[0].Method.Should().Be("Run");
        result.Rows[0].PercentOfScope.Should().Be(75.0);
    }

    [TestMethod]
    public void HotLines_MethodFilter_ExcludesNonMatchingLeafMethods()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Other"], 7.0, "1", ["Other.cs:42"])
        ];

        LineRankingResult result = Engine(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 25);

        result.ScopeWeight.Should().Be(5.0);
        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("Engine.cs:10");
    }

    [TestMethod]
    public void HotLines_SamplesWithoutLocations_AreIgnored()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Run"], 9.0, "1")
        ];

        LineRankingResult result = Engine(samples).HotLines("", FrameNames.DefaultFoldPatterns, 25);

        result.ScopeWeight.Should().Be(5.0);
        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("Engine.cs:10");
    }

    [TestMethod]
    public void HotLines_UnresolvedLeafLocation_BucketsAsNoSource()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", [""])
        ];

        LineRankingResult result = Engine(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 25);

        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("<no source>");
        result.Rows[0].Weight.Should().Be(5.0);
    }

    [TestMethod]
    public void HotLines_TopLimit_TruncatesToHottestRows()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Run"], 3.0, "1", ["Engine.cs:20"])
        ];

        LineRankingResult result = Engine(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 1);

        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("Engine.cs:10");
    }

    [TestMethod]
    public void HotLines_TiedAtSameLocation_OrderedByMethodForDeterminism()
    {
        // Two different methods map to the same file:line with equal time; the
        // location tie-break alone leaves them equal, so method breaks the final tie
        // and the ordering stays deterministic regardless of enumeration order.
        List<SampleStack> samples =
        [
            new(["app!Zzz"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Aaa"], 5.0, "1", ["Engine.cs:10"])
        ];

        LineRankingResult result = Engine(samples).HotLines("", FrameNames.DefaultFoldPatterns, 25);

        result.Rows.Should().HaveCount(2);
        result.Rows[0].Method.Should().Be("Aaa");
        result.Rows[1].Method.Should().Be("Zzz");
    }

    [TestMethod]
    public void CallersOf_FocusAtStackRoot_CreditsRootCaller()
    {
        // Program.Main is the outermost frame, so its only caller is the synthetic <root>.
        CallersResult result = LoadFolding().Aggregator.CallersOf("Program.Main", "", 25);

        result.Callers.Should().ContainSingle();
        result.Callers[0].Caller.Should().Be("<root>");
        result.Callers[0].Weight.Should().Be(25.0);
    }

    [TestMethod]
    public void SourceHeatmap_BucketsLeafSamplesByLineFoldingHelperLeaves()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Run"], 3.0, "1", ["Engine.cs:20"]),
            new(["app!Run", "app!WriteBarrier"], 4.0, "1", ["Engine.cs:10", "Helpers.cs:99"]),
            new(["app!Other"], 8.0, "1", ["Other.cs:1"])
        ];

        SourceHeatmapResult result = Engine(samples).SourceHeatmap("Engine.cs", FrameNames.DefaultFoldPatterns);

        // Percent is over the whole trace (20 ms), file total is the 12 ms in Engine.cs.
        result.ScopeWeight.Should().Be(20.0);
        result.File.Should().Be("Engine.cs");
        result.FileWeight.Should().Be(12.0);

        result.Lines.Should().HaveCount(2);
        // Ordered by line number, not by time.
        result.Lines[0].Line.Should().Be(10);
        result.Lines[0].Weight.Should().Be(9.0);
        result.Lines[0].SampleCount.Should().Be(2);
        result.Lines[0].Method.Should().Be("Run");
        result.Lines[0].PercentOfScope.Should().Be(45.0);
        result.Lines[1].Line.Should().Be(20);
        result.Lines[1].Weight.Should().Be(3.0);
    }

    [TestMethod]
    public void SourceHeatmap_MatchesFileNameCaseInsensitively()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"])
        ];

        SourceHeatmapResult result = Engine(samples).SourceHeatmap("ENGINE.CS", FrameNames.DefaultFoldPatterns);

        result.Lines.Should().ContainSingle();
        // The file name keeps the casing recorded in the trace.
        result.File.Should().Be("Engine.cs");
    }

    [TestMethod]
    public void SourceHeatmap_SamplesWithoutLocationsOrOtherFiles_AreExcluded()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Run"], 9.0, "1"),
            new(["app!Run"], 4.0, "1", [""]),
            new(["app!Run"], 2.0, "1", ["Other.cs:3"])
        ];

        SourceHeatmapResult result = Engine(samples).SourceHeatmap("Engine.cs", FrameNames.DefaultFoldPatterns);

        // Whole-trace total still counts every sample; only Engine.cs contributes lines.
        result.ScopeWeight.Should().Be(20.0);
        result.FileWeight.Should().Be(5.0);
        result.Lines.Should().ContainSingle();
        result.Lines[0].Line.Should().Be(10);
    }

    [TestMethod]
    public void SourceHeatmap_NoMatchingFile_ReturnsEmptyLines()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"])
        ];

        SourceHeatmapResult result = Engine(samples).SourceHeatmap("Missing.cs", FrameNames.DefaultFoldPatterns);

        result.Lines.Should().BeEmpty();
        result.FileWeight.Should().Be(0.0);
        result.ScopeWeight.Should().Be(5.0);
    }

    [TestMethod]
    public void SourceHeatmap_CompetingMethodsOnOneLine_ReportsDominantByTime()
    {
        // Two methods resolve to the same Engine.cs:10; the heaviest one dominates.
        List<SampleStack> samples =
        [
            new(["app!Run"], 3.0, "1", ["Engine.cs:10"]),
            new(["app!Helper"], 7.0, "1", ["Engine.cs:10"])
        ];

        SourceHeatmapResult result = Engine(samples).SourceHeatmap("Engine.cs", FrameNames.DefaultFoldPatterns);

        result.Lines.Should().ContainSingle();
        result.Lines[0].Line.Should().Be(10);
        result.Lines[0].Weight.Should().Be(10.0);
        result.Lines[0].SampleCount.Should().Be(2);
        // Helper (7 ms) outweighs Run (3 ms) for the line.
        result.Lines[0].Method.Should().Be("Helper");
    }

    [TestMethod]
    [DataRow("Engine.cs")]
    [DataRow("Engine.cs:")]
    [DataRow("Engine.cs:abc")]
    [DataRow(":10")]
    [DataRow("Engine.cs:0")]
    [DataRow("Engine.cs:-1")]
    public void SourceHeatmap_MalformedLeafLocation_IsExcluded(string location)
    {
        // A location with no colon, a trailing colon, a non-numeric line, no file
        // name, or a non-positive (non 1-based) line cannot be split into file:line
        // and must not contribute to any file.
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", [location])
        ];

        SourceHeatmapResult result = Engine(samples).SourceHeatmap("Engine.cs", FrameNames.DefaultFoldPatterns);

        result.Lines.Should().BeEmpty();
        result.FileWeight.Should().Be(0.0);
        // The sample still counts toward the whole-trace total.
        result.ScopeWeight.Should().Be(5.0);
    }
}
