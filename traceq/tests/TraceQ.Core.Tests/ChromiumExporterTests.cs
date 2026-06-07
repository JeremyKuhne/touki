// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;
using TraceQ.Tracing;
using TraceQ.Tracing.Providers;

namespace TraceQ.Output;

[TestClass]
public sealed class ChromiumExporterTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static StackSampleSource SyntheticCpu() =>
        new(
            MetricInfo.Cpu,
            [
                new SampleStack(["Main", "Work", "Inner"], 10.0, "1"),
                new SampleStack(["Main", "Work"], 5.0, "1"),
                new SampleStack(["Main", "Other"], 3.0, "1")
            ]);

    private static JsonElement[] TraceEvents(string json, out JsonDocument doc)
    {
        doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("traceEvents").EnumerateArray().ToArray();
    }

    [TestMethod]
    public void Export_BeginAndEndEvents_AreBalanced()
    {
        JsonElement[] events = TraceEvents(ChromiumExporter.Export(SyntheticCpu()), out JsonDocument doc);
        using JsonDocument _ = doc;

        int begins = events.Count(e => e.GetProperty("ph").GetString() == "B");
        int ends = events.Count(e => e.GetProperty("ph").GetString() == "E");

        // Every opened frame is closed; the timeline is well-formed.
        begins.Should().Be(ends);
        begins.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Export_NestingDepthNeverGoesNegative_AndEndsAtZero()
    {
        JsonElement[] events = TraceEvents(ChromiumExporter.Export(SyntheticCpu()), out JsonDocument doc);
        using JsonDocument _ = doc;

        int depth = 0;
        foreach (JsonElement e in events)
        {
            switch (e.GetProperty("ph").GetString())
            {
                case "B":
                    depth++;
                    break;
                case "E":
                    depth--;
                    break;
            }

            depth.Should().BeGreaterThanOrEqualTo(0, "an end never precedes its begin");
        }

        depth.Should().Be(0, "every begin is matched by an end");
    }

    [TestMethod]
    public void Export_BeginEvents_CarryFrameNames()
    {
        JsonElement[] events = TraceEvents(ChromiumExporter.Export(SyntheticCpu()), out JsonDocument doc);
        using JsonDocument _ = doc;

        string[] beginNames = events
            .Where(e => e.GetProperty("ph").GetString() == "B")
            .Select(e => e.GetProperty("name").GetString()!)
            .ToArray();

        beginNames.Should().Contain("Main").And.Contain("Inner").And.Contain("Other");
    }

    [TestMethod]
    public void Export_FinalTimestamp_IsTheScaledTotalWeight()
    {
        JsonElement[] events = TraceEvents(ChromiumExporter.Export(SyntheticCpu()), out JsonDocument doc);
        using JsonDocument _ = doc;

        double maxTs = events
            .Where(e => e.GetProperty("ph").GetString() != "M")
            .Max(e => e.GetProperty("ts").GetDouble());

        // 18 ms total, scaled to microseconds.
        maxTs.Should().Be(18.0 * 1000.0);
    }

    [TestMethod]
    public void Export_AllocationMetric_DoesNotScaleTheByteAxis()
    {
        StackSampleSource source = new(
            MetricInfo.Allocations,
            [new SampleStack(["A", "B"], 2048.0, "1")]);

        JsonElement[] events = TraceEvents(ChromiumExporter.Export(source), out JsonDocument doc);
        using JsonDocument _ = doc;

        double maxTs = events
            .Where(e => e.GetProperty("ph").GetString() != "M")
            .Max(e => e.GetProperty("ts").GetDouble());

        // Bytes are used as the axis magnitude directly, not scaled like milliseconds.
        maxTs.Should().Be(2048.0);
    }

    [TestMethod]
    public void Export_NamesTheAggregateThread()
    {
        JsonElement[] events = TraceEvents(ChromiumExporter.Export(SyntheticCpu(), "cpu"), out JsonDocument doc);
        using JsonDocument _ = doc;

        JsonElement meta = events.Single(e => e.GetProperty("ph").GetString() == "M");
        meta.GetProperty("name").GetString().Should().Be("thread_name");
        meta.GetProperty("args").GetProperty("name").GetString().Should().Be("cpu");
    }

    [TestMethod]
    public void Export_AllocationFixture_ProducesABalancedTimeline()
    {
        StackSampleSource source = new AllocationProvider().Read(FixturePath("alloc.nettrace"));

        JsonElement[] events = TraceEvents(ChromiumExporter.Export(source), out JsonDocument doc);
        using JsonDocument _ = doc;

        int begins = events.Count(e => e.GetProperty("ph").GetString() == "B");
        int ends = events.Count(e => e.GetProperty("ph").GetString() == "E");
        begins.Should().Be(ends);
        begins.Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Export_CpuMetric_LabelsTheAxisInMilliseconds()
    {
        using JsonDocument doc = JsonDocument.Parse(ChromiumExporter.Export(SyntheticCpu()));

        doc.RootElement.TryGetProperty("displayTimeUnit", out JsonElement unit).Should().BeTrue();
        unit.GetString().Should().Be("ms");
    }

    [TestMethod]
    public void Export_AllocationMetric_OmitsTheTimeUnitLabel()
    {
        StackSampleSource source = new(
            MetricInfo.Allocations,
            [new SampleStack(["A", "B"], 2048.0, "1")]);

        using JsonDocument doc = JsonDocument.Parse(ChromiumExporter.Export(source));

        // The ts field carries bytes for allocation, so the millisecond axis label is
        // omitted rather than mislabeling the axis.
        doc.RootElement.TryGetProperty("displayTimeUnit", out _).Should().BeFalse();
    }
}
