// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;

namespace Touki.Mcp.Server;

public class TraceToolsTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Fixture => FixturePath("folding.speedscope.json");

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Test]
    public void LoadTrace_ReportsFormatSamplesAndThreads()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.LoadTrace(store, Fixture));

        root.GetProperty("format").GetString().Should().Be("Speedscope");
        root.GetProperty("sampleCount").GetInt32().Should().Be(4);
        root.GetProperty("durationMs").GetDouble().Should().Be(25.0);
        root.GetProperty("symbolResolutionRate").GetDouble().Should().Be(1.0);
        root.GetProperty("threads")[0].GetProperty("thread").GetString().Should().Be("Worker");
    }

    [Test]
    public void HotspotsSelf_RanksFoldedSelfTime()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.HotspotsSelf(store, Fixture));

        root.GetProperty("scopeMilliseconds").GetDouble().Should().Be(25.0);
        root.GetProperty("rows")[0].GetProperty("frame").GetString().Should().Be("MyApp.Inner");
        root.GetProperty("rows")[0].GetProperty("milliseconds").GetDouble().Should().Be(16.0);
    }

    [Test]
    public void HotspotsSelf_RootFrame_ScopesRanking()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.HotspotsSelf(store, Fixture, rootFrame: "MyApp.Other"));

        root.GetProperty("rootFrame").GetString().Should().Be("MyApp.Other");
        root.GetProperty("scopeMilliseconds").GetDouble().Should().Be(5.0);
    }

    [Test]
    public void HotspotsSelf_Top_LimitsRows()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.HotspotsSelf(store, Fixture, top: 1));

        root.GetProperty("rows").GetArrayLength().Should().Be(1);
    }

    [Test]
    public void HotspotsInclusive_CreditsDistinctFrames()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.HotspotsInclusive(store, Fixture));

        root.GetProperty("scopeMilliseconds").GetDouble().Should().Be(25.0);
        root.GetProperty("rows")[0].GetProperty("frame").GetString().Should().Be("Program.Main");
        root.GetProperty("rows")[0].GetProperty("milliseconds").GetDouble().Should().Be(25.0);
    }

    [Test]
    public void CallersOf_ReportsImmediateCaller()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.CallersOf(store, Fixture, frame: "MyApp.Inner"));

        root.GetProperty("focus").GetString().Should().Be("MyApp.Inner");
        root.GetProperty("targetMilliseconds").GetDouble().Should().Be(16.0);
        root.GetProperty("callers")[0].GetProperty("caller").GetString().Should().Be("MyApp.Work");
    }

    [Test]
    public void HotLines_SpeedscopeHasNoLineData_ReturnsEmptyRanking()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.HotLines(store, Fixture));

        root.GetProperty("scopeMilliseconds").GetDouble().Should().Be(0.0);
        root.GetProperty("rows").GetArrayLength().Should().Be(0);
    }

    [Test]
    public void ListThreads_ReportsPerThreadSampleCounts()
    {
        TraceStore store = new();

        JsonElement root = Parse(TraceTools.ListThreads(store, Fixture));

        JsonElement thread = root.GetProperty("threads")[0];
        thread.GetProperty("thread").GetString().Should().Be("Worker");
        thread.GetProperty("sampleCount").GetInt32().Should().Be(4);
    }
}
