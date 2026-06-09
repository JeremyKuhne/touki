// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

[TestClass]
public sealed class RankRequestFactoryTests
{
    [TestMethod]
    [DataRow("cpu")]
    [DataRow("CPU")]
    [DataRow("Cpu")]
    public void IsCpuMetric_CpuInAnyCase_IsTrue(string metric)
    {
        RankRequestFactory.IsCpuMetric(metric).Should().BeTrue();
    }

    [TestMethod]
    [DataRow("alloc")]
    [DataRow("threadtime")]
    [DataRow("")]
    public void IsCpuMetric_OtherMetric_IsFalse(string metric)
    {
        RankRequestFactory.IsCpuMetric(metric).Should().BeFalse();
    }

    [TestMethod]
    public void Create_NullFold_UsesDefaultFoldPatterns()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace", Measure.Self, root: "", top: 25, fold: null, symbols: null, OutputFormat.Text, strict: false);

        request.Fold.Should().BeSameAs(FrameNames.DefaultFoldPatterns);
    }

    [TestMethod]
    public void Create_EmptyFold_UsesDefaultFoldPatterns()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace", Measure.Self, root: "", top: 25, fold: [], symbols: null, OutputFormat.Text, strict: false);

        request.Fold.Should().BeSameAs(FrameNames.DefaultFoldPatterns);
    }

    [TestMethod]
    public void Create_ExplicitFold_IsUsed()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace", Measure.Self, root: "", top: 25, fold: ["^A", "^B"], symbols: null, OutputFormat.Text, strict: false);

        request.Fold.Should().Equal("^A", "^B");
    }

    [TestMethod]
    public void Create_MapsAllFields()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace",
            Measure.Inclusive,
            root: "MoveNext",
            top: 10,
            fold: null,
            symbols: "bin/net10.0",
            OutputFormat.Json,
            strict: true);

        request.Path.Should().Be("t.nettrace");
        request.Measure.Should().Be(Measure.Inclusive);
        request.Root.Should().Be("MoveNext");
        request.Top.Should().Be(10);
        request.Symbols.Should().Be("bin/net10.0");
        request.Format.Should().Be(OutputFormat.Json);
        request.Strict.Should().BeTrue();
    }
}
