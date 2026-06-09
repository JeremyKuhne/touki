// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs a ranking request against the analysis core: load the trace, compute the
///  self- or inclusive-time ranking, wrap it in the output contract, and render it
///  as text or JSON.
/// </summary>
/// <remarks>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as a <see cref="RankRequest"/> and writes to the supplied writers, so it can be
///   driven directly in tests as well as from the verb handlers in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class RankingExecutor
{
    /// <summary>
    ///  Executes the ranking request.
    /// </summary>
    /// <param name="request">The validated ranking inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(RankRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryValidateFold(request.Fold, error))
        {
            return ExitCodes.UsageError;
        }

        if (!TraceExecution.TryLoad(request.Path, request.Metric, request.Symbols, error, out LoadedTrace? trace))
        {
            return ExitCodes.InputError;
        }

        TraceInfo info = trace.Info;
        RankingResult ranking = request.Measure == Measure.Inclusive
            ? trace.Aggregator.InclusiveTime(request.Root, request.Fold, request.Top)
            : trace.Aggregator.SelfTime(request.Root, request.Fold, request.Top);

        AnalysisResult<RankingResult> envelope = new(
            ranking,
            TraceExecution.SymbolWarnings(info),
            SteeringHints.ForRanking(ranking));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            RankingTextRenderer.Render(envelope, info, trace.Aggregator.Metric, request.Measure, output);
        }

        return TraceExecution.StrictExit(info, request.Strict);
    }
}
