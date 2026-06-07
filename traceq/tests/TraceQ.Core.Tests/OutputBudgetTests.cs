// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Output;

[TestClass]
public sealed class OutputBudgetTests
{
    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(4, 1)]
    [DataRow(5, 2)]
    [DataRow(8, 2)]
    [DataRow(9, 3)]
    public void EstimateTokens_RoundsUpAtFourCharsPerToken(int length, int expected)
    {
        string text = new('x', length);
        OutputBudget.EstimateTokens(text).Should().Be(expected);
    }

    [TestMethod]
    public void IsOverBudget_UnderCeiling_False()
    {
        // 8 chars -> 2 tokens, under a ceiling of 3.
        OutputBudget.IsOverBudget(new string('x', 8), ceilingTokens: 3).Should().BeFalse();
    }

    [TestMethod]
    public void IsOverBudget_OverCeiling_True()
    {
        // 16 chars -> 4 tokens, over a ceiling of 3.
        OutputBudget.IsOverBudget(new string('x', 16), ceilingTokens: 3).Should().BeTrue();
    }

    [TestMethod]
    public void TryGetBudgetWarning_OverCeiling_ProducesRemediationWarning()
    {
        bool fired = OutputBudget.TryGetBudgetWarning(new string('x', 400), ceilingTokens: 10, out string? warning);

        fired.Should().BeTrue();
        warning.Should().NotBeNull();
        warning.Should().Contain("--top");
        warning.Should().Contain("budget");
    }

    [TestMethod]
    public void TryGetBudgetWarning_UnderCeiling_NoWarning()
    {
        bool fired = OutputBudget.TryGetBudgetWarning("small", ceilingTokens: 1000, out string? warning);

        fired.Should().BeFalse();
        warning.Should().BeNull();
    }

    [TestMethod]
    public void DefaultCeiling_Is25000()
    {
        OutputBudget.DefaultCeilingTokens.Should().Be(25_000);
    }
}
