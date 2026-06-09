// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a ranking result as the dense, fixed-width text view a human reads at
///  the terminal: a one-line trace banner, the ranked frames in aligned columns,
///  then any quality warnings and the next-step steering hints.
/// </summary>
/// <remarks>
///  <para>
///   This is the text half of the output contract; the JSON half is
///   <see cref="OutputJson"/>. Both render the same <see cref="AnalysisResult{T}"/>
///   envelope so the warnings and hints a verb attaches reach a human and an agent
///   alike.
///  </para>
/// </remarks>
internal static class RankingTextRenderer
{
    private const int WeightColumnWidth = 16;
    private const int PercentColumnWidth = 6;

    /// <summary>
    ///  Renders the ranking envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The ranking result, with its warnings and hints.</param>
    /// <param name="info">The loaded trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the ranked weights are measured in.</param>
    /// <param name="measure">Which measure the ranking reports.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<RankingResult> envelope,
        TraceInfo info,
        MetricInfo metric,
        Measure measure,
        TextWriter output)
    {
        RankingResult ranking = envelope.Result;
        string unit = metric.Unit;
        string measureLabel = measure == Measure.Inclusive ? "inclusive-time" : "self-time";
        string scope = ranking.RootFrame.Length > 0 ? $"scoped to '{ranking.RootFrame}'" : "whole trace";

        // The banner total is the sum of the sample weights in the metric's own unit
        // (ms for CPU, bytes for allocation), so it reads correctly for every family.
        output.WriteLine(
            $"{info.Format}  {info.SampleCount} samples  {info.DurationMs:N1} {unit}  symbols {info.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine($"{metric.Name} {measureLabel}  -  scope {ranking.ScopeWeight:N2} {unit}  ({scope})");

        if (ranking.Rows.Count == 0)
        {
            output.WriteLine("  (no frames in scope)");
        }
        else
        {
            output.WriteLine($"  {"weight",WeightColumnWidth}  {"%",PercentColumnWidth}  frame");
            foreach (RankRow row in ranking.Rows)
            {
                output.WriteLine(
                    $"  {FormatWeight(row.Weight, unit),WeightColumnWidth}  {row.PercentOfScope,PercentColumnWidth:N2}  {row.Frame}");
            }
        }

        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }

        foreach (string hint in envelope.Hints)
        {
            output.WriteLine($"> {hint}");
        }
    }

    private static string FormatWeight(double weight, string unit) => $"{weight:N2} {unit}";
}
