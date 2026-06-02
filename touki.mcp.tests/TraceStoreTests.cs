// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Touki.Mcp.Tracing;

namespace Touki.Mcp.Server;

public class TraceStoreTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Test]
    public void Get_SamePath_ReturnsCachedInstance()
    {
        TraceStore store = new();
        string path = FixturePath("folding.speedscope.json");

        LoadedTrace first = store.Get(path);
        LoadedTrace second = store.Get(path);

        second.Should().BeSameAs(first);
    }

    [Test]
    public void Get_RelativeAndAbsolutePath_ShareCacheEntry()
    {
        TraceStore store = new();
        string absolute = FixturePath("folding.speedscope.json");

        LoadedTrace viaAbsolute = store.Get(absolute);
        LoadedTrace viaFullPath = store.Get(Path.GetFullPath(absolute));

        viaFullPath.Should().BeSameAs(viaAbsolute);
    }

    [Test]
    public void Get_DifferentSymbolsKey_CachesSeparately()
    {
        TraceStore store = new();
        string path = FixturePath("folding.speedscope.json");

        LoadedTrace withoutSymbols = store.Get(path);
        LoadedTrace withSymbols = store.Get(path, AppContext.BaseDirectory);

        withSymbols.Should().NotBeSameAs(withoutSymbols);
    }

    [Test]
    public void Get_LoadsTraceWithExpectedInfo()
    {
        TraceStore store = new();

        LoadedTrace trace = store.Get(FixturePath("folding.speedscope.json"));

        trace.Info.Format.Should().Be(TraceFormat.Speedscope);
        trace.Info.SampleCount.Should().Be(4);
    }
}
