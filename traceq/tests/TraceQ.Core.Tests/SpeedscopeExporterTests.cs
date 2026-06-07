// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;
using TraceQ.Tracing;
using TraceQ.Tracing.Providers;

namespace TraceQ.Output;

[TestClass]
public sealed class SpeedscopeExporterTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static StackSampleSource SyntheticCpu() =>
        new(
            MetricInfo.Cpu,
            [
                new SampleStack(["Program.Main", "App.Work", "App.Inner"], 10.0, "1"),
                new SampleStack(["Program.Main", "App.Work"], 5.0, "1"),
                new SampleStack(["Program.Main", "<root>"], 3.0, "1")
            ]);

    [TestMethod]
    public void Export_ReproducesEverySampleStackAndWeight()
    {
        StackSampleSource source = SyntheticCpu();

        string json = SpeedscopeExporter.Export(source);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        string[] frames = root.GetProperty("shared").GetProperty("frames")
            .EnumerateArray()
            .Select(static f => f.GetProperty("name").GetString()!)
            .ToArray();

        JsonElement profile = root.GetProperty("profiles").EnumerateArray().Single();
        JsonElement samples = profile.GetProperty("samples");
        JsonElement weights = profile.GetProperty("weights");

        samples.GetArrayLength().Should().Be(source.Samples.Count);
        weights.GetArrayLength().Should().Be(source.Samples.Count);

        // Each exported sample, resolved through the shared frame table, must equal
        // the source sample's frames and weight exactly (a faithful 1:1 export).
        for (int s = 0; s < source.Samples.Count; s++)
        {
            string[] resolved = samples[s].EnumerateArray()
                .Select(i => frames[i.GetInt32()])
                .ToArray();

            resolved.Should().Equal(source.Samples[s].Frames);
            weights[s].GetDouble().Should().Be(source.Samples[s].Weight);
        }
    }

    [TestMethod]
    public void Export_EndValue_IsTheTotalWeight()
    {
        StackSampleSource source = SyntheticCpu();

        string json = SpeedscopeExporter.Export(source);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement profile = doc.RootElement.GetProperty("profiles").EnumerateArray().Single();
        profile.GetProperty("endValue").GetDouble().Should().Be(18.0);
        profile.GetProperty("type").GetString().Should().Be("sampled");
    }

    [TestMethod]
    public void Export_CpuMetric_UsesMillisecondsUnit()
    {
        string json = SpeedscopeExporter.Export(SyntheticCpu());

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement profile = doc.RootElement.GetProperty("profiles").EnumerateArray().Single();
        profile.GetProperty("unit").GetString().Should().Be("milliseconds");
    }

    [TestMethod]
    public void Export_AllocationMetric_UsesBytesUnit()
    {
        StackSampleSource source = new AllocationProvider().Read(FixturePath("alloc.nettrace"));

        string json = SpeedscopeExporter.Export(source);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        root.GetProperty("$schema").GetString().Should().Contain("speedscope");

        JsonElement profile = root.GetProperty("profiles").EnumerateArray().Single();
        profile.GetProperty("unit").GetString().Should().Be("bytes");
        // The byte-weighted total is positive and large.
        profile.GetProperty("endValue").GetDouble().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Export_FrameNamesWithAngleBrackets_AreNotOverEscaped()
    {
        string json = SpeedscopeExporter.Export(SyntheticCpu());

        json.Should().Contain("<root>");
        json.Should().NotContain("\\u003C");
    }

    [TestMethod]
    public void Export_SharedFrameTable_DeduplicatesByName()
    {
        // Program.Main appears on all three stacks but must occupy one frame-table slot.
        string json = SpeedscopeExporter.Export(SyntheticCpu());

        using JsonDocument doc = JsonDocument.Parse(json);
        string[] frames = doc.RootElement.GetProperty("shared").GetProperty("frames")
            .EnumerateArray()
            .Select(static f => f.GetProperty("name").GetString()!)
            .ToArray();

        frames.Should().OnlyHaveUniqueItems();
        frames.Should().Contain("Program.Main");
    }
}
