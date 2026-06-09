// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a hot-lines result as the dense, fixed-width text view a human reads at
///  the terminal: a one-line trace banner, the scope, the ranked source lines in
///  aligned columns, then any quality warnings.
/// </summary>
internal static class LinesTextRenderer
{
    private const int WeightColumnWidth = 16;
    private const int PercentColumnWidth = 6;

    /// <summary>
    ///  Renders the hot-lines envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The hot-lines result, with its warnings.</param>
    /// <param name="info">The loaded trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the weights are measured in.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<LineRankingResult> envelope,
        TraceInfo info,
        MetricInfo metric,
        TextWriter output)
    {
        LineRankingResult lines = envelope.Result;
        string unit = metric.Unit;
        string scope = lines.MethodFilter.Length > 0 ? $"method '{lines.MethodFilter}'" : "all methods";

        output.WriteLine(
            $"{info.Format}  {info.SampleCount} samples  {info.TotalWeight:N1} {unit}  symbols {info.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine($"{metric.Name} hot lines  -  scope {lines.ScopeWeight:N2} {unit}  ({scope})");

        if (lines.Rows.Count == 0)
        {
            output.WriteLine("  (no lines in scope)");
        }
        else
        {
            output.WriteLine($"  {"weight",WeightColumnWidth}  {"%",PercentColumnWidth}  location  method");
            foreach (LineRow row in lines.Rows)
            {
                output.WriteLine(
                    $"  {$"{row.Weight:N2} {unit}",WeightColumnWidth}  {row.PercentOfScope,PercentColumnWidth:N2}  {row.Location}  {row.Method}");
            }
        }

        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }
    }
}
