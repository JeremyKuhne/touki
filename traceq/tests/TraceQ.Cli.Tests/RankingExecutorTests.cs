// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

[TestClass]
public sealed class RankingExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static string Alloc => FixturePath("alloc.nettrace");

    private static string ExceptionsTrace => FixturePath("exceptions.nettrace");

    private static string Etw => FixturePath("etw.etl");

    private static RankRequest Request(
        string path,
        Measure measure = Measure.Self,
        string root = "",
        int top = RankRequestFactory.DefaultTop,
        OutputFormat format = OutputFormat.Text,
        bool strict = false,
        IReadOnlyList<string>? fold = null,
        TraceMetric metric = TraceMetric.Cpu) =>
        new(path, metric, root, top, fold ?? FrameNames.DefaultFoldPatterns, measure, format, Symbols: null, strict);

    private static (int Exit, string Out, string Error) Run(RankRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = RankingExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_TextFormat_WritesBannerAndRankedFrames()
    {
        (int exit, string output, _) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU self-time");
        output.Should().Contain("frame");
        output.Should().Contain("samples");
    }

    [TestMethod]
    public void Run_InclusiveMeasure_LabelsTheReport()
    {
        (int exit, string output, _) = Run(Request(Speedscope, measure: Measure.Inclusive));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU inclusive-time");
    }

    [TestMethod]
    public void Run_AllocMetric_RanksAllocationSitesInBytes()
    {
        // The allocation provider ranks the GCAllocationTick stacks of the .nettrace
        // fixture, so the banner names the allocation metric and reports bytes, not ms.
        (int exit, string output, _) = Run(Request(Alloc, metric: TraceMetric.Allocations));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Allocations self-time");
        output.Should().Contain("bytes");
    }

    [TestMethod]
    public void Run_AllocMetricOnSpeedscope_ReturnsInputError()
    {
        // A speedscope export carries no allocation events, so the format guardrail
        // rejects it cleanly rather than letting the provider fail deep in the reader.
        (int exit, _, string error) = Run(Request(Speedscope, metric: TraceMetric.Allocations));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("allocation metric requires");
    }

    [TestMethod]
    public void Run_ExceptionsMetric_RanksThrowSitesInCounts()
    {
        // The exceptions provider ranks the Exception/Start stacks of the .nettrace
        // fixture, so the banner names the exceptions metric and reports counts.
        (int exit, string output, _) = Run(Request(ExceptionsTrace, metric: TraceMetric.Exceptions));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Exceptions self-time");
        output.Should().Contain("count");
    }

    [TestMethod]
    public void Run_ExceptionsMetricOnSpeedscope_ReturnsInputError()
    {
        // A speedscope export carries no exception events, so the format guardrail
        // rejects it cleanly.
        (int exit, _, string error) = Run(Request(Speedscope, metric: TraceMetric.Exceptions));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("exceptions metric requires");
    }

    [TestMethod]
    public void Run_ThreadTimeMetricOnNetTrace_ReturnsInputError()
    {
        // The thread-time guardrail fires on the format before any .etl read, so this
        // rejection is platform-agnostic and runs on every CI leg.
        (int exit, _, string error) = Run(Request(Alloc, metric: TraceMetric.ThreadTime));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("thread-time metric requires");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Run_ThreadTimeMetric_RanksElapsedTimeInMs()
    {
        // Reading an .etl requires the Windows-only ETW conversion, so this runs on
        // Windows and skips on the Linux CI leg.
        (int exit, string output, _) = Run(Request(Etw, metric: TraceMetric.ThreadTime));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("ThreadTime self-time");
    }

    [TestMethod]
    public void Run_JsonFormat_WritesSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Speedscope, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"result\"");
    }

    [TestMethod]
    public void Run_Top_LimitsRowCount()
    {
        (int exit, string output, _) = Run(Request(Speedscope, top: 1, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        Regex.Matches(output, "\"frame\":").Count.Should().Be(1);
    }

    [TestMethod]
    public void Run_MissingFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(FixturePath("does-not-exist.nettrace")));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_StrictOnFullyResolvedTrace_DoesNotTripGate()
    {
        // The speedscope fixture resolves every frame, so --strict must not gate it.
        (int exit, _, _) = Run(Request(Speedscope, strict: true));

        exit.Should().Be(ExitCodes.Success);
    }

    [TestMethod]
    public void Run_InvalidFoldPattern_ReturnsUsageError()
    {
        // A malformed fold regex is a usage error, reported before any trace work.
        (int exit, _, string error) = Run(Request(Speedscope, fold: ["["]));

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_MalformedSpeedscopeJson_ReturnsInputError()
    {
        // Malformed JSON must terminate with a defined exit code, not an unhandled crash.
        string path = Path.Combine(Path.GetTempPath(), $"traceq-malformed-{Guid.NewGuid():N}.speedscope.json");
        File.WriteAllText(path, "{ this is not valid json");
        try
        {
            (int exit, _, string error) = Run(Request(path));

            exit.Should().Be(ExitCodes.InputError);
            error.Should().NotBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_WellFormedButWrongShapeJson_ReturnsInputError()
    {
        // Valid JSON whose shape is wrong (an event is missing its required "at" field)
        // surfaces as a KeyNotFoundException from the reader's JsonElement access; it must
        // map to a defined exit code rather than crashing the process.
        string path = Path.Combine(Path.GetTempPath(), $"traceq-wrongshape-{Guid.NewGuid():N}.speedscope.json");
        File.WriteAllText(path, """{"profiles":[{"name":"t","events":[{"type":"O","frame":0}]}]}""");
        try
        {
            (int exit, _, string error) = Run(Request(path));

            exit.Should().Be(ExitCodes.InputError);
            error.Should().NotBeEmpty();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
