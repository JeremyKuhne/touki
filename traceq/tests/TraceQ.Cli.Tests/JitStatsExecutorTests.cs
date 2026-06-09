// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

[TestClass]
public sealed class JitStatsExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Jit => FixturePath("jit.nettrace");

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static JitStatsRequest Request(string path, int top = 25, OutputFormat format = OutputFormat.Text) =>
        new(path, top, format);

    private static (int Exit, string Out, string Error) Run(JitStatsRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = JitStatsExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_TextFormat_WritesTheSummary()
    {
        (int exit, string output, _) = Run(Request(Jit));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("JIT report");
        output.Should().Contain("methods");
        output.Should().Contain("compile");
    }

    [TestMethod]
    public void Run_JsonFormat_WritesSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Jit, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"methodCount\"");
    }

    [TestMethod]
    public void Run_TopLimitsDetailRowsButKeepsTheFullCount()
    {
        (int exit, string output, _) = Run(Request(Jit, top: 1, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        int methodCount = int.Parse(Regex.Match(output, "\"methodCount\":(\\d+)").Groups[1].Value);
        int shown = Regex.Matches(output, "\"methodName\":").Count;

        shown.Should().BeLessThanOrEqualTo(1);
        methodCount.Should().BeGreaterThanOrEqualTo(shown);
        if (methodCount > 1)
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
        (int exit, _, string error) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("JIT report requires");
    }
}
