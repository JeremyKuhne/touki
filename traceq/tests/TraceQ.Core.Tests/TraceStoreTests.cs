// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Server;

[TestClass]
public sealed class TraceStoreTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [TestMethod]
    public void Get_SamePath_ReturnsCachedInstance()
    {
        TraceStore store = new();
        string path = FixturePath("folding.speedscope.json");

        LoadedTrace first = store.Get(path);
        LoadedTrace second = store.Get(path);

        second.Should().BeSameAs(first);
    }

    [TestMethod]
    public void Get_RelativeAndAbsolutePath_ShareCacheEntry()
    {
        TraceStore store = new();
        string absolute = FixturePath("folding.speedscope.json");
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), absolute);

        // Guard against a degenerate run where the two spellings come out identical:
        // the point is that a genuinely relative path and its absolute form collapse
        // onto a single cache entry.
        relative.Should().NotBe(absolute);

        LoadedTrace viaAbsolute = store.Get(absolute);
        LoadedTrace viaRelative = store.Get(relative);

        viaRelative.Should().BeSameAs(viaAbsolute);
    }

    [TestMethod]
    public void Get_DifferentSymbolsKey_CachesSeparately()
    {
        TraceStore store = new();
        string path = FixturePath("folding.speedscope.json");

        LoadedTrace withoutSymbols = store.Get(path);
        LoadedTrace withSymbols = store.Get(path, AppContext.BaseDirectory);

        withSymbols.Should().NotBeSameAs(withoutSymbols);
    }

    [TestMethod]
    public void Get_DifferentMetricKey_CachesSeparately()
    {
        TraceStore store = new();
        // A .nettrace can be read as either the CPU view or the allocation view, so the
        // same path under two metrics must key to two distinct cache entries - each
        // carrying its own provider source - rather than collapsing onto one.
        string path = FixturePath("alloc.nettrace");

        LoadedTrace cpu = store.Get(path, metric: TraceMetric.Cpu);
        LoadedTrace allocations = store.Get(path, metric: TraceMetric.Allocations);

        allocations.Should().NotBeSameAs(cpu);
        cpu.Source.Metric.Should().Be(MetricInfo.Cpu);
        allocations.Source.Metric.Should().Be(MetricInfo.Allocations);
    }

    [TestMethod]
    public void Get_SameMetric_ReturnsCachedInstance()
    {
        TraceStore store = new();
        string path = FixturePath("alloc.nettrace");

        LoadedTrace first = store.Get(path, metric: TraceMetric.Allocations);
        LoadedTrace second = store.Get(path, metric: TraceMetric.Allocations);

        second.Should().BeSameAs(first);
    }

    [TestMethod]
    public void Get_LoadsTraceWithExpectedInfo()
    {
        TraceStore store = new();

        LoadedTrace trace = store.Get(FixturePath("folding.speedscope.json"));

        trace.Info.Format.Should().Be(TraceFormat.Speedscope);
        trace.Info.SampleCount.Should().Be(4);
    }

    [TestMethod]
    public void Get_BeyondCapacity_EvictsLeastRecentlyUsedTrace()
    {
        // A capacity-1 store can hold one trace; loading a second distinct cache
        // entry evicts the first, so re-loading it produces a fresh instance.
        TraceStore store = new(capacity: 1);
        string path = FixturePath("folding.speedscope.json");

        LoadedTrace first = store.Get(path, AppContext.BaseDirectory);
        // A different symbols key is a separate cache entry; loading it evicts the first.
        store.Get(path, Path.GetTempPath());

        LoadedTrace reloaded = store.Get(path, AppContext.BaseDirectory);

        reloaded.Should().NotBeSameAs(first);
    }
}
