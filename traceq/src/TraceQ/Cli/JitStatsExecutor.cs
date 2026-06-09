// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing.Providers;

namespace TraceQ.Cli;

/// <summary>
///  Runs a JIT-stats request against the analysis core: read the structured
///  per-method compilation records, cap the detail to the costliest compiles, wrap
///  the result in the output contract, and render it as text or JSON.
/// </summary>
/// <remarks>
///  <para>
///   Unlike the ranking verbs this is a structured report, not a stack ranking, so
///   it does not flow through the folding aggregator. The aggregate summary always
///   reflects every method; only the per-method detail list is capped, and ranked
///   by compile time so the costliest compiles are kept - a startup trace can jit
///   thousands of methods.
///  </para>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as a <see cref="JitStatsRequest"/> and writes to the supplied writers, so it
///   can be driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class JitStatsExecutor
{
    /// <summary>
    ///  Executes the JIT-stats request.
    /// </summary>
    /// <param name="request">The validated JIT-stats inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(JitStatsRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryReadNetTraceReport(
            request.Path,
            "JIT",
            () => new JitStatsProvider().Read(request.Path),
            error,
            out JitStatsResult? full))
        {
            return ExitCodes.InputError;
        }

        // Keep the full aggregate summary, but cap the per-method detail to the
        // costliest compiles so a startup trace's thousands of methods cannot blow
        // the output budget.
        List<string> warnings = [];
        IReadOnlyList<JitMethodRecord> shown = full.Methods;
        if (shown.Count > request.Top)
        {
            shown = [.. shown.OrderByDescending(static m => m.CompileMs).Take(request.Top)];
            warnings.Add($"Showing the top {request.Top} of {full.MethodCount} methods by compile time.");
        }

        JitStatsResult report = full with { Methods = shown };
        AnalysisResult<JitStatsResult> envelope = new(report, warnings);

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            JitStatsTextRenderer.Render(envelope, request.Path, output);
        }

        return ExitCodes.Success;
    }
}
