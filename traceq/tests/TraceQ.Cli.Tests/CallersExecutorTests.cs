// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

[TestClass]
public sealed class CallersExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static CallersRequest Request(
        string path,
        string frame = "MyApp.Inner",
        string root = "",
        int top = 25,
        OutputFormat format = OutputFormat.Text,
        bool strict = false) =>
        new(path, frame, root, top, Symbols: null, format, strict);

    private static (int Exit, string Out, string Error) Run(CallersRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = CallersExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_TextFormat_WritesFocusAndCallers()
    {
        (int exit, string output, _) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("callers of 'MyApp.Inner'");
        output.Should().Contain("MyApp.Work");
    }

    [TestMethod]
    public void Run_JsonFormat_WritesSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Speedscope, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"focus\":\"MyApp.Inner\"");
    }

    [TestMethod]
    public void Run_UnknownFrame_SucceedsWithNoCallers()
    {
        (int exit, string output, _) = Run(Request(Speedscope, frame: "NoSuchFrame"));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("no callers in scope");
    }

    [TestMethod]
    public void Run_MissingFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(FixturePath("does-not-exist.nettrace")));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }
}
