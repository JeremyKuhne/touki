// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Output;
using TraceQ.Tracing.Providers;

namespace TraceQ.Cli;

/// <summary>
///  Renders an events-query result as the dense, fixed-width text view a human
///  reads at the terminal: a header naming the filter and page, the matched events
///  in aligned columns, then any steering hint toward the next page.
/// </summary>
/// <remarks>
///  <para>
///   This is the text half of the events query; the JSON half is
///   <see cref="OutputJson"/>. Both render the same <see cref="AnalysisResult{T}"/>
///   envelope.
///  </para>
/// </remarks>
internal static class EventsTextRenderer
{
    private const int QualifiedNameWidth = 44;

    /// <summary>
    ///  Renders the events envelope to <paramref name="output"/>.
    /// </summary>
    /// <param name="envelope">The events page, with its steering hints.</param>
    /// <param name="path">The trace path, for the header line.</param>
    /// <param name="name">The name filter, for the header line.</param>
    /// <param name="output">The writer the text is rendered to.</param>
    public static void Render(AnalysisResult<EventQueryResult> envelope, string path, string name, TextWriter output)
    {
        EventQueryResult result = envelope.Result;
        string filter = name.Length > 0 ? $"filter '{name}'" : "all events";

        output.WriteLine($"events  -  {path}  ({filter})");
        output.WriteLine();

        if (result.Events.Count == 0)
        {
            output.WriteLine(
                result.TotalMatched == 0
                    ? "  (no events matched)"
                    : $"  (no events on this page; {result.TotalMatched} matched - lower --skip)");
            RenderHints(envelope, output);
            return;
        }

        int from = result.Skipped + 1;
        int through = result.Skipped + result.Events.Count;
        output.WriteLine($"  {result.TotalMatched} matched   showing {from}-{through}");
        output.WriteLine();

        output.WriteLine($"  {"time(ms)",12}  {"thread",6}  {"provider / event",-QualifiedNameWidth}  payload");
        foreach (EventRecord e in result.Events)
        {
            string qualified = $"{e.Provider}/{e.EventName}";
            output.WriteLine(
                $"  {e.TimestampMs,12:N2}  {e.ThreadId,6}  {qualified,-QualifiedNameWidth}  {e.Payload}");
        }

        RenderHints(envelope, output);
    }

    private static void RenderHints(AnalysisResult<EventQueryResult> envelope, TextWriter output)
    {
        foreach (string hint in envelope.Hints)
        {
            output.WriteLine($"> {hint}");
        }
    }
}
