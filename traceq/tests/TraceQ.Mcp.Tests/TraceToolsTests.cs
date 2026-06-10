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
    public void Rank_InvalidFoldPattern_ThrowsMcpExceptionNamingThePattern()
    {
        TraceStore store = new();

        // A malformed user-supplied fold regex is a usage error, not an internal failure:
        // it must surface as a clean tool error that names the offending pattern.
        Action act = () => TraceTools.Rank(store, FixturePath(Speedscope), fold: ["("]);

        act.Should().Throw<McpException>().WithMessage("*Invalid fold pattern*");
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
    public void Lines_InvalidFoldPattern_ThrowsMcpException()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Lines(store, FixturePath(Speedscope), fold: ["("]);

        act.Should().Throw<McpException>().WithMessage("*Invalid fold pattern*");
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
    public void Heatmap_InvalidFoldPattern_ThrowsMcpException()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Heatmap(store, FixturePath(Speedscope), file: "ExtGlob.cs", fold: ["("]);

        act.Should().Throw<McpException>().WithMessage("*Invalid fold pattern*");
    }

    [TestMethod]
    public void Info_MissingFile_ThrowsMcpException()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Info(store, FixturePath("does-not-exist.nettrace"));

        act.Should().Throw<McpException>();
    }

    [TestMethod]
    public void Diff_SameTraceTwice_ReturnsZeroScopeDelta()
    {
        TraceStore store = new();
        string path = FixturePath(Speedscope);

        // Diffing a trace against itself: every frame matches, so the scope total is
        // unchanged and no frame shows a delta.
        string json = TraceTools.Diff(store, path, path);

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        JsonElement result = root.GetProperty("result");
        result.GetProperty("scopeDelta").GetDouble().Should().Be(0.0);
        foreach (JsonElement changed in result.GetProperty("rows").EnumerateArray())
        {
            changed.GetProperty("delta").GetDouble().Should().Be(0.0);
        }
    }

    [TestMethod]
    public void Diff_UnknownMeasure_Throws()
    {
        TraceStore store = new();
        string path = FixturePath(Speedscope);

        Action act = () => TraceTools.Diff(store, path, path, measure: "average");

        act.Should().Throw<McpException>().WithMessage("*Unknown measure 'average'*");
    }

    [TestMethod]
    public void Diff_InclusiveMeasure_SameTraceTwice_ReturnsZeroScopeDelta()
    {
        TraceStore store = new();
        string path = FixturePath(Speedscope);

        // The inclusive branch ranks both sides with InclusiveTime; diffing a trace
        // against itself still yields no change.
        string json = TraceTools.Diff(store, path, path, measure: "inclusive");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        root.GetProperty("result").GetProperty("scopeDelta").GetDouble().Should().Be(0.0);
    }

    [TestMethod]
    public void Gc_NetTrace_ReturnsAggregateSummary()
    {
        string json = TraceTools.Gc(FixturePath(Alloc));

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        AssertEnvelopeShape(root);
        JsonElement result = root.GetProperty("result");
        result.GetProperty("gcCount").GetInt32().Should().BeGreaterThan(0);
        result.GetProperty("gcs").GetArrayLength().Should().BeGreaterThan(0);
    }

    [TestMethod]
    public void Gc_NonNetTraceInput_ThrowsMcpException()
    {
        // The GC report parses the EventPipe format; an .etl or speedscope is rejected
        // up front by the extension guardrail.
        Action act = () => TraceTools.Gc(FixturePath(Speedscope));

        act.Should().Throw<McpException>().WithMessage("*requires a .nettrace*");
    }

    [TestMethod]
    public void Gc_NonPositiveTop_Throws()
    {
        Action act = () => TraceTools.Gc(FixturePath(Alloc), top: 0);

        act.Should().Throw<McpException>().WithMessage("*top must be 1 or greater*");
    }

    [TestMethod]
    public void Gc_Top_CapsPerCollectionDetail()
    {
        // The aggregate summary always reflects every collection, but the per-collection
        // detail list is capped to 'top' so a long trace cannot blow the output budget.
        string json = TraceTools.Gc(FixturePath(Alloc), top: 1);

        using JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("result").GetProperty("gcs").GetArrayLength().Should().BeLessThanOrEqualTo(1);
    }

    [TestMethod]
    public void Export_Speedscope_WritesFileAndConfirms()
    {
        TraceStore store = new();
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.speedscope.json");

        try
        {
            string json = TraceTools.Export(store, FixturePath(Speedscope), outputPath);

            File.Exists(outputPath).Should().BeTrue();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            AssertEnvelopeShape(root);

            JsonElement result = root.GetProperty("result");
            result.GetProperty("format").GetString().Should().Be("speedscope");
            result.GetProperty("outputPath").GetString().Should().Be(Path.GetFullPath(outputPath));
            result.GetProperty("byteCount").GetInt64().Should().BeGreaterThan(0);

            // The hint steers a human to the viewer for the chosen format.
            root.GetProperty("hints").EnumerateArray().Should().Contain(h =>
                h.GetString()!.Contains("speedscope.app", StringComparison.Ordinal));

            // The written file is the same speedscope JSON the exporter produced.
            string written = File.ReadAllText(outputPath);
            written.Should().Contain("\"$schema\"");
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [TestMethod]
    public void Export_Chromium_WritesChromeTraceFormat()
    {
        TraceStore store = new();
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.chrome.json");

        try
        {
            string json = TraceTools.Export(store, FixturePath(Speedscope), outputPath, format: "chromium");

            File.Exists(outputPath).Should().BeTrue();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            AssertEnvelopeShape(root);
            root.GetProperty("result").GetProperty("format").GetString().Should().Be("chromium");
            root.GetProperty("hints").EnumerateArray().Should().Contain(h =>
                h.GetString()!.Contains("perfetto", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [TestMethod]
    public void Export_UnknownFormat_Throws()
    {
        TraceStore store = new();
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.json");

        Action act = () => TraceTools.Export(store, FixturePath(Speedscope), outputPath, format: "perfetto");

        act.Should().Throw<McpException>().WithMessage("*Unknown format 'perfetto'*");
        File.Exists(outputPath).Should().BeFalse();
    }

    [TestMethod]
    public void Export_EmptyOutput_Throws()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Export(store, FixturePath(Speedscope), output: "  ");

        act.Should().Throw<McpException>().WithMessage("*output is required*");
    }

    [TestMethod]
    public void Export_UnwritablePath_ThrowsMcpException()
    {
        TraceStore store = new();

        // A path into a directory that does not exist is not writable; the failure
        // surfaces as a clean tool error rather than an unhandled exception.
        string badPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "nested", "out.json");

        Action act = () => TraceTools.Export(store, FixturePath(Speedscope), badPath);

        act.Should().Throw<McpException>().WithMessage("*Could not write*");
    }
}
