// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.Json;
using TraceQ.Output;
using TraceQ.Server;
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
        // The fold patterns are user-supplied; validate them up front so a malformed
        // regex is a defined usage error instead of an unhandled crash later in the rank.
        try
        {
            _ = FrameNames.CompileFoldPatterns(request.Fold);
        }
        catch (ArgumentException ex)
        {
            error.WriteLine(ex.Message);
            return ExitCodes.UsageError;
        }

        TraceStore store = new();
        LoadedTrace trace;
        try
        {
            trace = store.Get(request.Path, request.Symbols);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or JsonException
            or ArgumentException)
        {
            // Missing, unreadable, or malformed trace input terminates with a defined
            // exit code rather than crashing the process.
            error.WriteLine(ex.Message);
            return ExitCodes.InputError;
        }

        TraceInfo info = trace.Info;
        RankingResult ranking = request.Measure == Measure.Inclusive
            ? trace.Aggregator.InclusiveTime(request.Root, request.Fold, request.Top)
            : trace.Aggregator.SelfTime(request.Root, request.Fold, request.Top);

        List<string> warnings = [];
        if (SymbolGate.TryGetWarning(info.SymbolResolutionRate, info.SampleCount, out string? warning))
        {
            warnings.Add(warning);
        }

        AnalysisResult<RankingResult> envelope = new(ranking, warnings, SteeringHints.ForRanking(ranking));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            RankingTextRenderer.Render(envelope, info, trace.Aggregator.Metric, request.Measure, output);
        }

        return request.Strict && SymbolGate.IsBelowThreshold(info.SymbolResolutionRate, info.SampleCount)
            ? ExitCodes.StrictGate
            : ExitCodes.Success;
    }
}
