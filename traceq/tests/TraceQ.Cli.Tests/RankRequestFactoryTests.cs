// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

[TestClass]
public sealed class RankRequestFactoryTests
{
    [TestMethod]
    [DataRow("cpu", TraceMetric.Cpu)]
    [DataRow("CPU", TraceMetric.Cpu)]
    [DataRow("Cpu", TraceMetric.Cpu)]
    [DataRow("alloc", TraceMetric.Allocations)]
    [DataRow("Alloc", TraceMetric.Allocations)]
    [DataRow("allocations", TraceMetric.Allocations)]
    [DataRow("exceptions", TraceMetric.Exceptions)]
    [DataRow("Exceptions", TraceMetric.Exceptions)]
    [DataRow("threadtime", TraceMetric.ThreadTime)]
    [DataRow("ThreadTime", TraceMetric.ThreadTime)]
    public void TryResolveMetric_KnownMetric_ResolvesProvider(string metric, TraceMetric expected)
    {
        RankRequestFactory.TryResolveMetric(metric, out TraceMetric resolved).Should().BeTrue();
        resolved.Should().Be(expected);
    }

    [TestMethod]
    [DataRow("gcstats")]
    [DataRow("bogus")]
    [DataRow("")]
    public void TryResolveMetric_UnknownMetric_IsFalse(string metric)
    {
        // gcstats is a planned report provider (not a stack metric), so it resolves as
        // unknown to the ranking verbs until its own verb lands.
        RankRequestFactory.TryResolveMetric(metric, out _).Should().BeFalse();
    }

    [TestMethod]
    public void Create_NullFold_UsesDefaultFoldPatterns()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace", TraceMetric.Cpu, Measure.Self, root: "", top: 25, fold: null, symbols: null, OutputFormat.Text, strict: false);

        request.Fold.Should().BeSameAs(FrameNames.DefaultFoldPatterns);
    }

    [TestMethod]
    public void Create_EmptyFold_UsesDefaultFoldPatterns()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace", TraceMetric.Cpu, Measure.Self, root: "", top: 25, fold: [], symbols: null, OutputFormat.Text, strict: false);

        request.Fold.Should().BeSameAs(FrameNames.DefaultFoldPatterns);
    }

    [TestMethod]
    public void Create_ExplicitFold_IsUsed()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace", TraceMetric.Cpu, Measure.Self, root: "", top: 25, fold: ["^A", "^B"], symbols: null, OutputFormat.Text, strict: false);

        request.Fold.Should().Equal("^A", "^B");
    }

    [TestMethod]
    public void Create_MapsAllFields()
    {
        RankRequest request = RankRequestFactory.Create(
            "t.nettrace",
            TraceMetric.Allocations,
            Measure.Inclusive,
            root: "MoveNext",
            top: 10,
            fold: null,
            symbols: "bin/net10.0",
            OutputFormat.Json,
            strict: true);

        request.Path.Should().Be("t.nettrace");
        request.Metric.Should().Be(TraceMetric.Allocations);
        request.Measure.Should().Be(Measure.Inclusive);
        request.Root.Should().Be("MoveNext");
        request.Top.Should().Be(10);
        request.Symbols.Should().Be("bin/net10.0");
        request.Format.Should().Be(OutputFormat.Json);
        request.Strict.Should().BeTrue();
    }

    [TestMethod]
    public void TryResolveScope_NoOptions_IsAutomatic()
    {
        RankRequestFactory.TryResolveScope("", allProcesses: false, out ScopeRequest scope, out string? error)
            .Should().BeTrue();
        error.Should().BeNull();
        scope.Should().BeSameAs(ScopeRequest.Auto);
    }

    [TestMethod]
    public void TryResolveScope_AllProcesses_IsTheOptOut()
    {
        RankRequestFactory.TryResolveScope("", allProcesses: true, out ScopeRequest scope, out _)
            .Should().BeTrue();
        scope.Should().BeSameAs(ScopeRequest.AllProcesses);
    }

    [TestMethod]
    public void TryResolveScope_ProcessName_BuildsAnExplicitScope()
    {
        RankRequestFactory.TryResolveScope("MyApp", allProcesses: false, out ScopeRequest scope, out _)
            .Should().BeTrue();
        scope.ProcessName.Should().Be("MyApp");
        scope.IncludeAll.Should().BeFalse();
    }

    [TestMethod]
    public void TryResolveScope_BothOptions_IsAUsageError()
    {
        RankRequestFactory.TryResolveScope("MyApp", allProcesses: true, out _, out string? error)
            .Should().BeFalse();
        error.Should().Contain("only one of --process and --all-processes");
    }
}
