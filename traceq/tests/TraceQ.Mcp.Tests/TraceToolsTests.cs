// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using ModelContextProtocol;
using TraceQ.Output;
using TraceQ.Server;
using TraceQ.Tracing;
using TraceQ.Tracing.Providers;

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

    // Every tool returns the typed AnalysisResult envelope; the SDK serializes it (with
    // an output schema and structured content) from the typed shape, so the unit tests
    // assert on the object directly rather than re-parsing JSON.
    private static void AssertEnvelope<T>(AnalysisResult<T> envelope)
    {
        envelope.SchemaVersion.Should().Be(2);
        envelope.Warnings.Should().NotBeNull();
        envelope.Hints.Should().NotBeNull();
        envelope.Result.Should().NotBeNull();
    }

    [TestMethod]
    public void Info_Speedscope_ReturnsFormatSampleCountAndThreads()
    {
        TraceStore store = new();

        AnalysisResult<TraceInfoView> envelope = TraceTools.Info(store, FixturePath(Speedscope));

        AssertEnvelope(envelope);
        TraceInfoView result = envelope.Result;
        result.Path.Should().EndWith(Speedscope);
        result.Format.Should().Be("Speedscope");
        result.SampleCount.Should().Be(4);
        result.SymbolResolutionRate.Should().BeInRange(0.0, 1.0);
        result.Threads.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Info_Payload_HasNoWarningsMember()
    {
        // The quality warnings travel only in the envelope's warnings channel; the typed
        // trace_info payload has no warnings member, so the duplication the old string
        // contract risked is now impossible by construction.
        typeof(TraceInfoView).GetProperty("Warnings").Should().BeNull();
    }

    [TestMethod]
    public void Rank_SpeedscopeSelf_RanksFramesAndEmitsHint()
    {
        TraceStore store = new();

        AnalysisResult<RankingResult> envelope = TraceTools.Rank(store, FixturePath(Speedscope));

        AssertEnvelope(envelope);
        envelope.Hints.Should().NotBeEmpty();
        envelope.Result.Rows.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Rank_SpeedscopeInclusive_RanksFrames()
    {
        TraceStore store = new();

        AnalysisResult<RankingResult> envelope = TraceTools.Rank(store, FixturePath(Speedscope), measure: "inclusive");

        envelope.Result.Rows.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Rank_AllocMetric_ReadsTheAllocationView()
    {
        TraceStore store = new();

        AnalysisResult<RankingResult> envelope = TraceTools.Rank(store, FixturePath(Alloc), metric: "alloc");

        AssertEnvelope(envelope);
        envelope.Result.Rows.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Rank_ExceptionsMetric_ReadsTheExceptionView()
    {
        TraceStore store = new();

        AnalysisResult<RankingResult> envelope = TraceTools.Rank(store, FixturePath(Exceptions), metric: "exceptions");

        AssertEnvelope(envelope);
        envelope.Result.Rows.Should().NotBeEmpty();
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Rank_ThreadTimeMetric_ReadsTheEtlView()
    {
        TraceStore store = new();

        AnalysisResult<RankingResult> envelope = TraceTools.Rank(store, FixturePath(Etw), metric: "threadtime");

        AssertEnvelope(envelope);
        envelope.Result.Rows.Should().NotBeEmpty();
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

        AnalysisResult<CallersResult> envelope = TraceTools.Callers(store, FixturePath(Speedscope), frame: "");

        AssertEnvelope(envelope);
        envelope.Result.Callers.Should().NotBeNull();
    }

    [TestMethod]
    public void Lines_SpeedscopeWithoutLineData_ReturnsEmptyRanking()
    {
        TraceStore store = new();

        // Speedscope carries no per-frame source locations, so the line ranking is empty.
        AnalysisResult<LineRankingResult> envelope = TraceTools.Lines(store, FixturePath(Speedscope));

        AssertEnvelope(envelope);
        envelope.Result.Rows.Should().BeEmpty();
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

        AnalysisResult<SourceHeatmapResult> envelope =
            TraceTools.Heatmap(store, FixturePath(Speedscope), file: "ExtGlob.cs");

        AssertEnvelope(envelope);
        envelope.Result.Lines.Should().BeEmpty();
    }

    [TestMethod]
    public void Heatmap_InvalidFoldPattern_ThrowsMcpException()
    {
        TraceStore store = new();

        Action act = () => TraceTools.Heatmap(store, FixturePath(Speedscope), file: "ExtGlob.cs", fold: ["("]);

        act.Should().Throw<McpException>().WithMessage("*Invalid fold pattern*");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Lines_ProcessScope_OnMachineWideCapture_Warns()
    {
        TraceStore store = new();

        // The lines tool now scopes a multi-process ETW capture to a named process
        // tree; the scope notice surfaces in the envelope warnings. Reading an .etl is
        // Windows-only, so this is guarded.
        AnalysisResult<LineRankingResult> envelope =
            TraceTools.Lines(store, FixturePath(Etw), process: "HotLoopBench-Job");

        AssertEnvelope(envelope);
        envelope.Warnings.Should().Contain(w => w.Contains("Scoped to the"));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Callers_ProcessScope_OnMachineWideCapture_Warns()
    {
        TraceStore store = new();

        AnalysisResult<CallersResult> envelope =
            TraceTools.Callers(store, FixturePath(Etw), frame: "", process: "HotLoopBench-Job");

        AssertEnvelope(envelope);
        envelope.Warnings.Should().Contain(w => w.Contains("Scoped to the"));
    }

    [TestMethod]
    public void Lines_ProcessScopeOnSpeedscope_IsHarmlessNoOp()
    {
        TraceStore store = new();

        // Speedscope is single-process, so a process selector is a no-op: the tool
        // still succeeds and returns the (empty, no line data) ranking.
        AnalysisResult<LineRankingResult> envelope =
            TraceTools.Lines(store, FixturePath(Speedscope), process: "anything");

        AssertEnvelope(envelope);
        envelope.Result.Rows.Should().BeEmpty();
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
        AnalysisResult<RankingDiffResult> envelope = TraceTools.Diff(store, path, path);

        AssertEnvelope(envelope);
        envelope.Result.ScopeDelta.Should().Be(0.0);
        envelope.Result.Rows.Should().OnlyContain(row => row.Delta == 0.0);
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
        AnalysisResult<RankingDiffResult> envelope = TraceTools.Diff(store, path, path, measure: "inclusive");

        AssertEnvelope(envelope);
        envelope.Result.ScopeDelta.Should().Be(0.0);
    }

    [TestMethod]
    public void Gc_NetTrace_ReturnsAggregateSummary()
    {
        AnalysisResult<GcStatsResult> envelope = TraceTools.Gc(FixturePath(Alloc));

        AssertEnvelope(envelope);
        envelope.Result.GcCount.Should().BeGreaterThan(0);
        envelope.Result.Gcs.Should().NotBeEmpty();
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
        AnalysisResult<GcStatsResult> envelope = TraceTools.Gc(FixturePath(Alloc), top: 1);

        envelope.Result.Gcs.Count.Should().BeLessThanOrEqualTo(1);
    }

    [TestMethod]
    public void Export_Speedscope_WritesFileAndConfirms()
    {
        TraceStore store = new();
        string outputPath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.speedscope.json");

        try
        {
            AnalysisResult<ExportResult> envelope = TraceTools.Export(store, FixturePath(Speedscope), outputPath);

            File.Exists(outputPath).Should().BeTrue();
            AssertEnvelope(envelope);

            ExportResult result = envelope.Result;
            result.Format.Should().Be("speedscope");
            result.OutputPath.Should().Be(Path.GetFullPath(outputPath));
            result.ByteCount.Should().BeGreaterThan(0);

            // The hint steers a human to the viewer for the chosen format.
            envelope.Hints.Should().Contain(h => h.Contains("speedscope.app", StringComparison.Ordinal));

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
            AnalysisResult<ExportResult> envelope =
                TraceTools.Export(store, FixturePath(Speedscope), outputPath, format: "chromium");

            File.Exists(outputPath).Should().BeTrue();
            AssertEnvelope(envelope);
            envelope.Result.Format.Should().Be("chromium");
            envelope.Hints.Should().Contain(h => h.Contains("perfetto", StringComparison.OrdinalIgnoreCase));

            // The written file is the Chrome Trace Event Format the exporter produced; its
            // distinctive marker is the traceEvents array. Asserting on the file content -
            // not just the envelope - catches a regression that writes the wrong or empty
            // content to disk.
            string written = File.ReadAllText(outputPath);
            written.Should().Contain("\"traceEvents\"");
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

    [TestMethod]
    public void QueryEvents_NetTrace_ReturnsMatchingEventsPage()
    {
        AnalysisResult<EventQueryResult> envelope = TraceTools.QueryEvents(FixturePath(Alloc));

        AssertEnvelope(envelope);
        envelope.Result.TotalMatched.Should().BeGreaterThan(0);
        envelope.Result.Events.Should().NotBeEmpty();
    }

    [TestMethod]
    public void QueryEvents_Take_PagesAndHintsRemaining()
    {
        // A take smaller than the total match count returns one page and steers toward
        // the next with a paging hint.
        AnalysisResult<EventQueryResult> envelope = TraceTools.QueryEvents(FixturePath(Alloc), take: 1);

        envelope.Result.Events.Count.Should().BeLessThanOrEqualTo(1);

        // When more matches remain, a hint gives the next page's skip.
        if (envelope.Result.TotalMatched > 1)
        {
            envelope.Hints.Should().Contain(h => h.Contains("page with skip", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public void QueryEvents_NonNetTraceInput_ThrowsMcpException()
    {
        // The events query parses the EventPipe format; an .etl or speedscope is rejected
        // up front by the extension guardrail.
        Action act = () => TraceTools.QueryEvents(FixturePath(Speedscope));

        act.Should().Throw<McpException>().WithMessage("*requires a .nettrace*");
    }

    [TestMethod]
    public void QueryEvents_NegativeSkip_Throws()
    {
        Action act = () => TraceTools.QueryEvents(FixturePath(Alloc), skip: -1);

        act.Should().Throw<McpException>().WithMessage("*skip must be 0 or greater*");
    }

    [TestMethod]
    public void QueryEvents_TakeAboveMax_ClampsWithWarning()
    {
        // A take past the page ceiling is clamped rather than honored, so a caller cannot
        // request a page large enough to exhaust memory or blow the token budget; the
        // clamp is surfaced as a warning so paging still works.
        AnalysisResult<EventQueryResult> envelope = TraceTools.QueryEvents(FixturePath(Alloc), take: int.MaxValue);

        envelope.Result.Events.Count.Should().BeLessThanOrEqualTo(1000);
        envelope.Warnings.Should().Contain(w => w.Contains("clamped", StringComparison.Ordinal));
    }

    [TestMethod]
    public void QueryEvents_MaxPayloadAboveMax_ClampsWithWarning()
    {
        // A maxPayload past the ceiling is clamped with a warning for the same reason.
        AnalysisResult<EventQueryResult> envelope =
            TraceTools.QueryEvents(FixturePath(Alloc), maxPayload: int.MaxValue);

        envelope.Warnings.Should().Contain(w => w.Contains("clamped", StringComparison.Ordinal));
    }

    [TestMethod]
    public void QueryEvents_NullPath_ThrowsMcpException()
    {
        // A null path must fail through the format guardrail as a clean McpException, not
        // a NullReferenceException surfaced as an opaque JSON-RPC error.
        Action act = () => TraceTools.QueryEvents(null!);

        act.Should().Throw<McpException>().WithMessage("*requires a .nettrace*");
    }
}
