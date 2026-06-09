// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

[TestClass]
public sealed class DiffExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Speedscope => FixturePath("folding.speedscope.json");

    // A minimal evented speedscope where frame A wraps frame B: the B close at
    // 'bCloseAt' fixes B's self-weight, and A's self-weight is the remainder up to
    // 'aCloseAt'. Authoring both sides lets a test assert exact diff deltas.
    private static string TwoFrameSpeedscope(int bCloseAt, int aCloseAt) =>
        $$"""
        {"shared":{"frames":[{"name":"A"},{"name":"B"}]},
         "profiles":[{"type":"evented","name":"t","unit":"milliseconds","startValue":0,"endValue":{{aCloseAt}},
          "events":[{"type":"O","frame":0,"at":0},{"type":"O","frame":1,"at":0},
                    {"type":"C","frame":1,"at":{{bCloseAt}}},{"type":"C","frame":0,"at":{{aCloseAt}}}]}]}
        """;

    private static DiffRequest Request(
        string beforePath,
        string afterPath,
        Measure measure = Measure.Self,
        string root = "",
        int top = 25,
        OutputFormat format = OutputFormat.Text,
        bool strict = false,
        IReadOnlyList<string>? fold = null) =>
        new(beforePath, afterPath, root, top, fold ?? FrameNames.DefaultFoldPatterns, measure, format, Symbols: null, strict);

    private static (int Exit, string Out, string Error) Run(DiffRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = DiffExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_SameTraceTwice_ReportsNoChanges()
    {
        (int exit, string output, _) = Run(Request(Speedscope, Speedscope));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("no changes in scope");
    }

    [TestMethod]
    public void Run_ChangedWeights_ShowsPerFrameDeltas()
    {
        // before: B self 5, A self 5; after: B self 8, A self 2.
        string before = Path.Combine(Path.GetTempPath(), $"traceq-before-{Guid.NewGuid():N}.speedscope.json");
        string after = Path.Combine(Path.GetTempPath(), $"traceq-after-{Guid.NewGuid():N}.speedscope.json");
        File.WriteAllText(before, TwoFrameSpeedscope(bCloseAt: 5, aCloseAt: 10));
        File.WriteAllText(after, TwoFrameSpeedscope(bCloseAt: 8, aCloseAt: 10));
        try
        {
            (int exit, string output, _) = Run(Request(before, after, format: OutputFormat.Json));

            exit.Should().Be(ExitCodes.Success);
            // B regressed +3, A improved -3; both rows present.
            output.Should().Contain("\"frame\":\"B\"");
            output.Should().Contain("\"frame\":\"A\"");
            output.Should().Contain("\"delta\":3");
            output.Should().Contain("\"delta\":-3");
        }
        finally
        {
            File.Delete(before);
            File.Delete(after);
        }
    }

    [TestMethod]
    public void Run_JsonFormat_WritesSingleLineEnvelope()
    {
        (int exit, string output, _) = Run(Request(Speedscope, Speedscope, format: OutputFormat.Json));

        exit.Should().Be(ExitCodes.Success);
        string json = output.Trim();
        json.Should().NotContain("\n");
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"scopeDelta\"");
    }

    [TestMethod]
    public void Run_InvalidFoldPattern_ReturnsUsageError()
    {
        (int exit, _, string error) = Run(Request(Speedscope, Speedscope, fold: ["("]));

        exit.Should().Be(ExitCodes.UsageError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_MissingBeforeFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(FixturePath("nope.nettrace"), Speedscope));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }

    [TestMethod]
    public void Run_MissingAfterFile_ReturnsInputError()
    {
        (int exit, _, string error) = Run(Request(Speedscope, FixturePath("nope.nettrace")));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }
}
