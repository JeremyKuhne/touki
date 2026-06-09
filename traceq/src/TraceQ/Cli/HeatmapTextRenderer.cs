// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a source heat-map result as text: a one-line trace banner, the file's
///  share of the trace, then - when the source file is on disk - the heat overlaid
///  on the source, otherwise a per-line list. Quality warnings follow.
/// </summary>
internal static class HeatmapTextRenderer
{
    private const int WeightColumnWidth = 16;
    private const int PercentColumnWidth = 6;

    /// <summary>
    ///  Renders the heat-map envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The heat-map result, with its warnings.</param>
    /// <param name="info">The loaded trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the weights are measured in.</param>
    /// <param name="sourceFile">
    ///  The source file path as supplied; when it resolves to a readable file the heat
    ///  is overlaid on the source rather than listed by line.
    /// </param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<SourceHeatmapResult> envelope,
        TraceInfo info,
        MetricInfo metric,
        string sourceFile,
        TextWriter output)
    {
        SourceHeatmapResult heatmap = envelope.Result;
        string unit = metric.Unit;
        double percentOfTrace = heatmap.ScopeWeight > 0 ? 100.0 * heatmap.FileWeight / heatmap.ScopeWeight : 0.0;

        output.WriteLine(
            $"{info.Format}  {info.SampleCount} samples  {info.DurationMs:N1} ms  symbols {info.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine(
            $"{metric.Name} source heatmap '{heatmap.File}'  -  {heatmap.FileWeight:N2} {unit} "
            + $"({percentOfTrace:N2}% of trace)");

        if (heatmap.Lines.Count == 0)
        {
            output.WriteLine(
                $"  (no samples attributed to '{heatmap.File}'; the trace needs source line info "
                + "(--symbols) and the file name must match)");
        }
        else if (SourceAnnotator.TryReadSourceLines(sourceFile, out string[] sourceLines))
        {
            output.WriteLine();
            output.Write(SourceAnnotator.Render(sourceLines, heatmap.Lines, heatmap.FileWeight));
        }
        else
        {
            output.WriteLine("  (pass the source file's on-disk path to overlay the heat; showing line data only)");
            output.WriteLine($"  {"weight",WeightColumnWidth}  {"%",PercentColumnWidth}  line  method");
            foreach (HeatLine row in heatmap.Lines)
            {
                output.WriteLine(
                    $"  {$"{row.Weight:N2} {unit}",WeightColumnWidth}  {row.PercentOfScope,PercentColumnWidth:N2}  "
                    + $"{row.Line,5}  {row.Method}");
            }
        }

        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }
    }
}
