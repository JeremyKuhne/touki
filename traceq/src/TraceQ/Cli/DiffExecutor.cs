// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs a diff request against the analysis core: load the baseline and current
///  traces, rank each, compute the per-frame change, wrap it in the output
///  contract, and render it as text or JSON.
/// </summary>
/// <remarks>
///  <para>
///   Both traces are ranked with no row cap so the diff sees every frame; the
///   user's <c>--top</c> then caps the changed rows. Capping each ranking first
///   would drop a frame that is hot on only one side and misreport it as a full
///   regression or improvement.
///  </para>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as a <see cref="DiffRequest"/> and writes to the supplied writers, so it can
///   be driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class DiffExecutor
{
    /// <summary>
    ///  Executes the diff request.
    /// </summary>
    /// <param name="request">The validated diff inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(DiffRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryValidateFold(request.Fold, error))
        {
            return ExitCodes.UsageError;
        }

        if (!TraceExecution.TryLoad(request.BeforePath, request.Symbols, error, out LoadedTrace? before))
        {
            return ExitCodes.InputError;
        }

        if (!TraceExecution.TryLoad(request.AfterPath, request.Symbols, error, out LoadedTrace? after))
        {
            return ExitCodes.InputError;
        }

        RankingResult beforeRanking = Rank(before, request.Measure, request.Root, request.Fold);
        RankingResult afterRanking = Rank(after, request.Measure, request.Root, request.Fold);
        RankingDiffResult diff = RankingDiff.Diff(beforeRanking, afterRanking, request.Top);

        AnalysisResult<RankingDiffResult> envelope = new(diff, Warnings(before.Info, after.Info), SteeringHints.ForDiff(diff));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            DiffTextRenderer.Render(envelope, before.Info, after.Info, before.Aggregator.Metric, request.Measure, output);
        }

        // The strict gate trips when either trace is too poorly resolved to trust.
        bool belowThreshold =
            SymbolGate.IsBelowThreshold(before.Info.SymbolResolutionRate, before.Info.SampleCount)
            || SymbolGate.IsBelowThreshold(after.Info.SymbolResolutionRate, after.Info.SampleCount);

        return request.Strict && belowThreshold ? ExitCodes.StrictGate : ExitCodes.Success;
    }

    // Rank every frame (no row cap) so the diff is not skewed by per-side truncation;
    // RankingDiff applies the user's top to the changed rows instead.
    private static RankingResult Rank(LoadedTrace trace, Measure measure, string root, IReadOnlyList<string> fold) =>
        measure == Measure.Inclusive
            ? trace.Aggregator.InclusiveTime(root, fold, int.MaxValue)
            : trace.Aggregator.SelfTime(root, fold, int.MaxValue);

    private static IReadOnlyList<string> Warnings(TraceInfo before, TraceInfo after)
    {
        List<string> warnings = [];
        foreach (string warning in TraceExecution.ResultWarnings(before))
        {
            warnings.Add($"baseline: {warning}");
        }

        foreach (string warning in TraceExecution.ResultWarnings(after))
        {
            warnings.Add($"current: {warning}");
        }

        return warnings;
    }
}
