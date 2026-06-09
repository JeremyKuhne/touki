// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing.Providers;

namespace TraceQ.Cli;

/// <summary>
///  Runs an events query against the analysis core: page through the trace's raw
///  events by name, wrap the result in the output contract, and render it as text
///  or JSON.
/// </summary>
/// <remarks>
///  <para>
///   Unlike the GC and JIT reports the events query is paged by the provider
///   itself (<c>skip</c> / <c>take</c> with a per-event payload cap), so the
///   executor passes the page bounds straight through and adds a steering hint when
///   more matches remain beyond the current page.
///  </para>
///  <para>
///   The execution is independent of the command-line parser; it takes its inputs
///   as an <see cref="EventsRequest"/> and writes to the supplied writers, so it
///   can be driven directly in tests as well as from the verb handler in
///   <see cref="TraceCommands"/>.
///  </para>
/// </remarks>
internal static class EventsExecutor
{
    /// <summary>
    ///  Executes the events query.
    /// </summary>
    /// <param name="request">The validated events inputs.</param>
    /// <param name="output">The writer the result is rendered to.</param>
    /// <param name="error">The writer load errors are reported to.</param>
    /// <returns>A process exit code (see <see cref="ExitCodes"/>).</returns>
    public static int Run(EventsRequest request, TextWriter output, TextWriter error)
    {
        if (!TraceExecution.TryReadNetTraceReport(
            request.Path,
            "events",
            () => new EventQueryProvider().Query(
                request.Path,
                request.Name,
                request.Skip,
                request.Take,
                request.MaxPayload),
            error,
            out EventQueryResult? result))
        {
            return ExitCodes.InputError;
        }

        // When matches remain beyond this page, steer toward the next one rather than
        // leaving the agent to guess the skip arithmetic.
        List<string> hints = [];
        int shownThrough = result.Skipped + result.Events.Count;
        if (shownThrough < result.TotalMatched)
        {
            hints.Add(
                $"{result.TotalMatched - shownThrough} more match; page with --skip {shownThrough}.");
        }

        AnalysisResult<EventQueryResult> envelope = new(result, hints: hints);

        if (request.Format == OutputFormat.Json)
        {
            output.WriteLine(OutputJson.Serialize(envelope));
        }
        else
        {
            EventsTextRenderer.Render(envelope, request.Path, request.Name, output);
        }

        return ExitCodes.Success;
    }
}
