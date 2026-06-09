// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  Runs a call-tree request against the analysis core: load the trace, build the
///  top-down call tree, wrap it in the output contract, and render it as an indented
///  tree or JSON.
/// </summary>
/// <remarks>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as a <see cref="TreeRequest"/> and writes to the supplied writers, so it can be
///   driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class TreeExecutor
{
    /// <summary>
    ///  Executes the call-tree request.
    /// </summary>
    /// <param name="request">The validated call-tree inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(TreeRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryValidateFold(request.Fold, error))
        {
            return ExitCodes.UsageError;
        }

        if (!TraceExecution.TryLoad(request.Path, request.Symbols, error, out LoadedTrace? trace))
        {
            return ExitCodes.InputError;
        }

        TraceInfo info = trace.Info;
        CallTreeResult tree = trace.Aggregator.CallTree(
            request.Root,
            request.Fold,
            request.MaxDepth,
            request.MinPercent);

        AnalysisResult<CallTreeResult> envelope = new(tree, TraceExecution.SymbolWarnings(info));

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            TreeTextRenderer.Render(envelope, info, trace.Aggregator.Metric, output);
        }

        return TraceExecution.StrictExit(info, request.Strict);
    }
}
