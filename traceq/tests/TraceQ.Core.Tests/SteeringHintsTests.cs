// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Output;

[TestClass]
public sealed class SteeringHintsTests
{
    [TestMethod]
    public void ForRanking_WithRows_NudgesToHotFrameCallers()
    {
        RankingResult ranking = new(
            25.0,
            "",
            [
                new RankRow("MyApp.Inner", 16.0, 64.0),
                new RankRow("MyApp.Work", 4.0, 16.0)
            ]);

        IReadOnlyList<string> hints = SteeringHints.ForRanking(ranking);

        // The nudge names the engine verb and the hottest frame, matching the
        // output-contract golden's pinned hint.
        hints.Should().ContainSingle().Which.Should().Be("drill into the hot frame with: callers MyApp.Inner");
    }

    [TestMethod]
    public void ForRanking_Empty_NudgesToWidenScope()
    {
        RankingResult ranking = new(0.0, "", []);

        IReadOnlyList<string> hints = SteeringHints.ForRanking(ranking);

        hints.Should().ContainSingle().Which.Should().Contain("widen the filter");
    }

    [TestMethod]
    public void ForCallers_WithNamedCaller_NudgesUpTheStack()
    {
        CallersResult callers = new(
            "Inner",
            16.0,
            64.0,
            25.0,
            [
                new CallerRow("MyApp.Work", 12.0, 75.0),
                new CallerRow("MyApp.Other", 4.0, 25.0)
            ]);

        IReadOnlyList<string> hints = SteeringHints.ForCallers(callers);

        hints.Should().ContainSingle().Which.Should().Be("continue up the stack with: callers MyApp.Work");
    }

    [TestMethod]
    public void ForCallers_DominantCallerIsRoot_NudgesEntryPoint()
    {
        CallersResult callers = new(
            "Main",
            16.0,
            64.0,
            25.0,
            [new CallerRow("<root>", 16.0, 100.0)]);

        IReadOnlyList<string> hints = SteeringHints.ForCallers(callers);

        hints.Should().ContainSingle().Which.Should().Contain("top-level entry point");
    }

    [TestMethod]
    public void ForCallers_Empty_NudgesToWidenScope()
    {
        CallersResult callers = new("Nothing", 0.0, 0.0, 25.0, []);

        IReadOnlyList<string> hints = SteeringHints.ForCallers(callers);

        hints.Should().ContainSingle().Which.Should().Contain("widen the filter");
    }

    [TestMethod]
    public void ForDiff_WithChanges_NudgesToLargestChange()
    {
        RankingDiffResult diff = new(
            20.0,
            30.0,
            10.0,
            [
                new DiffRow("MyApp.Slow", 4.0, 12.0, 8.0),
                new DiffRow("MyApp.Fast", 6.0, 4.0, -2.0)
            ]);

        IReadOnlyList<string> hints = SteeringHints.ForDiff(diff);

        hints.Should().ContainSingle().Which.Should().Be("the largest change is MyApp.Slow; drill into it with: callers MyApp.Slow");
    }

    [TestMethod]
    public void ForDiff_NoChanges_NotesTheMatch()
    {
        RankingDiffResult diff = new(20.0, 20.0, 0.0, []);

        IReadOnlyList<string> hints = SteeringHints.ForDiff(diff);

        hints.Should().ContainSingle().Which.Should().Contain("no frames changed");
    }

    [TestMethod]
    public void ForRanking_Null_ThrowsArgumentNull()
    {
        Action act = () => SteeringHints.ForRanking(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ForCallers_Null_ThrowsArgumentNull()
    {
        Action act = () => SteeringHints.ForCallers(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public void ForDiff_Null_ThrowsArgumentNull()
    {
        Action act = () => SteeringHints.ForDiff(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
