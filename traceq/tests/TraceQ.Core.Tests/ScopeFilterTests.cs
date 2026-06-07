// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing.Providers;

namespace TraceQ.Tracing;

[TestClass]
public sealed class ScopeFilterTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static StackSampleSource ThreeStacks() =>
        new(
            MetricInfo.Cpu,
            [
                new SampleStack(["app!Program.Main", "app!Engine.Run"], 10.0, "1"),
                new SampleStack(["app!Program.Main", "noise!Idle.Spin"], 5.0, "1"),
                new SampleStack(["app!Program.Main", "app!Engine.Run", "lib!Helper.Do"], 3.0, "1")
            ]);

    [TestMethod]
    public void Apply_EmptyFilter_ReturnsSameInstance()
    {
        StackSampleSource source = ThreeStacks();

        ScopeFilter.None.Apply(source).Should().BeSameAs(source);
    }

    [TestMethod]
    public void Apply_Include_KeepsOnlySamplesWithAMatchingFrame()
    {
        StackSampleSource source = ThreeStacks();
        ScopeFilter filter = new(["Engine.Run"], []);

        StackSampleSource result = filter.Apply(source);

        // The two stacks containing Engine.Run survive; the Idle.Spin stack is dropped.
        result.Samples.Should().HaveCount(2);
        result.Samples.Should().OnlyContain(s => s.Frames.Any(f => f.Contains("Engine.Run", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Apply_Exclude_DropsSamplesWithAMatchingFrame()
    {
        StackSampleSource source = ThreeStacks();
        ScopeFilter filter = new([], ["Idle"]);

        StackSampleSource result = filter.Apply(source);

        result.Samples.Should().HaveCount(2);
        result.Samples.Should().NotContain(s => s.Frames.Any(f => f.Contains("Idle", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Apply_ExcludeWinsOverInclude()
    {
        StackSampleSource source = ThreeStacks();
        // The third stack matches both include (Engine.Run) and exclude (Helper); exclude wins.
        ScopeFilter filter = new(["Engine.Run"], ["Helper"]);

        StackSampleSource result = filter.Apply(source);

        result.Samples.Should().ContainSingle();
        result.Samples[0].Frames.Should().NotContain(f => f.Contains("Helper", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Apply_PreservesMetric()
    {
        StackSampleSource source = new(MetricInfo.Allocations, [new SampleStack(["a!B.C"], 1024.0, "1")]);

        StackSampleSource result = new ScopeFilter(["B.C"], []).Apply(source);

        result.Metric.Should().Be(MetricInfo.Allocations);
    }

    [TestMethod]
    public void Apply_Regex_MatchesFrameNames()
    {
        StackSampleSource source = ThreeStacks();
        // Anchored alternation: keep stacks touching the app module's Engine or Program.
        ScopeFilter filter = new(["^app!Engine\\."], []);

        StackSampleSource result = filter.Apply(source);

        result.Samples.Should().HaveCount(2);
    }

    [TestMethod]
    public void Apply_InvalidPattern_ThrowsArgument()
    {
        StackSampleSource source = ThreeStacks();
        ScopeFilter filter = new(["(unclosed"], []);

        Action act = () => filter.Apply(source);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Apply_FilteredSource_RanksThroughTheEngineUnchanged()
    {
        // The filter composes with the engine: ranking a scoped allocation source
        // restricts the result to the kept stacks, with no aggregator change.
        StackSampleSource source = new AllocationProvider().Read(FixturePath("alloc.nettrace"));
        ScopeFilter filter = new(["AllocLoop"], []);

        StackSampleSource scoped = filter.Apply(source);
        scoped.Samples.Count.Should().BeLessThan(source.Samples.Count);

        RankingResult ranked = new FoldingAggregator(scoped).InclusiveTime("", FrameNames.DefaultFoldPatterns, 25);
        ranked.Rows.Should().Contain(r => r.Frame.Contains("AllocLoop", StringComparison.Ordinal));
    }
}
