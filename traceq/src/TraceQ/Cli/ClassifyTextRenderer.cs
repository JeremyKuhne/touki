// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a runtime work-category classification as a text table: a one-line trace
///  banner, then each category on its own line with its weight and share of scope,
///  highest weight first.
/// </summary>
internal static class ClassifyTextRenderer
{
    private const int WeightColumnWidth = 16;
    private const int PercentColumnWidth = 6;

    /// <summary>
    ///  Renders the classify envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The classify result, with its warnings.</param>
    /// <param name="info">The loaded trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the weights are measured in.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<ClassifyResult> envelope,
        TraceInfo info,
        MetricInfo metric,
        TextWriter output)
    {
        ClassifyResult result = envelope.Result;
        string unit = metric.Unit;
        string scope = result.RootFrame.Length > 0 ? $"scoped to '{result.RootFrame}'" : "whole trace";

        output.WriteLine(
            $"{info.Format}  {info.SampleCount} samples  {info.TotalWeight:N1} {unit}  symbols {info.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine($"{metric.Name} by work category  -  scope {result.ScopeWeight:N2} {unit}  ({scope})");
        output.WriteLine($"  {"weight",WeightColumnWidth}  {"%",PercentColumnWidth}  category");

        foreach (CategoryRow row in result.Categories)
        {
            output.WriteLine(
                $"  {$"{row.Weight:N2} {unit}",WeightColumnWidth}  {row.PercentOfScope,PercentColumnWidth:N2}  {row.Category}");
        }

        if (result.Categories.Count == 0)
        {
            output.WriteLine("  (no samples in scope)");
        }

        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }
    }
}
