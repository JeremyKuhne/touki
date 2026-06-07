// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing.Providers;

namespace TraceQ.Tracing;

[TestClass]
public sealed class GroupTransformTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [TestMethod]
    public void Apply_EmptyTransform_ReturnsSameInstance()
    {
        StackSampleSource source = new(MetricInfo.Cpu, [new SampleStack(["app!A.B"], 1.0, "1")]);

        GroupTransform.None.Apply(source).Should().BeSameAs(source);
    }

    [TestMethod]
    public void Apply_CollapsesMatchedModuleFramesToModuleBox()
    {
        StackSampleSource source = new(
            MetricInfo.Cpu,
            [new SampleStack(["app!Program.Main", "lib!Helper.Do", "app!Engine.Run"], 10.0, "1")]);

        StackSampleSource result = new GroupTransform(["lib"]).Apply(source);

        // The lib frame collapses to "lib!"; the app frames are untouched.
        result.Samples[0].Frames.Should().Equal("app!Program.Main", "lib!", "app!Engine.Run");
    }

    [TestMethod]
    public void Apply_ConsecutiveSameModuleFrames_CollapseToOne()
    {
        StackSampleSource source = new(
            MetricInfo.Cpu,
            [new SampleStack(["app!Main", "noise!A", "noise!B", "noise!C", "app!Leaf"], 5.0, "1")]);

        StackSampleSource result = new GroupTransform(["noise"]).Apply(source);

        // The three adjacent noise frames become a single "noise!" node.
        result.Samples[0].Frames.Should().Equal("app!Main", "noise!", "app!Leaf");
    }

    [TestMethod]
    public void Apply_NonConsecutiveSameModule_StaysSeparate()
    {
        StackSampleSource source = new(
            MetricInfo.Cpu,
            [new SampleStack(["lib!A", "app!Middle", "lib!B"], 5.0, "1")]);

        StackSampleSource result = new GroupTransform(["lib"]).Apply(source);

        // The two lib frames are not adjacent, so both survive as "lib!".
        result.Samples[0].Frames.Should().Equal("lib!", "app!Middle", "lib!");
    }

    [TestMethod]
    public void Apply_FrameWithoutModulePrefix_IsNeverGrouped()
    {
        StackSampleSource source = new(
            MetricInfo.Cpu,
            [new SampleStack(["app!Main", "<root>", "CPU_TIME"], 5.0, "1")]);

        StackSampleSource result = new GroupTransform([".*"]).Apply(source);

        // Only app! is groupable; the prefixless frames pass through unchanged.
        result.Samples[0].Frames.Should().Equal("app!", "<root>", "CPU_TIME");
    }

    [TestMethod]
    public void Apply_PreservesMetricAndWeight()
    {
        StackSampleSource source = new(MetricInfo.Allocations, [new SampleStack(["lib!A.B"], 2048.0, "1")]);

        StackSampleSource result = new GroupTransform(["lib"]).Apply(source);

        result.Metric.Should().Be(MetricInfo.Allocations);
        result.Samples[0].Weight.Should().Be(2048.0);
    }

    [TestMethod]
    public void Apply_InvalidPattern_ThrowsArgumentNamingTheGroupContext()
    {
        StackSampleSource source = new(MetricInfo.Cpu, [new SampleStack(["app!A"], 1.0, "1")]);

        Action act = () => new GroupTransform(["(unclosed"]).Apply(source);

        act.Should().Throw<ArgumentException>().WithMessage("*group*");
    }

    [TestMethod]
    public void Apply_GroupedSource_RanksThroughTheEngine_WithAModuleNode()
    {
        // Grouping the runtime modules of the allocation trace collapses them into
        // module boxes the ranking then surfaces, with no aggregator change.
        StackSampleSource source = new AllocationProvider().Read(FixturePath("alloc.nettrace"));
        StackSampleSource grouped = new GroupTransform(["System.Private.CoreLib"]).Apply(source);

        RankingResult ranked = new FoldingAggregator(grouped).InclusiveTime("", FrameNames.DefaultFoldPatterns, 50);

        // A collapsed module box shortens (via FrameNames.Short) to the bare module
        // token, so the ranking carries the grouped node rather than its methods.
        ranked.Rows.Should().Contain(r => r.Frame.Contains("System.Private.CoreLib", StringComparison.Ordinal));
    }
}
