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
        output.Should().Contain("callers");
        output.Should().Contain("lines");
        output.Should().Contain("heatmap");
        output.Should().Contain("diff");
        output.Should().Contain("export");
        output.Should().Contain("tree");
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
    public void Run_BadMetric_ReturnsUsageError()
    {
        (int exit, _, string error) = Run("rank", Speedscope, "--metric", "alloc");

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().Contain("alloc");
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
}
