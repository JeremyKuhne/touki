// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Renders a trace's process inventory as a text table a human reads at the
///  terminal: a one-line trace banner, then each process on its own line with its
///  weight, share of the capture, and sample count, highest weight first.
/// </summary>
internal static class ProcessesTextRenderer
{
    private const int WeightColumnWidth = 16;
    private const int PercentColumnWidth = 6;
    private const int SamplesColumnWidth = 9;

    /// <summary>
    ///  Renders the process-inventory envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The process-inventory result, with its warnings.</param>
    /// <param name="info">The loaded trace's metadata, for the banner line.</param>
    /// <param name="metric">The metric the weights are measured in.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(
        AnalysisResult<ProcessListResult> envelope,
        TraceInfo info,
        MetricInfo metric,
        TextWriter output)
    {
        ProcessListResult result = envelope.Result;
        string unit = metric.Unit;

        output.WriteLine(
            $"{info.Format}  {info.SampleCount} samples  {info.TotalWeight:N1} {unit}  symbols {info.SymbolResolutionRate:P0}");
        output.WriteLine();
        output.WriteLine(
            $"processes by {metric.Name}  -  {result.TotalSamples} samples  {result.TotalWeight:N1} {unit}");
        output.WriteLine(
            $"  {"weight",WeightColumnWidth}  {"%",PercentColumnWidth}  {"samples",SamplesColumnWidth}  process");

        foreach (ProcessSummary process in result.Processes)
        {
            // A single-process trace format carries an empty process label; name it so
            // the row is not blank.
            string name = process.Process.Length > 0 ? process.Process : "(single process)";
            output.WriteLine(
                $"  {$"{process.Weight:N2} {unit}",WeightColumnWidth}  {process.PercentOfScope,PercentColumnWidth:N2}  "
                + $"{process.SampleCount,SamplesColumnWidth}  {name}");
        }

        if (result.Processes.Count == 0)
        {
            output.WriteLine("  (no samples)");
        }

        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }
    }
}
