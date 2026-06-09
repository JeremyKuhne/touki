// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing.Providers;

namespace TraceQ.Cli;

/// <summary>
///  Runs a GC-stats request against the analysis core: read the structured
///  garbage-collection records, cap the per-collection detail to the hottest
///  pauses, wrap the result in the output contract, and render it as text or JSON.
/// </summary>
/// <remarks>
///  <para>
///   Unlike the ranking verbs this is a structured report, not a stack ranking, so
///   it does not flow through the folding aggregator. The aggregate summary always
///   reflects every collection; only the per-collection detail list is capped, and
///   ranked by pause time so the most disruptive collections are kept.
///  </para>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as a <see cref="GcStatsRequest"/> and writes to the supplied writers, so it
///   can be driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class GcStatsExecutor
{
    /// <summary>
    ///  Executes the GC-stats request.
    /// </summary>
    /// <param name="request">The validated GC-stats inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(GcStatsRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryReadNetTraceReport(
            request.Path,
            "GC",
            () => new GcStatsProvider().Read(request.Path),
            error,
            out GcStatsResult? full))
        {
            return ExitCodes.InputError;
        }

        // Keep the full aggregate summary, but cap the per-collection detail to the
        // hottest pauses so a long trace cannot blow the output budget.
        List<string> warnings = [];
        IReadOnlyList<GcRecord> shown = full.Gcs;
        if (shown.Count > request.Top)
        {
            shown = [.. shown.OrderByDescending(static g => g.PauseMs).Take(request.Top)];
            warnings.Add($"Showing the top {request.Top} of {full.GcCount} collections by pause time.");
        }

        GcStatsResult report = full with { Gcs = shown };
        AnalysisResult<GcStatsResult> envelope = new(report, warnings);

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            GcStatsTextRenderer.Render(envelope, request.Path, output);
        }

        return ExitCodes.Success;
    }
}
