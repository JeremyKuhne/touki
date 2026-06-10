// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;
using ModelContextProtocol;
using TraceQ.Server;

namespace TraceQ.Mcp;

[TestClass]
public sealed class TraceToolsTests
{
    private const string Speedscope = "folding.speedscope.json";
    private const string Alloc = "alloc.nettrace";
    private const string Exceptions = "exceptions.nettrace";
    private const string Etw = "etw.etl";

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static void AssertEnvelopeShape(JsonElement root)
    {
        root.GetProperty("schemaVersion").GetInt32().Should().Be(2);
        root.TryGetProperty("warnings", out JsonElement warnings).Should().BeTrue();
        warnings.ValueKind.Should().Be(JsonValueKind.Array);
        root.TryGetProperty("hints", out JsonElement hints).Should().BeTrue();
        hints.ValueKind.Should().Be(JsonValueKind.Array);
        root.TryGetProperty("result", out _).Should().BeTrue();
    }

    [TestMethod]
    public void Info_Speedscope_ReturnsFormatSampleCountAndThreads()
    {
        TraceStore store = new();

        string json = TraceTools.Info(store, FixturePath(Speedscope));

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);

        JsonElement result = root.GetProperty("result");
        result.GetProperty("path").GetString().Should().EndWith(Speedscope);
        result.GetProperty("format").GetString().Should().Be("Speedscope");
        result.GetProperty("sampleCount").GetInt32().Should().Be(4);
        result.GetProperty("symbolResolutionRate").GetDouble().Should().BeInRange(0.0, 1.0);
        result.GetProperty("threads").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Info_DoesNotRepeatWarningsInsideResult()
    {
        TraceStore store = new();

        // The quality warnings travel only in the envelope's warnings channel; the
        // trace_info payload itself must not carry a second copy of them.
        string json = TraceTools.Info(store, FixturePath(Speedscope));

        using JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("result").TryGetProperty("warnings", out _).Should().BeFalse();
    }

    [TestMethod]
    public void Rank_SpeedscopeSelf_RanksFramesAndEmitsHint()
    {
        TraceStore store = new();

        string json = TraceTools.Rank(store, FixturePath(Speedscope));

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("hints").GetArrayLength().Should().BeGreaterThan(0);
        root.GetProperty("result").GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Rank_SpeedscopeInclusive_RanksFrames()
    {
        TraceStore store = new();

        string json = TraceTools.Rank(store, FixturePath(Speedscope), measure: "inclusive");

        using JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("result").GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Rank_AllocMetric_ReadsTheAllocationView()
    {
        TraceStore store = new();

        string json = TraceTools.Rank(store, FixturePath(Alloc), metric: "alloc");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("result").GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Rank_ExceptionsMetric_ReadsTheExceptionView()
    {
        TraceStore store = new();

        string json = TraceTools.Rank(store, FixturePath(Exceptions), metric: "exceptions");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("result").GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Rank_ThreadTimeMetric_ReadsTheEtlView()
    {
        TraceStore store = new();

        string json = TraceTools.Rank(store, FixturePath(Etw), metric: "threadtime");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("result").GetProperty("rows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Rank_UnknownMetric_ThrowsWithSelectorList()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Rank(store, FixturePath(Speedscope), metric: "ipc");

        act.Should().Throw<McpException>().WithMessage("*Unknown metric 'ipc'*cpu*");
    }

    [TestMethod]
    public void Rank_UnknownMeasure_Throws()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Rank(store, FixturePath(Speedscope), measure: "average");

        act.Should().Throw<McpException>().WithMessage("*Unknown measure 'average'*");
    }

    [TestMethod]
    public void Rank_NonPositiveTop_Throws()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Rank(store, FixturePath(Speedscope), top: 0);

        act.Should().Throw<McpException>().WithMessage("*top must be 1 or greater*");
    }

    [TestMethod]
    public void Callers_Speedscope_ReturnsCallerBreakdown()
    {
        TraceStore store = new();

        string json = TraceTools.Callers(store, FixturePath(Speedscope), frame: "");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("result").TryGetProperty("callers", out JsonElement callers).Should().BeTrue();
        callers.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [TestMethod]
    public void Lines_SpeedscopeWithoutLineData_ReturnsEmptyRanking()
    {
        TraceStore store = new();

        // Speedscope carries no per-frame source locations, so the line ranking is empty.
        string json = TraceTools.Lines(store, FixturePath(Speedscope));

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("result").GetProperty("rows").GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public void Heatmap_SpeedscopeWithoutLineData_ReturnsEmptyMap()
    {
        TraceStore store = new();

        string json = TraceTools.Heatmap(store, FixturePath(Speedscope), file: "ExtGlob.cs");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("result").GetProperty("lines").GetArrayLength().Should().Be(0);
    }

    [TestMethod]
    public void Info_MissingFile_ThrowsMcpException()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Info(store, FixturePath("does-not-exist.nettrace"));

        act.Should().Throw<McpException>();
    }
}
