// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs a source heat-map request against the analysis core: load the trace, build
///  the per-line heat map for a source file, wrap the result in the output contract,
///  and render it as text (optionally overlaid on the source) or JSON.
/// </summary>
/// <remarks>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as a <see cref="HeatmapRequest"/> and writes to the supplied writers, so it can
///   be driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class HeatmapExecutor
{
    /// <summary>
    ///  Executes the source heat-map request.
    /// </summary>
    /// <param name="request">The validated heat-map inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(HeatmapRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryValidateFold(request.Fold, error))
        {
            return ExitCodes.UsageError;
        }

        if (!TraceExecution.TryLoad(request.Path, TraceMetric.Cpu, request.Symbols, error, out LoadedTrace? trace, request.Scope))
        {
            return ExitCodes.InputError;
        }

        TraceInfo info = trace.Info;

        // The trace records the build-time file name, not its full path, so match on
        // the file name; the original path is kept for the source overlay in text mode.
        string fileName = System.IO.Path.GetFileName(request.File);
        SourceHeatmapResult heatmap = trace.Aggregator.SourceHeatmap(fileName, request.Fold);

        AnalysisResult<SourceHeatmapResult> envelope = new(heatmap, TraceExecution.ResultWarnings(info));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            HeatmapTextRenderer.Render(envelope, info, trace.Aggregator.Metric, request.File, output);
        }

        return TraceExecution.StrictExit(info, request.Strict);
    }
}
