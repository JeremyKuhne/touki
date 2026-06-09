// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class CallTreeTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static LoadedTrace LoadFolding() =>
        new TraceLoader().Load(FixturePath("folding.speedscope.json"));

    private static FoldingAggregator Engine(IReadOnlyList<SampleStack> samples) =>
        new(new StackSampleSource(MetricInfo.Cpu, samples));

    private static TreeNode Child(TreeNode node, string frame) =>
        node.Children.Single(c => c.Frame == frame);

    [TestMethod]
    public void CallTree_RootCarriesScopedTotal()
    {
        CallTreeResult result = LoadFolding().Aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 10, 0.0);

        result.ScopeWeight.Should().Be(25.0);
        result.Root.Frame.Should().Be("<root>");
        result.Root.Weight.Should().Be(25.0);
        result.Root.PercentOfScope.Should().Be(100.0);
    }

    [TestMethod]
    public void CallTree_BuildsTopDownHierarchyWithInclusiveWeights()
    {
        CallTreeResult result = LoadFolding().Aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 10, 0.0);

        // <root>(25) -> Program.Main(25) -> { MyApp.Work(20) -> MyApp.Inner(16); MyApp.Other(5) }
        TreeNode main = Child(result.Root, "Program.Main");
        main.Weight.Should().Be(25.0);

        TreeNode work = Child(main, "MyApp.Work");
        work.Weight.Should().Be(20.0);
        work.PercentOfScope.Should().Be(80.0);

        TreeNode inner = Child(work, "MyApp.Inner");
        inner.Weight.Should().Be(16.0);
        inner.PercentOfScope.Should().Be(64.0);

        TreeNode other = Child(main, "MyApp.Other");
        other.Weight.Should().Be(5.0);
    }

    [TestMethod]
    public void CallTree_FoldedFramesAreSkipped()
    {
        CallTreeResult result = LoadFolding().Aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 10, 0.0);

        TreeNode main = Child(result.Root, "Program.Main");
        TreeNode work = Child(main, "MyApp.Work");

        // CPU_TIME and WriteBarrier are folded, so they never appear as nodes.
        work.Children.Should().NotContain(c => c.Frame == "CPU_TIME");
        work.Children.Should().NotContain(c => c.Frame == "WriteBarrier");
    }

    [TestMethod]
    public void CallTree_ChildrenOrderedByWeightDescending()
    {
        CallTreeResult result = LoadFolding().Aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 10, 0.0);

        TreeNode main = Child(result.Root, "Program.Main");

        // Work (20) before Other (5).
        main.Children[0].Frame.Should().Be("MyApp.Work");
        main.Children[1].Frame.Should().Be("MyApp.Other");
    }

    [TestMethod]
    public void CallTree_MaxDepthZero_ReturnsRootOnly()
    {
        CallTreeResult result = LoadFolding().Aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 0, 0.0);

        result.Root.Weight.Should().Be(25.0);
        result.Root.Children.Should().BeEmpty();
    }

    [TestMethod]
    public void CallTree_MaxDepth_CapsDescent()
    {
        // Depth 1 keeps the root's direct children (Program.Main) but not their callees.
        CallTreeResult result = LoadFolding().Aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 1, 0.0);

        TreeNode main = Child(result.Root, "Program.Main");
        main.Children.Should().BeEmpty();
    }

    [TestMethod]
    public void CallTree_MinPercent_PrunesSmallBranches()
    {
        // MyApp.Other is 20% of scope; a 25% threshold prunes it but keeps Work (80%).
        CallTreeResult result = LoadFolding().Aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 10, 25.0);

        TreeNode main = Child(result.Root, "Program.Main");
        main.Children.Should().ContainSingle(c => c.Frame == "MyApp.Work");
        main.Children.Should().NotContain(c => c.Frame == "MyApp.Other");
    }

    [TestMethod]
    public void CallTree_RootFrame_ScopesToSubtree()
    {
        // Scoping to MyApp.Work roots the visible frames at Work; Program.Main is above it.
        CallTreeResult result = LoadFolding().Aggregator.CallTree("MyApp.Work", FrameNames.DefaultFoldPatterns, 10, 0.0);

        result.RootFrame.Should().Be("MyApp.Work");
        TreeNode work = Child(result.Root, "MyApp.Work");
        work.Weight.Should().Be(20.0);
        work.Children.Should().Contain(c => c.Frame == "MyApp.Inner");
    }

    [TestMethod]
    public void CallTree_Recursion_NestsTheRepeatedFrame()
    {
        // A -> B -> A: the second A is a node under B, not merged with the first.
        List<SampleStack> samples = [new(["app!A", "app!B", "app!A"], 3.0, "1")];

        CallTreeResult result = Engine(samples).CallTree("", FrameNames.DefaultFoldPatterns, 10, 0.0);

        TreeNode a1 = Child(result.Root, "A");
        TreeNode b = Child(a1, "B");
        TreeNode a2 = Child(b, "A");
        a2.Weight.Should().Be(3.0);
        a2.Children.Should().BeEmpty();
    }

    [TestMethod]
    public void CallTree_NegativeMaxDepth_Throws()
    {
        FoldingAggregator aggregator = LoadFolding().Aggregator;

        Action act = () => aggregator.CallTree("", FrameNames.DefaultFoldPatterns, -1, 0.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [TestMethod]
    public void CallTree_NegativeMinPercent_Throws()
    {
        FoldingAggregator aggregator = LoadFolding().Aggregator;

        Action act = () => aggregator.CallTree("", FrameNames.DefaultFoldPatterns, 10, -1.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
