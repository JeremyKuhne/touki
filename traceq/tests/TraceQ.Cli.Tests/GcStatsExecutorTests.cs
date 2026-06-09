// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

[TestClass]
public sealed class GcStatsExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // The allocation smoke trace is captured under the GC-verbose profile, so it
    // carries the GC events the report reads.
    private static string Alloc => FixturePath("alloc.nettrace");

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static GcStatsRequest Request(string path, int top = 50, OutputFormat format = OutputFormat.Text) =>
        new(path, top, format);

    private static (int Exit, string Out, string Error) Run(GcStatsRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = GcStatsExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_TextFormat_WritesTheSummary()
    {
        (int exit, string output, _) = Run(Request(Alloc));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("GC report");
        output.Should().Contain("collections");
        output.Should().Contain("pause");
    }

    [TestMethod]
    public void Run_JsonFormat_WritesSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Alloc, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"gcCount\"");
    }

    [TestMethod]
    public void Run_TopLimitsDetailRowsButKeepsTheFullCount()
    {
        (int exit, string output, _) = Run(Request(Alloc, top: 1, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        int gcCount = int.Parse(Regex.Match(output, "\"gcCount\":(\\d+)").Groups[1].Value);
        int shown = Regex.Matches(output, "\"number\":").Count;

        // The per-collection detail is capped to top, but the aggregate count reflects
        // every collection.
        shown.Should().BeLessThanOrEqualTo(1);
        gcCount.Should().BeGreaterThanOrEqualTo(shown);
        if (gcCount > 1)
        {
            output.Should().Contain("Showing the top 1");
        }
    }

    [TestMethod]
    public void Run_MissingFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(FixturePath("does-not-exist.nettrace")));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_WrongFormat_ReturnsInputError()
    {
        // A speedscope export carries no GC events; the format guardrail rejects it
        // before any EventPipe parse.
        (int exit, _, string error) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("GC report requires");
    }
}
