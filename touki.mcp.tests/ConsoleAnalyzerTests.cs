// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Mcp;

[NotInParallel("Console")]
public class ConsoleAnalyzerTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static string Fixture => FixturePath("folding.speedscope.json");

    private static (int Exit, string Out, string Error) Run(params string[] args)
    {
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        StringWriter outWriter = new();
        StringWriter errorWriter = new();

        try
        {
            // The analyzer warns that swapping the Console writer can disrupt TUnit logging;
            // this class is serialized via [NotInParallel] and always restores the writers.
#pragma warning disable TUnit0055
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);
            int exit = ConsoleAnalyzer.Run(args);
            return (exit, outWriter.ToString(), errorWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
#pragma warning restore TUnit0055
        }
    }

    [Test]
    public void Run_NoArgs_PrintsUsageAndReturnsFailure()
    {
        (int exit, _, string error) = Run();

        exit.Should().Be(1);
        error.Should().Contain("Usage:");
    }

    [Test]
    public void Run_MissingFile_ReturnsFailure()
    {
        (int exit, _, string error) = Run(FixturePath("does-not-exist.speedscope.json"));

        exit.Should().Be(1);
        error.Should().Contain("not found");
    }

    [Test]
    public void Run_UnsupportedFormat_ReturnsFailure()
    {
        string temp = Path.GetTempFileName();
        try
        {
            (int exit, _, string error) = Run(temp);

            exit.Should().Be(1);
            error.Should().Contain("Unrecognized trace format");
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Test]
    public void Run_DefaultRankings_PrintsSelfAndInclusive()
    {
        (int exit, string output, _) = Run(Fixture);

        exit.Should().Be(0);
        output.Should().Contain("Format: Speedscope");
        output.Should().Contain("TOP SELF-TIME");
        output.Should().Contain("TOP INCLUSIVE-TIME");
        output.Should().Contain("MyApp.Inner");
    }

    [Test]
    public void Run_RootFlag_ScopesRankings()
    {
        (int exit, string output, _) = Run(Fixture, "--root", "MyApp.Other");

        exit.Should().Be(0);
        output.Should().Contain("MyApp.Other");
    }

    [Test]
    public void Run_CallersFlag_PrintsCallers()
    {
        (int exit, string output, _) = Run(Fixture, "--callers", "MyApp.Inner");

        exit.Should().Be(0);
        output.Should().Contain("CALLERS OF 'MyApp.Inner'");
        output.Should().Contain("MyApp.Work");
    }

    [Test]
    public void Run_LinesFlag_PrintsHotLinesHeader()
    {
        (int exit, string output, _) = Run(Fixture, "--lines");

        exit.Should().Be(0);
        output.Should().Contain("HOT LINES (all methods");
    }

    [Test]
    public void Run_LinesFlagWithMethod_PrintsScopedHeader()
    {
        (int exit, string output, _) = Run(Fixture, "--lines", "MyApp.Inner");

        exit.Should().Be(0);
        output.Should().Contain("method 'MyApp.Inner'");
    }

    [Test]
    public void Run_TopFlag_LimitsRows()
    {
        (int exit, string output, _) = Run(Fixture, "--top", "1");

        exit.Should().Be(0);
        // Only the top self-time frame (MyApp.Inner) should be printed, not MyApp.Other.
        output.Should().NotContain("MyApp.Other");
    }

    [Test]
    public void Run_SymbolsFlag_IgnoredForSpeedscopeAndStillRanks()
    {
        (int exit, string output, _) = Run(Fixture, "--symbols", AppContext.BaseDirectory);

        exit.Should().Be(0);
        output.Should().Contain("TOP SELF-TIME");
    }

    [Test]
    public void Run_HeatmapFlag_SpeedscopeHasNoLineData_PrintsHeaderAndEmptyNote()
    {
        (int exit, string output, _) = Run(Fixture, "--heatmap", "Engine.cs");

        exit.Should().Be(0);
        output.Should().Contain("SOURCE HEATMAP");
        output.Should().Contain("No samples attributed to 'Engine.cs'");
    }
}
