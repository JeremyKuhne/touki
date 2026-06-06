// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ;

namespace TraceQ.Parity.Tests;

/// <summary>
///  Numeric parity harness skeleton (implementation-plan section 3). Once M1
///  exits, these tests assert that <c>traceq</c>'s self / inclusive / callers
///  rankings match the frozen legacy oracles - <c>Get-TraceHotspots.ps1</c> and
///  the <c>touki.mcp analyze</c> output - on a fixed fixture corpus, within a
///  small relative tolerance and identical top-N ordering. Treated as processes
///  whose output is compared, never as project references (keeps the coupling
///  graph empty by construction).
/// </summary>
[TestClass]
public sealed class ParityTests
{
    [TestMethod]
    public void Harness_IsWired_AgainstCore()
    {
        // M0 smoke: the parity project compiles and references the core. Replaced
        // by the real corpus comparison in M1 (this keeps the project running at
        // least one test so the runner does not report "zero tests ran").
        TraceQCore.Milestone.Should().Be("M0");
    }

    [TestMethod]
    [Ignore("Parity corpus and oracle comparison arrive with the M1 asset copy (plan section 3).")]
    public void Rank_MatchesLegacyOracle_WithinTolerance()
    {
        // Placeholder: M1 wires the fixture corpus and the oracle comparison.
    }
}
