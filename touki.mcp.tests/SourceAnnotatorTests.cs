// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Mcp.Tracing;

public class SourceAnnotatorTests
{
    [Test]
    public void TryReadSourceLines_ExistingFile_ReturnsLines()
    {
        string temp = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(temp, ["first", "second", "third"]);

            SourceAnnotator.TryReadSourceLines(temp, out string[] lines).Should().BeTrue();
            lines.Should().Equal("first", "second", "third");
        }
        finally
        {
            File.Delete(temp);
        }
    }

    [Test]
    public void TryReadSourceLines_MissingFile_ReturnsFalseAndEmpty()
    {
        string missing = Path.Combine(Path.GetTempPath(), $"does-not-exist-{Guid.NewGuid():N}.cs");

        SourceAnnotator.TryReadSourceLines(missing, out string[] lines).Should().BeFalse();
        lines.Should().BeEmpty();
    }

    [Test]
    public void Render_SmallFile_RendersEveryLineWithHeaderAndGutter()
    {
        string[] source = ["alpha", "beta", "gamma"];
        List<HeatLine> heat = [new HeatLine(2, 6.0, 50.0, 3, "Foo")];

        string output = SourceAnnotator.Render(source, heat, fileMilliseconds: 12.0);

        // Header is always present.
        output.Should().Contain("ms");
        output.Should().Contain("%file");
        output.Should().Contain("heat");

        // Every source line is rendered, hot or cold.
        output.Should().Contain("alpha");
        output.Should().Contain("beta");
        output.Should().Contain("gamma");
    }

    [Test]
    public void Render_HotLine_ShowsMillisecondsPercentOfFileAndHeatBars()
    {
        string[] source = ["cold", "hot"];
        List<HeatLine> heat = [new HeatLine(2, 6.0, 30.0, 3, "Foo")];

        string output = SourceAnnotator.Render(source, heat, fileMilliseconds: 12.0);

        string[] lines = output.Split('\n');
        string hotRow = lines.Single(static l => l.Contains("hot"));

        // 6 of 12 file ms = 50.0% of file, and a full 8-bar heat (it is the max line).
        hotRow.Should().Contain("6.0");
        hotRow.Should().Contain("50.0");
        hotRow.Should().Contain("########");
    }

    [Test]
    public void Render_ColdLine_HasBlankGutter()
    {
        string[] source = ["cold", "hot"];
        List<HeatLine> heat = [new HeatLine(2, 6.0, 30.0, 3, "Foo")];

        string output = SourceAnnotator.Render(source, heat, fileMilliseconds: 12.0);

        string[] lines = output.Split('\n');
        string coldRow = lines.Single(static l => l.Contains("cold"));

        // The cold line carries no ms/percent/heat - only the line number and text.
        coldRow.Should().NotContain("#");
        coldRow.Should().Contain("cold");
    }

    [Test]
    public void Render_ZeroFileMilliseconds_DoesNotDivideByZero()
    {
        string[] source = ["only"];
        List<HeatLine> heat = [new HeatLine(1, 5.0, 0.0, 1, "Foo")];

        string output = SourceAnnotator.Render(source, heat, fileMilliseconds: 0.0);

        string[] lines = output.Split('\n');
        string row = lines.Single(static l => l.Contains("only"));
        // Percent-of-file collapses to 0.0 rather than NaN/Infinity.
        row.Should().Contain("0.0");
        row.Should().Contain("5.0");
    }

    [Test]
    public void Render_LargeFile_WindowsAroundHotLinesWithGapMarkers()
    {
        string[] source = new string[700];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = $"line{i + 1}";
        }

        // Two hot lines far apart force two separate windows with a gap between them.
        List<HeatLine> heat =
        [
            new HeatLine(100, 4.0, 40.0, 1, "Foo"),
            new HeatLine(400, 6.0, 60.0, 1, "Bar")
        ];

        string output = SourceAnnotator.Render(source, heat, fileMilliseconds: 10.0);

        // Hot lines and their context are shown.
        output.Should().Contain("line100");
        output.Should().Contain("line400");

        // A line in neither window is omitted.
        output.Should().NotContain("line250\n");

        // A gap marker separates the two windows.
        output.Should().Contain("...");
    }
}
