// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

[TestClass]
public sealed class SymbolGateTests
{
    [TestMethod]
    public void IsBelowThreshold_RateUnderThresholdWithSamples_True()
    {
        SymbolGate.IsBelowThreshold(0.5, sampleCount: 100).Should().BeTrue();
    }

    [TestMethod]
    public void IsBelowThreshold_RateAtThreshold_False()
    {
        // 0.8 is the floor: at or above it the gate does not fire.
        SymbolGate.IsBelowThreshold(SymbolGate.MinimumResolutionRate, sampleCount: 100).Should().BeFalse();
    }

    [TestMethod]
    public void IsBelowThreshold_ZeroSamples_False()
    {
        // No samples means nothing to resolve; a separate "no samples" warning covers it.
        SymbolGate.IsBelowThreshold(0.0, sampleCount: 0).Should().BeFalse();
    }

    [TestMethod]
    public void TryGetWarning_LowResolution_ProducesRemediationWarning()
    {
        bool fired = SymbolGate.TryGetWarning(0.5, sampleCount: 100, out string? warning);

        fired.Should().BeTrue();
        warning.Should().NotBeNull();
        warning.Should().Contain("--symbols");
        warning.Should().Contain("50%");
        warning.Should().Contain("80%");
    }

    [TestMethod]
    public void TryGetWarning_JustBelowThreshold_PercentageDoesNotContradictThreshold()
    {
        // 0.799 fires the gate; the percentage must not round up to 80% and read
        // "Only 80% ... (< 80%)". Truncation keeps it at 79%.
        bool fired = SymbolGate.TryGetWarning(0.799, sampleCount: 100, out string? warning);

        fired.Should().BeTrue();
        warning.Should().NotBeNull();
        warning.Should().Contain("79%");
        warning.Should().NotContain("Only 80%");
    }

    [TestMethod]
    public void TryGetWarning_AtOrAboveThreshold_NoWarning()
    {
        bool fired = SymbolGate.TryGetWarning(0.95, sampleCount: 100, out string? warning);

        fired.Should().BeFalse();
        warning.Should().BeNull();
    }

    [TestMethod]
    public void TryGetWarning_ZeroSamples_NoWarning()
    {
        bool fired = SymbolGate.TryGetWarning(0.0, sampleCount: 0, out string? warning);

        fired.Should().BeFalse();
        warning.Should().BeNull();
    }
}
