// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Mcp.Tracing;

public class FoldingAggregatorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static LoadedTrace LoadFolding() =>
        new TraceLoader().Load(FixturePath("folding.speedscope.json"));

    private static Dictionary<string, double> ByFrame(RankingResult result)
    {
        Dictionary<string, double> map = new(StringComparer.Ordinal);
        foreach (RankRow row in result.Rows)
        {
            map[row.Frame] = row.Milliseconds;
        }

        return map;
    }

    [Test]
    public void Load_SpeedscopeFixture_ReportsFormatDurationAndSamples()
    {
        LoadedTrace trace = LoadFolding();
        trace.Info.Format.Should().Be(TraceFormat.Speedscope);
        trace.Info.SampleCount.Should().Be(4);
        trace.Info.DurationMs.Should().Be(25.0);
        trace.Info.SymbolResolutionRate.Should().Be(1.0);
    }

    [Test]
    public void SelfTime_FoldsHelperLeavesIntoNearestRealLeaf()
    {
        RankingResult result = LoadFolding().Aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 25);
        result.ScopeMilliseconds.Should().Be(25.0);

        Dictionary<string, double> self = ByFrame(result);
        // CPU_TIME folds into MyApp.Inner (10) plus the direct Inner sample (6) = 16.
        self["MyApp.Inner"].Should().Be(16.0);
        // WriteBarrier folds into MyApp.Work (4).
        self["MyApp.Work"].Should().Be(4.0);
        self["MyApp.Other"].Should().Be(5.0);
        self.Should().NotContainKey("CPU_TIME");
        self.Should().NotContainKey("WriteBarrier");
    }

    [Test]
    public void SelfTime_TopFrameByMilliseconds_IsRankedFirst()
    {
        RankingResult result = LoadFolding().Aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, 25);
        result.Rows[0].Frame.Should().Be("MyApp.Inner");
        result.Rows[0].PercentOfScope.Should().Be(64.0);
    }

    [Test]
    public void InclusiveTime_CreditsEachDistinctFrameOncePerSample()
    {
        RankingResult result = LoadFolding().Aggregator.InclusiveTime("", FrameNames.DefaultFoldPatterns, 25);
        result.ScopeMilliseconds.Should().Be(25.0);

        Dictionary<string, double> incl = ByFrame(result);
        incl["Program.Main"].Should().Be(25.0);
        incl["MyApp.Work"].Should().Be(20.0);
        incl["MyApp.Inner"].Should().Be(16.0);
        incl["MyApp.Other"].Should().Be(5.0);
        incl.Should().NotContainKey("CPU_TIME");
        incl.Should().NotContainKey("WriteBarrier");
    }

    [Test]
    public void CallersOf_ReportsImmediateCallerAndScopePercent()
    {
        CallersResult result = LoadFolding().Aggregator.CallersOf("MyApp.Inner", "", 25);
        result.TargetMilliseconds.Should().Be(16.0);
        result.ScopeMilliseconds.Should().Be(25.0);
        result.PercentOfScope.Should().Be(64.0);

        result.Callers.Should().ContainSingle();
        result.Callers[0].Caller.Should().Be("MyApp.Work");
        result.Callers[0].Milliseconds.Should().Be(16.0);
        result.Callers[0].PercentOfTarget.Should().Be(100.0);
    }

    [Test]
    public void SelfTime_RootScoping_ExcludesSamplesWithoutRootFrame()
    {
        RankingResult result = LoadFolding().Aggregator.SelfTime("MyApp.Work", FrameNames.DefaultFoldPatterns, 25);
        // The MyApp.Other sample (5 ms) has no MyApp.Work frame and is excluded.
        result.ScopeMilliseconds.Should().Be(20.0);

        Dictionary<string, double> self = ByFrame(result);
        self["MyApp.Inner"].Should().Be(16.0);
        self["MyApp.Work"].Should().Be(4.0);
        self.Should().NotContainKey("MyApp.Other");
    }

    [Test]
    public void InclusiveTime_RootScoping_StartsFromRootFrame()
    {
        RankingResult result = LoadFolding().Aggregator.InclusiveTime("MyApp.Work", FrameNames.DefaultFoldPatterns, 25);
        result.ScopeMilliseconds.Should().Be(20.0);

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
            map[row.Location] = row.Milliseconds;
        }

        return map;
    }

    [Test]
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

        LineRankingResult result = new FoldingAggregator(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 25);

        result.ScopeMilliseconds.Should().Be(12.0);
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

    [Test]
    public void HotLines_MethodFilter_ExcludesNonMatchingLeafMethods()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Other"], 7.0, "1", ["Other.cs:42"])
        ];

        LineRankingResult result = new FoldingAggregator(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 25);

        result.ScopeMilliseconds.Should().Be(5.0);
        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("Engine.cs:10");
    }

    [Test]
    public void HotLines_SamplesWithoutLocations_AreIgnored()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Run"], 9.0, "1")
        ];

        LineRankingResult result = new FoldingAggregator(samples).HotLines("", FrameNames.DefaultFoldPatterns, 25);

        result.ScopeMilliseconds.Should().Be(5.0);
        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("Engine.cs:10");
    }

    [Test]
    public void HotLines_UnresolvedLeafLocation_BucketsAsNoSource()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", [""])
        ];

        LineRankingResult result = new FoldingAggregator(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 25);

        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("<no source>");
        result.Rows[0].Milliseconds.Should().Be(5.0);
    }

    [Test]
    public void HotLines_TopLimit_TruncatesToHottestRows()
    {
        List<SampleStack> samples =
        [
            new(["app!Run"], 5.0, "1", ["Engine.cs:10"]),
            new(["app!Run"], 3.0, "1", ["Engine.cs:20"])
        ];

        LineRankingResult result = new FoldingAggregator(samples).HotLines("Run", FrameNames.DefaultFoldPatterns, 1);

        result.Rows.Should().ContainSingle();
        result.Rows[0].Location.Should().Be("Engine.cs:10");
    }

    [Test]
    public void CallersOf_FocusAtStackRoot_CreditsRootCaller()
    {
        // Program.Main is the outermost frame, so its only caller is the synthetic <root>.
        CallersResult result = LoadFolding().Aggregator.CallersOf("Program.Main", "", 25);

        result.Callers.Should().ContainSingle();
        result.Callers[0].Caller.Should().Be("<root>");
        result.Callers[0].Milliseconds.Should().Be(25.0);
    }
}
