// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing.Providers;

namespace TraceQ.Cli;

/// <summary>
///  Renders a GC-stats result as the dense, fixed-width text view a human reads at
///  the terminal: a header, the aggregate counts and pause / heap summary, then the
///  per-collection detail in aligned columns, and finally any warnings.
/// </summary>
/// <remarks>
///  <para>
///   This is the text half of the GC report; the JSON half is
///   <see cref="OutputJson"/>. Both render the same <see cref="AnalysisResult{T}"/>
///   envelope.
///  </para>
/// </remarks>
internal static class GcStatsTextRenderer
{
    /// <summary>
    ///  Renders the GC-stats envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The GC report, with its warnings.</param>
    /// <param name="path">The trace path, for the header line.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(AnalysisResult<GcStatsResult> envelope, string path, TextWriter output)
    {
        GcStatsResult report = envelope.Result;

        output.WriteLine($"GC report  -  {path}");
        output.WriteLine();

        if (report.GcCount == 0)
        {
            output.WriteLine("  (no collections; the trace carries no GC events)");
            RenderWarnings(envelope, output);
            return;
        }

        output.WriteLine(
            $"  {report.GcCount} collections   gen0 {report.Gen0Count}  gen1 {report.Gen1Count}  gen2 {report.Gen2Count}");
        output.WriteLine(
            $"  pause   total {report.TotalPauseMs:N2} ms   max {report.MaxPauseMs:N2} ms   mean {report.MeanPauseMs:N2} ms");
        output.WriteLine(
            $"  heap    peak {report.PeakHeapSizeMB:N2} MB   promoted {report.TotalPromotedMB:N2} MB");
        output.WriteLine();

        output.WriteLine(
            $"  {"#",6}  {"gen",3}  {"pause(ms)",12}  {"heap(MB)",12}  {"promoted(MB)",13}  kind / reason");
        foreach (GcRecord gc in report.Gcs)
        {
            output.WriteLine(
                $"  {gc.Number,6}  {gc.Generation,3}  {gc.PauseMs,12:N2}  {gc.HeapSizeAfterMB,12:N2}  "
                + $"{gc.PromotedMB,13:N2}  {gc.Kind} / {gc.Reason}");
        }

        RenderWarnings(envelope, output);
    }

    private static void RenderWarnings(AnalysisResult<GcStatsResult> envelope, TextWriter output)
    {
        foreach (string warning in envelope.Warnings)
        {
            output.WriteLine($"! {warning}");
        }
    }
}
