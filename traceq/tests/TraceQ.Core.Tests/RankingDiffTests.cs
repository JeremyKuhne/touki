// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing.Providers;

namespace TraceQ.Tracing;

[TestClass]
public sealed class RankingDiffTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static RankingResult Ranking(double scope, params (string Frame, double Weight)[] rows) =>
        new(scope, "", rows.Select(r => new RankRow(r.Frame, r.Weight, scope > 0 ? 100.0 * r.Weight / scope : 0.0)).ToArray());

    [TestMethod]
    public void Diff_ComputesPerFrameDeltaAndScopeDelta()
    {
        RankingResult before = Ranking(100.0, ("A", 60.0), ("B", 40.0));
        RankingResult after = Ranking(150.0, ("A", 110.0), ("B", 40.0));

        RankingDiffResult diff = RankingDiff.Diff(before, after, 25);

        diff.BeforeScopeWeight.Should().Be(100.0);
        diff.AfterScopeWeight.Should().Be(150.0);
        diff.ScopeDelta.Should().Be(50.0);

        // B is unchanged and so is dropped; only A moved (+50).
        diff.Rows.Should().ContainSingle();
        diff.Rows[0].Frame.Should().Be("A");
        diff.Rows[0].BeforeWeight.Should().Be(60.0);
        diff.Rows[0].AfterWeight.Should().Be(110.0);
        diff.Rows[0].Delta.Should().Be(50.0);
    }

    [TestMethod]
    public void Diff_OrdersByAbsoluteChangeLargestFirst()
    {
        RankingResult before = Ranking(100.0, ("Small", 50.0), ("Big", 50.0));
        // Small drops 10, Big drops 40; Big is the larger change.
        RankingResult after = Ranking(50.0, ("Small", 40.0), ("Big", 10.0));

        RankingDiffResult diff = RankingDiff.Diff(before, after, 25);

        diff.Rows[0].Frame.Should().Be("Big");
        diff.Rows[0].Delta.Should().Be(-40.0);
        diff.Rows[1].Frame.Should().Be("Small");
        diff.Rows[1].Delta.Should().Be(-10.0);
    }

    [TestMethod]
    public void Diff_FrameOnlyInAfter_IsANewRegressionFromZero()
    {
        RankingResult before = Ranking(50.0, ("A", 50.0));
        RankingResult after = Ranking(80.0, ("A", 50.0), ("New", 30.0));

        RankingDiffResult diff = RankingDiff.Diff(before, after, 25);

        diff.Rows.Should().ContainSingle();
        diff.Rows[0].Frame.Should().Be("New");
        diff.Rows[0].BeforeWeight.Should().Be(0.0);
        diff.Rows[0].AfterWeight.Should().Be(30.0);
        diff.Rows[0].Delta.Should().Be(30.0);
    }

    [TestMethod]
    public void Diff_FrameOnlyInBefore_IsAnImprovementToZero()
    {
        RankingResult before = Ranking(80.0, ("A", 50.0), ("Gone", 30.0));
        RankingResult after = Ranking(50.0, ("A", 50.0));

        RankingDiffResult diff = RankingDiff.Diff(before, after, 25);

        diff.Rows.Should().ContainSingle();
        diff.Rows[0].Frame.Should().Be("Gone");
        diff.Rows[0].BeforeWeight.Should().Be(30.0);
        diff.Rows[0].AfterWeight.Should().Be(0.0);
        diff.Rows[0].Delta.Should().Be(-30.0);
    }

    [TestMethod]
    public void Diff_IdenticalRankings_HaveNoChangedRows()
    {
        RankingResult ranking = Ranking(100.0, ("A", 60.0), ("B", 40.0));

        RankingDiffResult diff = RankingDiff.Diff(ranking, ranking, 25);

        diff.ScopeDelta.Should().Be(0.0);
        diff.Rows.Should().BeEmpty();
    }

    [TestMethod]
    public void Diff_TopLimit_KeepsTheLargestChanges()
    {
        RankingResult before = Ranking(0.0, ("A", 0.0), ("B", 0.0), ("C", 0.0));
        RankingResult after = Ranking(60.0, ("A", 10.0), ("B", 20.0), ("C", 30.0));

        RankingDiffResult diff = RankingDiff.Diff(before, after, 2);

        diff.Rows.Should().HaveCount(2);
        diff.Rows[0].Frame.Should().Be("C");
        diff.Rows[1].Frame.Should().Be("B");
    }

    [TestMethod]
    public void Diff_TiedMagnitude_BreaksByFrameNameForDeterminism()
    {
        RankingResult before = Ranking(0.0, ("Zzz", 0.0), ("Aaa", 0.0));
        RankingResult after = Ranking(20.0, ("Zzz", 10.0), ("Aaa", 10.0));

        RankingDiffResult diff = RankingDiff.Diff(before, after, 25);

        diff.Rows[0].Frame.Should().Be("Aaa");
        diff.Rows[1].Frame.Should().Be("Zzz");
    }

    [TestMethod]
    public void Diff_ComposesWithFilterAndEngine_OverARealFixture()
    {
        // A real before/after: the full allocation ranking versus the same ranking
        // scoped to the AllocLoop subtree. Frames the filter drops show up as
        // improvements (their allocation goes to zero in the "after").
        StackSampleSource source = new AllocationProvider().Read(FixturePath("alloc.nettrace"));
        RankingResult before = new FoldingAggregator(source).InclusiveTime("", FrameNames.DefaultFoldPatterns, 100);

        StackSampleSource scoped = new ScopeFilter(["AllocLoop"], []).Apply(source);
        RankingResult after = new FoldingAggregator(scoped).InclusiveTime("", FrameNames.DefaultFoldPatterns, 100);

        RankingDiffResult diff = RankingDiff.Diff(before, after, 50);

        // Scoping can only remove samples, so the after total is no larger.
        diff.AfterScopeWeight.Should().BeLessThanOrEqualTo(diff.BeforeScopeWeight);
        diff.Rows.Should().NotBeEmpty();
        // The rows are ordered by absolute change, largest first.
        for (int i = 1; i < diff.Rows.Count; i++)
        {
            Math.Abs(diff.Rows[i].Delta).Should().BeLessThanOrEqualTo(Math.Abs(diff.Rows[i - 1].Delta));
        }
    }
}
