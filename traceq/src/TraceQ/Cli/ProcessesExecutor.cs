// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs a process-inventory request against the analysis core: load every process
///  in the trace, rank them by weight, wrap the result in the output contract, and
///  render it as text or JSON.
/// </summary>
/// <remarks>
///  <para>
///   The load is pinned to <see cref="ScopeRequest.AllProcesses"/>: the inventory's
///   whole purpose is to show every process so the caller can choose one to scope a
///   ranking to, so it must not auto-scope to the busiest. The execution is
///   independent of the command-line parser; it takes its inputs as a
///   <see cref="ProcessesRequest"/> and writes to the supplied writers, so it can be
///   driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class ProcessesExecutor
{
    /// <summary>
    ///  Executes the process-inventory request.
    /// </summary>
    /// <param name="request">The validated inventory inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(ProcessesRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryLoad(
            request.Path,
            TraceMetric.Cpu,
            symbols: null,
            error,
            out LoadedTrace? trace,
            ScopeRequest.AllProcesses))
        {
            return ExitCodes.InputError;
        }

        TraceInfo info = trace.Info;
        ProcessListResult processes = trace.Aggregator.Processes();

        AnalysisResult<ProcessListResult> envelope = new(processes, TraceExecution.ResultWarnings(info));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            ProcessesTextRenderer.Render(envelope, info, trace.Aggregator.Metric, output);
        }

        return ExitCodes.Success;
    }
}
