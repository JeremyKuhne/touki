// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  End-to-end tests that drive the real ConsoleAppFramework parser through
///  <see cref="CliApp.Run"/>, exercising option binding, enum parsing, validation
///  and exit codes. Console is redirected for the duration of each run, so the
///  class opts out of parallelism.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CliAppTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static string Alloc => FixturePath("alloc.nettrace");

    private static string ExceptionsTrace => FixturePath("exceptions.nettrace");

    private static string Etw => FixturePath("etw.etl");

    private static string Jit => FixturePath("jit.nettrace");

    private static (int Exit, string Out, string Error) Run(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        StringWriter output = new();
        StringWriter error = new();
        Environment.ExitCode = 0;
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int exit = CliApp.Run(args);
            return (exit, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Environment.ExitCode = 0;
        }
    }

    [TestMethod]
    public void Run_NoArgs_ShowsVerbList()
    {
        (int exit, string output, _) = Run();

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Commands:");
        output.Should().Contain("rank");
        output.Should().Contain("cpu");
        output.Should().Contain("alloc");
        output.Should().Contain("exceptions");
        output.Should().Contain("threadtime");
        output.Should().Contain("callers");
        output.Should().Contain("lines");
        output.Should().Contain("heatmap");
        output.Should().Contain("diff");
        output.Should().Contain("export");
        output.Should().Contain("tree");
        output.Should().Contain("gcstats");
        output.Should().Contain("jitstats");
        output.Should().Contain("events");
        output.Should().Contain("convert");
        output.Should().Contain("clean");
    }

    [TestMethod]
    public void Run_HelpFlag_ShowsVerbList()
    {
        (int exit, string output, _) = Run("--help");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("rank");
        output.Should().Contain("cpu");
    }

    [TestMethod]
    public void Run_RankHelp_ShowsOptionsAndAliases()
    {
        (int exit, string output, _) = Run("rank", "--help");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("--measure");
        output.Should().Contain("--top");
        output.Should().Contain("--strict");
    }

    [TestMethod]
    public void Run_Rank_WritesRanking()
    {
        (int exit, string output, _) = Run("rank", Speedscope);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU self-time");
    }

    [TestMethod]
    public void Run_MeasureInclusive_IsParsed()
    {
        (int exit, string output, _) = Run("rank", Speedscope, "--measure", "inclusive");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU inclusive-time");
    }

    [TestMethod]
    public void Run_JsonFormat_IsParsedAndSingleLine()
    {
        (int exit, string output, _) = Run("rank", Speedscope, "--format", "json");

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
    }

    [TestMethod]
    public void Run_TopAlias_LimitsRows()
    {
        (int exit, string output, _) = Run("cpu", Speedscope, "-n", "1", "--format", "json");

        exit.Should().Be(ExitCodes.Success);
        Regex.Matches(output, "\"frame\":").Count.Should().Be(1);
    }

    [TestMethod]
    public void Run_CpuShortcut_MatchesRankDefault()
    {
        (int rankExit, string rankOut, _) = Run("rank", Speedscope);
        (int cpuExit, string cpuOut, _) = Run("cpu", Speedscope);

        cpuExit.Should().Be(rankExit);
        cpuOut.Should().Be(rankOut);
    }

    [TestMethod]
    public void Run_AllocShortcut_RanksAllocationBytes()
    {
        (int exit, string output, _) = Run("alloc", Alloc);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Allocations self-time");
        output.Should().Contain("bytes");
    }

    [TestMethod]
    public void Run_RankMetricAlloc_MatchesAllocShortcut()
    {
        // 'rank --metric alloc' and the 'alloc' shortcut select the same provider, so
        // they must produce identical output.
        (int rankExit, string rankOut, _) = Run("rank", Alloc, "--metric", "alloc");
        (int allocExit, string allocOut, _) = Run("alloc", Alloc);

        allocExit.Should().Be(rankExit);
        allocOut.Should().Be(rankOut);
    }

    [TestMethod]
    public void Run_ExceptionsShortcut_RanksThrowCounts()
    {
        (int exit, string output, _) = Run("exceptions", ExceptionsTrace);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Exceptions self-time");
        output.Should().Contain("count");
    }

    [TestMethod]
    public void Run_RankMetricExceptions_MatchesExceptionsShortcut()
    {
        (int rankExit, string rankOut, _) = Run("rank", ExceptionsTrace, "--metric", "exceptions");
        (int excExit, string excOut, _) = Run("exceptions", ExceptionsTrace);

        excExit.Should().Be(rankExit);
        excOut.Should().Be(rankOut);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Run_ThreadTimeShortcut_RanksElapsedTime()
    {
        // Reading an .etl requires the Windows-only ETW conversion, so this runs on
        // Windows and skips on the Linux CI leg.
        (int exit, string output, _) = Run("threadtime", Etw);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("ThreadTime self-time");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Run_RankMetricThreadTime_MatchesThreadTimeShortcut()
    {
        (int rankExit, string rankOut, _) = Run("rank", Etw, "--metric", "threadtime");
        (int ttExit, string ttOut, _) = Run("threadtime", Etw);

        ttExit.Should().Be(rankExit);
        ttOut.Should().Be(rankOut);
    }

    [TestMethod]
    public void Run_UnknownMetric_ReturnsUsageError()
    {
        (int exit, _, string error) = Run("rank", Speedscope, "--metric", "bogus");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("bogus");
    }

    [TestMethod]
    public void Run_ProcessAndAllProcesses_ReturnsUsageError()
    {
        // The two scope options are mutually exclusive; the conflict is caught before
        // any trace read, so it runs on every CI leg.
        (int exit, _, string error) = Run("rank", Speedscope, "--process", "MyApp", "--all-processes");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("only one of --process and --all-processes");
    }

    [TestMethod]
    public void Run_Benchmark_ScopesToTheMeasuredWorkload()
    {
        // The exceptions fixture is a BenchmarkDotNet EventPipe capture; --benchmark
        // scopes the ranking to the WorkloadAction subtree, past the harness/bootstrap.
        (int exit, string output, _) = Run("cpu", ExceptionsTrace, "--benchmark", "--measure", "inclusive");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("scoped to 'WorkloadAction'");
        // The measured benchmark method surfaces once the harness is scoped out.
        output.Should().Contain("ExceptionLoop");
    }

    [TestMethod]
    public void Run_RootAndBenchmark_ReturnsUsageError()
    {
        // --benchmark is itself a root preset, so a second explicit --root conflicts;
        // caught before any trace read.
        (int exit, _, string error) = Run("cpu", ExceptionsTrace, "--root", "Foo", "--benchmark");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("only one of --root and --benchmark");
    }

    [TestMethod]
    public void Run_AllProcessesOnSpeedscope_Succeeds()
    {
        // Speedscope is single-process, so --all-processes is a harmless no-op there;
        // the ranking still renders.
        (int exit, string output, _) = Run("cpu", Speedscope, "--all-processes");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU self-time");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Run_ExplicitProcessScope_OnMachineWideCapture_WarnsAndNarrows()
    {
        // An explicit --process that drops part of an ETW capture surfaces the scope
        // notice. Reading an .etl is Windows-only, so this is guarded.
        (int exit, string output, _) = Run("cpu", Etw, "--process", "HotLoopBench-Job");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Scoped to the");
        output.Should().Contain("--all-processes");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Run_AutoScope_OnMachineWideCapture_Succeeds()
    {
        // The default (no scope option) auto-scopes a multi-process ETW capture to the
        // busiest process tree and still renders a ranking.
        (int exit, string output, _) = Run("cpu", Etw);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU self-time");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Run_AllProcesses_OnMachineWideCapture_DoesNotWarn()
    {
        (int exit, string output, _) = Run("cpu", Etw, "--all-processes");

        exit.Should().Be(ExitCodes.Success);
        output.Should().NotContain("Scoped to the");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Run_Cpu_ForwardsTheLoaderQualityWarnings()
    {
        // The executors forward the full TraceInfo.Warnings list, so the reader's
        // low-symbol-resolution warning (this fixture resolves few frames) reaches the
        // output rather than being dropped by a cherry-picking helper. Reading an .etl
        // is Windows-only.
        (int exit, string output, _) = Run("cpu", Etw, "--all-processes");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("frames resolved to a method name");
    }

    [TestMethod]
    public void Run_AllocMetricOnSpeedscope_ReturnsInputError()
    {
        // 'alloc' is a recognized selector, but a speedscope export carries no
        // allocation events, so the format guardrail rejects it as an input error
        // rather than a usage error.
        (int exit, _, string error) = Run("rank", Speedscope, "--metric", "alloc");

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("allocation metric requires");
    }

    [TestMethod]
    public void Run_ExceptionsMetricOnSpeedscope_ReturnsInputError()
    {
        (int exit, _, string error) = Run("rank", Speedscope, "--metric", "exceptions");

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("exceptions metric requires");
    }

    [TestMethod]
    public void Run_ThreadTimeMetricOnNetTrace_ReturnsInputError()
    {
        // The thread-time guardrail fires on the format before any .etl read, so this
        // runs on every CI leg, not just Windows.
        (int exit, _, string error) = Run("rank", Alloc, "--metric", "threadtime");

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("thread-time metric requires");
    }

    [TestMethod]
    public void Run_TopBelowOne_ReturnsUsageError()
    {
        // ConsoleAppFramework's [Range] validation fails before the verb body runs.
        (int exit, _, _) = Run("rank", Speedscope, "--top", "0");

        exit.Should().Be(ExitCodes.UsageError);
    }

    [TestMethod]
    public void Run_BareVerb_ShowsVerbHelp()
    {
        // A verb with no further arguments shows its own help rather than erroring.
        (int exit, string output, _) = Run("rank");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Path to a .speedscope.json");
    }

    [TestMethod]
    public void Run_MissingTraceArgument_ReturnsUsageError()
    {
        // With an option present but the required trace argument absent, the parser
        // fails rather than falling back to help.
        (int exit, _, string error) = Run("rank", "--measure", "inclusive");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("trace");
    }

    [TestMethod]
    public void Run_MissingFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run("rank", FixturePath("does-not-exist.nettrace"));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_InvalidFoldPattern_ReturnsUsageError()
    {
        // '(' parses as a single fold element (it is not the JSON-array prefix '['), so it
        // reaches the executor's regex validation and surfaces as a usage error there.
        (int exit, _, string error) = Run("rank", Speedscope, "--fold", "(");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("fold");
    }

    [TestMethod]
    public void Run_Callers_WritesCallers()
    {
        (int exit, string output, _) = Run("callers", Speedscope, "MyApp.Inner");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("callers of 'MyApp.Inner'");
    }

    [TestMethod]
    public void Run_CallersMissingFrameArgument_ReturnsUsageError()
    {
        // 'frame' is a required positional, so a callers run without it fails to parse.
        (int exit, _, string error) = Run("callers", Speedscope, "--strict");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("frame");
    }

    [TestMethod]
    public void Run_Lines_RunsAndRendersJson()
    {
        (int exit, string output, _) = Run("lines", Speedscope, "--format", "json");

        exit.Should().Be(ExitCodes.Success);
        output.Trim().Should().Contain("\"schemaVersion\"");
    }

    [TestMethod]
    public void Run_Heatmap_RunsForKnownFile()
    {
        (int exit, string output, _) = Run("heatmap", Speedscope, "Program.cs");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("source heatmap 'Program.cs'");
    }

    [TestMethod]
    public void Run_CallersHelp_ShowsPositionalArguments()
    {
        (int exit, string output, _) = Run("callers", "--help");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Arguments:");
        output.Should().Contain("--strict");
    }

    [TestMethod]
    public void Run_Diff_SameTraceTwice_ReportsNoChanges()
    {
        (int exit, string output, _) = Run("diff", Speedscope, Speedscope);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("diff");
        output.Should().Contain("no changes in scope");
    }

    [TestMethod]
    public void Run_DiffMissingAfterArgument_ReturnsUsageError()
    {
        // 'after' is a required positional, so a diff with only the baseline fails to parse.
        (int exit, _, string error) = Run("diff", Speedscope, "--strict");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("after");
    }

    [TestMethod]
    public void Run_Export_WritesSpeedscopeJsonToStdout()
    {
        (int exit, string output, _) = Run("export", Speedscope, "--format", "speedscope");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("speedscope.app/file-format-schema");
    }

    [TestMethod]
    public void Run_ExportChromium_WritesTraceEvents()
    {
        (int exit, string output, _) = Run("export", Speedscope, "--format", "chromium");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("traceEvents");
    }

    [TestMethod]
    public void Run_ExportHelp_ShowsFormatAndOutputOptions()
    {
        (int exit, string output, _) = Run("export", "--help");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("--format");
        output.Should().Contain("--output");
    }

    [TestMethod]
    public void Run_Tree_RendersIndentedHierarchy()
    {
        (int exit, string output, _) = Run("tree", Speedscope);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU call tree");
        output.Should().Contain("<root>");
    }

    [TestMethod]
    public void Run_TreeMaxDepthAlias_IsParsed()
    {
        // -d caps the depth: at depth 1 the root's children show but their callees do not.
        (int exit, string output, _) = Run("tree", Speedscope, "-d", "1", "--format", "json");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("Program.Main");
        output.Should().NotContain("MyApp.Work");
    }

    [TestMethod]
    public void Run_TreeNegativeMaxDepth_ReturnsUsageError()
    {
        // ConsoleAppFramework's [Range(0, ...)] rejects a negative depth before the verb runs.
        (int exit, _, _) = Run("tree", Speedscope, "--max-depth", "-1");

        exit.Should().Be(ExitCodes.UsageError);
    }

    [TestMethod]
    public void Run_TreeMaxDepthAboveCap_ReturnsUsageError()
    {
        // The depth bound is capped so a recursive deep tree cannot overflow the stack; an
        // over-cap request is rejected as a usage error before any trace work.
        (int exit, _, _) = Run("tree", Speedscope, "--max-depth", "100000");

        exit.Should().Be(ExitCodes.UsageError);
    }

    [TestMethod]
    public void Run_GcStats_ReportsCollections()
    {
        (int exit, string output, _) = Run("gcstats", Alloc);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("GC report");
        output.Should().Contain("collections");
    }

    [TestMethod]
    public void Run_GcStatsWrongFormat_ReturnsInputError()
    {
        (int exit, _, string error) = Run("gcstats", Speedscope);

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("GC report requires");
    }

    [TestMethod]
    public void Run_JitStats_ReportsMethods()
    {
        (int exit, string output, _) = Run("jitstats", Jit);

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("JIT report");
        output.Should().Contain("methods");
    }

    [TestMethod]
    public void Run_Events_RendersJsonAndPages()
    {
        (int exit, string output, _) = Run("events", Alloc, "--take", "1", "--format", "json");

        exit.Should().Be(ExitCodes.Success);
        output.Trim().Should().Contain("\"totalMatched\"");
    }

    [TestMethod]
    public void Run_EventsNameFilterAndTakeAlias_AreParsed()
    {
        // -n is the take alias; the name option filters by provider/event substring.
        (int exit, string output, _) = Run("events", Alloc, "--name", "AllocationTick", "-n", "5");

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("filter 'AllocationTick'");
    }

    [TestMethod]
    public void Run_EventsNegativeSkip_ReturnsUsageError()
    {
        // ConsoleAppFramework's [Range(0, ...)] rejects a negative skip before the verb runs.
        (int exit, _, _) = Run("events", Alloc, "--skip", "-1");

        exit.Should().Be(ExitCodes.UsageError);
    }
}
