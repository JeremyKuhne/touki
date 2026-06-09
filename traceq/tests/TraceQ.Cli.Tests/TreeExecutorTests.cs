// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

[TestClass]
public sealed class TreeExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static TreeRequest Request(
        string path,
        string root = "",
        int maxDepth = TreeRequest.DefaultMaxDepth,
        double minPercent = 0.0,
        OutputFormat format = OutputFormat.Text,
        bool strict = false,
        IReadOnlyList<string>? fold = null) =>
        new(path, root, fold ?? FrameNames.DefaultFoldPatterns, maxDepth, minPercent, Symbols: null, format, strict);

    private static (int Exit, string Out, string Error) Run(TreeRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = TreeExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_TextFormat_RendersIndentedHierarchy()
    {
        (int exit, string output, _) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("CPU call tree");
        output.Should().Contain("<root>");
        // The leaf is indented deeper than its ancestors.
        output.Should().Contain("    MyApp.Work");
        output.Should().Contain("      MyApp.Inner");
    }

    [TestMethod]
    public void Run_JsonFormat_WritesNestedSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Speedscope, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"root\":");
        json.Should().Contain("\"children\":");
    }

    [TestMethod]
    public void Run_MaxDepthZero_ShowsRootOnly()
    {
        (int exit, string output, _) = Run(Request(Speedscope, maxDepth: 0, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        // Only the synthetic root frame appears; no method frames.
        output.Should().Contain("\"frame\":\"<root>\"");
        output.Should().NotContain("Program.Main");
    }

    [TestMethod]
    public void Run_MinPercent_PrunesSmallBranches()
    {
        // MyApp.Other is 20%; a 25% floor drops it but keeps MyApp.Work (80%).
        (int exit, string output, _) = Run(Request(Speedscope, minPercent: 25.0, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("MyApp.Work");
        output.Should().NotContain("MyApp.Other");
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
