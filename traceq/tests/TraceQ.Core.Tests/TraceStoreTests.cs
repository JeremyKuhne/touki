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
    public void Get_LoadsTraceWithExpectedInfo()
    {
        TraceStore store = new();

        LoadedTrace trace = store.Get(FixturePath("folding.speedscope.json"));

        trace.Info.Format.Should().Be(TraceFormat.Speedscope);
        trace.Info.SampleCount.Should().Be(4);
    }
}
