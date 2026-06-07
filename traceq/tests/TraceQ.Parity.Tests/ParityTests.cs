// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;
using TraceQ;
using TraceQ.Tracing;

namespace TraceQ.Parity.Tests;

/// <summary>
///  Numeric parity harness (implementation-plan section 3): asserts that
///  <c>traceq</c>'s rankings match the frozen legacy oracle
///  (<c>Get-TraceHotspots.ps1</c>) on a fixed fixture, within a small relative
///  tolerance and identical top-N ordering.
/// </summary>
/// <remarks>
///  <para>
///   The oracle is treated as a process whose output is compared, never as a
///   project reference: <c>traceq/fixtures/make-fixtures.ps1</c> captures the
///   EventPipe profile, runs the oracle once, and freezes its rankings to
///   <c>Fixtures/hotloop.oracle.json</c> alongside the captured
///   <c>hotloop.speedscope.json</c>. Both are committed and read here, so the
///   test stays self-contained (it survives the extraction rehearsal) and needs
///   no PowerShell at run time. Regenerating the corpus refreshes the fixture and
///   the golden together, keeping them a matched pair.
///  </para>
/// </remarks>
[TestClass]
public sealed class ParityTests
{
    // The oracle rounds its displayed milliseconds to 0.1 ms; traceq computes
    // full precision from the same samples. Allow that display rounding plus a
    // small relative margin, which is still tight enough to catch a real
    // divergence between the C# port and the PowerShell oracle.
    private const double RelativeTolerance = 0.01;
    private const double AbsoluteToleranceMs = 0.2;

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static LoadedTrace LoadHotLoop() =>
        new TraceLoader().Load(FixturePath("hotloop.speedscope.json"));

    private static OracleGolden LoadGolden()
    {
        string json = File.ReadAllText(FixturePath("hotloop.oracle.json"));
        return JsonSerializer.Deserialize<OracleGolden>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    [TestMethod]
    public void Harness_IsWired_AgainstCore()
    {
        TraceQCore.Milestone.Should().Be("M1");
    }

    [TestMethod]
    public void SelfTime_MatchesLegacyOracle_WithinTolerance()
    {
        OracleGolden golden = LoadGolden();
        golden.SelfTime.Should().NotBeEmpty("the frozen oracle golden must carry self-time rows");

        RankingResult result = LoadHotLoop().Aggregator.SelfTime("", FrameNames.DefaultFoldPatterns, golden.SelfTime.Count);

        // Compare the meaningful managed frames at the top of the ranking: same
        // frames, same order, milliseconds within tolerance of the oracle.
        int compared = Math.Min(5, golden.SelfTime.Count);
        for (int i = 0; i < compared; i++)
        {
            OracleRow expected = golden.SelfTime[i];
            RankRow actual = result.Rows[i];

            actual.Frame.Should().Be(
                expected.Frame,
                $"self-time rank {i} should name the same frame as the oracle");

            double tolerance = Math.Max(AbsoluteToleranceMs, expected.Milliseconds * RelativeTolerance);
            actual.Milliseconds.Should().BeApproximately(
                expected.Milliseconds,
                tolerance,
                $"self-time for '{expected.Frame}' should match the oracle within tolerance");
        }
    }

    private sealed record OracleRow(string Frame, double Milliseconds, double PercentOfScope);

    private sealed record OracleGolden(
        string Source,
        IReadOnlyList<OracleRow> SelfTime,
        IReadOnlyList<OracleRow> Inclusive);
}
