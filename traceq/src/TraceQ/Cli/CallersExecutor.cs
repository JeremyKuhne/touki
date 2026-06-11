// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs a callers request against the analysis core: load the trace, report the
///  immediate callers of the focus frame, wrap the result in the output contract,
///  and render it as text or JSON.
/// </summary>
/// <remarks>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as a <see cref="CallersRequest"/> and writes to the supplied writers, so it can
///   be driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class CallersExecutor
{
    /// <summary>
    ///  Executes the callers request.
    /// </summary>
    /// <param name="request">The validated callers inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(CallersRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryLoad(request.Path, TraceMetric.Cpu, request.Symbols, error, out LoadedTrace? trace, request.Scope))
        {
            return ExitCodes.InputError;
        }

        TraceInfo info = trace.Info;
        CallersResult callers = trace.Aggregator.CallersOf(request.Frame, request.Root, request.Top);

        AnalysisResult<CallersResult> envelope = new(
            callers,
            TraceExecution.ResultWarnings(info),
            SteeringHints.ForCallers(callers));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            CallersTextRenderer.Render(envelope, info, trace.Aggregator.Metric, output);
        }

        return TraceExecution.StrictExit(info, request.Strict);
    }
}
