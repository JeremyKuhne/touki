// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text;

namespace TraceQ.Tracing;

/// <summary>
///  Renders a <see cref="SourceHeatmapResult"/> over a source file's text as a
///  ready-to-read annotated listing with a per-line <c>ms / %file / heat</c>
///  gutter. Shared by the <c>source_heatmap</c> MCP tool and the console
///  analyzer's <c>--heatmap</c> command.
/// </summary>
internal static class SourceAnnotator
{
    // Files larger than this render only windows around hot lines; smaller files
    // render in full so the heat map reads top to bottom.
    private const int FullRenderLineCap = 600;

    // Lines of unchanged context shown around each hot line in windowed mode.
    private const int HeatContextLines = 4;

    /// <summary>
    ///  Reads the source file at <paramref name="file"/> if it exists on disk.
    /// </summary>
    /// <param name="file">The on-disk path to read.</param>
    /// <param name="lines">The file's lines on success, otherwise empty.</param>
    /// <returns><see langword="true"/> when the file was read.</returns>
    public static bool TryReadSourceLines(string file, out string[] lines)
    {
        lines = [];
        try
        {
            if (!File.Exists(file))
            {
                return false;
            }

            lines = File.ReadAllLines(file);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    ///  Renders source with a per-line ms/percent/heat gutter, in full for small
    ///  files or as windows around hot lines for large ones.
    /// </summary>
    /// <param name="sourceLines">The source file's lines.</param>
    /// <param name="heat">The per-line heat, in any order.</param>
    /// <param name="fileMilliseconds">Total self-time attributed to the file.</param>
    /// <returns>The annotated source listing.</returns>
    public static string Render(string[] sourceLines, IReadOnlyList<HeatLine> heat, double fileMilliseconds)
    {
        Dictionary<int, HeatLine> byLine = new(heat.Count);
        double maxMs = 0.0;
        foreach (HeatLine line in heat)
        {
            byLine[line.Line] = line;
            if (line.Milliseconds > maxMs)
            {
                maxMs = line.Milliseconds;
            }
        }

        StringBuilder builder = new();
        builder.AppendLine("       ms   %file  heat      | line  source");
        builder.AppendLine("  -------  ------  --------  | ----  ------");

        if (sourceLines.Length <= FullRenderLineCap)
        {
            for (int i = 0; i < sourceLines.Length; i++)
            {
                AppendSourceLine(builder, i + 1, sourceLines[i], byLine, maxMs, fileMilliseconds);
            }

            return builder.ToString();
        }

        SortedSet<int> show = [];
        foreach (HeatLine line in heat)
        {
            for (int l = line.Line - HeatContextLines; l <= line.Line + HeatContextLines; l++)
            {
                if (l >= 1 && l <= sourceLines.Length)
                {
                    show.Add(l);
                }
            }
        }

        int previous = 0;
        foreach (int l in show)
        {
            if (previous != 0 && l > previous + 1)
            {
                builder.AppendLine("     ...");
            }

            AppendSourceLine(builder, l, sourceLines[l - 1], byLine, maxMs, fileMilliseconds);
            previous = l;
        }

        return builder.ToString();
    }

    /// <summary>
    ///  Appends one annotated source line: a populated gutter for hot lines, a
    ///  blank gutter for cold ones.
    /// </summary>
    private static void AppendSourceLine(
        StringBuilder builder,
        int lineNumber,
        string text,
        Dictionary<int, HeatLine> byLine,
        double maxMilliseconds,
        double fileMilliseconds)
    {
        if (byLine.TryGetValue(lineNumber, out HeatLine? line))
        {
            double percentOfFile = fileMilliseconds > 0 ? 100.0 * line.Milliseconds / fileMilliseconds : 0.0;
            int bars = maxMilliseconds > 0 ? (int)Math.Round(8.0 * line.Milliseconds / maxMilliseconds) : 0;
            string heat = new string('#', bars);
            builder.AppendLine($"  {line.Milliseconds,7:F1}  {percentOfFile,5:F1}  {heat,-8}  | {lineNumber,4}  {text}");
        }
        else
        {
            builder.AppendLine($"  {"",7}  {"",5}  {"",8}  | {lineNumber,4}  {text}");
        }
    }
}
