// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs a classify request against the analysis core: load the CPU view, bucket
///  self-time by runtime work category, wrap the result in the output contract, and
///  render it as text or JSON.
/// </summary>
/// <remarks>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs as a
///   <see cref="ClassifyRequest"/> and writes to the supplied writers, so it can be
///   driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class ClassifyExecutor
{
    /// <summary>
    ///  Executes the classify request.
    /// </summary>
    /// <param name="request">The validated classify inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(ClassifyRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryLoad(
            request.Path, TraceMetric.Cpu, request.Symbols, error, out LoadedTrace? trace, request.Scope, request.SymbolOptions))
        {
            return ExitCodes.InputError;
        }

        TraceInfo info = trace.Info;
        ClassifyResult classification = trace.Aggregator.Classify(request.Root);

        AnalysisResult<ClassifyResult> envelope = new(classification, TraceExecution.ResultWarnings(info));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            ClassifyTextRenderer.Render(envelope, info, trace.Aggregator.Metric, output);
        }

        return TraceExecution.StrictExit(info, request.Strict);
    }
}
