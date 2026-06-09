// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a ranking diff as dense, fixed-width text: a two-line banner for the
///  baseline and current traces, the scoped totals and their delta, the per-frame
///  changes in aligned columns (largest change first), then any warnings and the
///  steering hint.
/// </summary>
internal static class DiffTextRenderer
{
    private const int WeightColumnWidth = 14;

    /// <summary>
    ///  Renders the diff envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The diff result, with its warnings and hints.</param>
    /// <param name="before">The baseline trace's metadata, for the banner line.</param>
    /// <param name="after">The current trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the weights are measured in.</param>
    /// <param name="measure">Which measure the rankings compared.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<RankingDiffResult> envelope,
        TraceInfo before,
        TraceInfo after,
        MetricInfo metric,
        Measure measure,
        TextWriter output)
    {
        RankingDiffResult diff = envelope.Result;
        string unit = metric.Unit;
        string measureLabel = measure == Measure.Inclusive ? "inclusive-time" : "self-time";

        output.WriteLine(
            $"baseline  {before.Format}  {before.SampleCount} samples  symbols {before.SymbolResolutionRate:P0}");
        output.WriteLine(
            $"current   {after.Format}  {after.SampleCount} samples  symbols {after.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine(
            $"{metric.Name} {measureLabel} diff  -  scope {diff.BeforeScopeWeight:N2} -> {diff.AfterScopeWeight:N2} {unit} "
            + $"(delta {Signed(diff.ScopeDelta)} {unit})");

        if (diff.Rows.Count == 0)
        {
            output.WriteLine("  (no changes in scope)");
        }
        else
        {
            output.WriteLine($"  {"before",WeightColumnWidth}  {"after",WeightColumnWidth}  {"delta",WeightColumnWidth}  frame");
            foreach (DiffRow row in diff.Rows)
            {
                output.WriteLine(
                    $"  {$"{row.BeforeWeight:N2} {unit}",WeightColumnWidth}  {$"{row.AfterWeight:N2} {unit}",WeightColumnWidth}  "
                    + $"{$"{Signed(row.Delta)} {unit}",WeightColumnWidth}  {row.Frame}");
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

    // A leading '+' marks a positive change (a regression); negatives already carry
    // their '-' from the numeric format.
    private static string Signed(double value) => value >= 0 ? $"+{value:N2}" : value.ToString("N2");
}
