// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a callers result as the dense, fixed-width text view a human reads at
///  the terminal: a one-line trace banner, the focus frame's total, the immediate
///  callers in aligned columns, then any quality warnings and steering hints.
/// </summary>
internal static class CallersTextRenderer
{
    private const int WeightColumnWidth = 16;
    private const int PercentColumnWidth = 6;

    /// <summary>
    ///  Renders the callers envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The callers result, with its warnings and hints.</param>
    /// <param name="info">The loaded trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the weights are measured in.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<CallersResult> envelope,
        TraceInfo info,
        MetricInfo metric,
        TextWriter output)
    {
        CallersResult callers = envelope.Result;
        string unit = metric.Unit;

        output.WriteLine(
            $"{info.Format}  {info.SampleCount} samples  {info.TotalWeight:N1} {unit}  symbols {info.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine(
            $"{metric.Name} callers of '{callers.Focus}'  -  {callers.TargetWeight:N2} {unit} "
            + $"({callers.PercentOfScope:N2}% of scope)");

        if (callers.Callers.Count == 0)
        {
            output.WriteLine("  (no callers in scope)");
        }
        else
        {
            output.WriteLine($"  {"weight",WeightColumnWidth}  {"%",PercentColumnWidth}  caller");
            foreach (CallerRow row in callers.Callers)
            {
                output.WriteLine(
                    $"  {$"{row.Weight:N2} {unit}",WeightColumnWidth}  {row.PercentOfTarget,PercentColumnWidth:N2}  {row.Caller}");
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
}
