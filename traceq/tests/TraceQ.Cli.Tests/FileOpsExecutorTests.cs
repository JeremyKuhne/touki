// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

[TestClass]
public sealed class FileOpsExecutorTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    // convert / clean write and delete the ETLX sidecar, so each test works on a
    // private temp copy of the fixture rather than the shared committed one.
    private static string CopyToTemp(string fixture, out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"traceq-fileops-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string dest = Path.Combine(tempDir, fixture);
        File.Copy(FixturePath(fixture), dest);
        return dest;
    }

    private static (int Exit, string Out, string Error) RunConvert(string path)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = FileOpsExecutor.Convert(path, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    private static (int Exit, string Out, string Error) RunClean(string path)
    {
        StringWriter output = new();
        StringWriter error = new();
        int exit = FileOpsExecutor.Clean(path, output, error);
        return (exit, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void Convert_NetTrace_ReportsTheEtlxPath()
    {
        string trace = CopyToTemp("alloc.nettrace", out string tempDir);
        try
        {
            (int exit, string output, _) = RunConvert(trace);

            exit.Should().Be(ExitCodes.Success);
            output.Should().Contain("Converted to");
            output.Should().Contain(".etlx");
            File.Exists(trace + ".etlx").Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Clean_AfterConvert_ReportsRemoval()
    {
        string trace = CopyToTemp("alloc.nettrace", out string tempDir);
        try
        {
            RunConvert(trace);

            (int exit, string output, _) = RunClean(trace);

            exit.Should().Be(ExitCodes.Success);
            output.Should().Contain("Removed");
            File.Exists(trace + ".etlx").Should().BeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Clean_WithNoCache_ReportsNothingToRemove()
    {
        string trace = CopyToTemp("alloc.nettrace", out string tempDir);
        try
        {
            (int exit, string output, _) = RunClean(trace);

            exit.Should().Be(ExitCodes.Success);
            output.Should().Contain("No ETLX cache");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void Convert_Speedscope_ReturnsInputError()
    {
        (int exit, _, string error) = RunConvert(FixturePath("folding.speedscope.json"));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().Contain("ETLX cache");
    }

    [TestMethod]
    public void Convert_MissingFile_ReturnsInputError()
    {
        (int exit, _, string error) = RunConvert(FixturePath("does-not-exist.nettrace"));

        exit.Should().Be(ExitCodes.InputError);
        error.Should().NotBeEmpty();
    }
}
