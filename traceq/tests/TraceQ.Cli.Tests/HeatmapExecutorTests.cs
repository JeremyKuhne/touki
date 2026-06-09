// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

[TestClass]
public sealed class HeatmapExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static HeatmapRequest Request(
        string path,
        string file = "Program.cs",
        OutputFormat format = OutputFormat.Text,
        bool strict = false,
        IReadOnlyList<string>? fold = null) =>
        new(path, file, fold ?? FrameNames.DefaultFoldPatterns, Symbols: null, format, strict);

    private static (int Exit, string Out, string Error) Run(HeatmapRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = HeatmapExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_FileWithNoSamples_SucceedsWithNotice()
    {
        // The speedscope fixture carries no per-frame source locations, so no line
        // attributes; the verb still completes and explains the empty result.
        (int exit, string output, _) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("source heatmap 'Program.cs'");
        output.Should().Contain("no samples attributed");
    }

    [TestMethod]
    public void Run_JsonFormat_WritesSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Speedscope, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"file\":\"Program.cs\"");
    }

    [TestMethod]
    public void Run_InvalidFoldPattern_ReturnsUsageError()
    {
        (int exit, _, string error) = Run(Request(Speedscope, fold: ["("]));

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_MissingFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(FixturePath("does-not-exist.nettrace")));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }
}
