// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

[TestClass]
public sealed class ExportExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Speedscope => FixturePath("folding.speedscope.json");

    private static ExportRequest Request(
        string path,
        ExportFormat format = ExportFormat.Speedscope,
        string? output = null,
        string name = "traceq") =>
        new(path, format, output, Symbols: null, name);

    private static (int Exit, string Out, string Error) Run(ExportRequest request)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = ExportExecutor.Run(request, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Run_Speedscope_WritesProfileJsonToOutput()
    {
        (int exit, string output, _) = Run(Request(Speedscope));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("speedscope.app/file-format-schema");
        output.Should().Contain("Program.Main");
    }

    [TestMethod]
    public void Run_Chromium_WritesTraceEventsToOutput()
    {
        (int exit, string output, _) = Run(Request(Speedscope, ExportFormat.Chromium));

        exit.Should().Be(ExitCodes.Success);
        output.Should().Contain("traceEvents");
    }

    [TestMethod]
    public void Run_WithOutputFile_WritesFileAndConfirms()
    {
        string path = Path.Combine(Path.GetTempPath(), $"traceq-export-{Guid.NewGuid():N}.json");
        try
        {
            (int exit, string output, _) = Run(Request(Speedscope, output: path));

            exit.Should().Be(ExitCodes.Success);
            output.Should().Contain("Wrote");
            File.Exists(path).Should().BeTrue();
            File.ReadAllText(path).Should().Contain("speedscope.app/file-format-schema");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Run_ToStdout_MatchesFileOutputByteForByte()
    {
        // Exporting to stdout must produce exactly the bytes the --output file gets - no
        // trailing newline - so a redirect (traceq export ... > out.json) yields a file a
        // viewer reads identically.
        string path = Path.Combine(Path.GetTempPath(), $"traceq-export-{Guid.NewGuid():N}.json");
        try
        {
            (int fileExit, _, _) = Run(Request(Speedscope, output: path));
            (int stdoutExit, string stdout, _) = Run(Request(Speedscope));

            fileExit.Should().Be(ExitCodes.Success);
            stdoutExit.Should().Be(ExitCodes.Success);
            stdout.Should().Be(File.ReadAllText(path));
            stdout.Should().EndWith("}");
        }
        finally
        {
            File.Delete(path);
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
    public void Run_UnwritableOutputPath_ReturnsInputError()
    {
        // Treat an existing file as a directory: writing under it fails on every OS.
        string file = Path.GetTempFileName();
        try
        {
            string unwritable = Path.Combine(file, "child.json");

            (int exit, _, string error) = Run(Request(Speedscope, output: unwritable));

            exit.Should().Be(ExitCodes.InputError);
            error.Should().NotBeEmpty();
        }
        finally
        {
            File.Delete(file);
        }
    }
}
