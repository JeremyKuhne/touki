// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Tracing;
using Etlx = Microsoft.Diagnostics.Tracing.Etlx;

namespace TraceQ.Tracing.Providers;

/// <summary>
///  One event in an <see cref="EventQueryResult"/>.
/// </summary>
/// <param name="TimestampMs">The event time, in milliseconds relative to the start of the trace.</param>
/// <param name="Provider">The ETW / EventPipe provider that emitted the event.</param>
/// <param name="EventName">The event name.</param>
/// <param name="ThreadId">The OS thread the event was emitted on.</param>
/// <param name="Payload">The event's named fields rendered compactly, truncated to the query's payload cap.</param>
internal sealed record EventRecord(
    double TimestampMs,
    string Provider,
    string EventName,
    int ThreadId,
    string Payload);

/// <summary>
///  A page of events matching an <see cref="EventQueryProvider"/> query, plus the
///  total number matched so a consumer can page through them.
/// </summary>
/// <param name="TotalMatched">The total number of events matching the query across the whole trace.</param>
/// <param name="Skipped">The number of matches skipped before this page.</param>
/// <param name="Events">The events on this page, in trace (time) order.</param>
internal sealed record EventQueryResult(
    int TotalMatched,
    int Skipped,
    IReadOnlyList<EventRecord> Events);

/// <summary>
///  Queries the raw events of a trace by name, with pagination and payload
///  truncation, so an agent can inspect arbitrary events (the field guide's "Any
///  Stacks" / event view) without drowning in a machine-wide firehose.
/// </summary>
/// <remarks>
///  <para>
///   This is a structured query, not a stack source, so like the GC-stats
///   provider it returns its own result. Pagination (<c>skip</c> / <c>take</c>)
///   and a per-event payload cap keep the output inside an agent's budget even
///   when a query matches hundreds of thousands of events.
///  </para>
/// </remarks>
internal sealed class EventQueryProvider
{
    /// <summary>
    ///  The default maximum number of characters of an event's rendered payload.
    /// </summary>
    public const int DefaultMaxPayloadChars = 200;

    /// <summary>
    ///  Queries events whose <c>Provider/EventName</c> contains <paramref name="nameFilter"/>.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="nameFilter">
    ///  A case-insensitive substring matched against <c>Provider/EventName</c>; empty matches every event.
    /// </param>
    /// <param name="skip">The number of matches to skip (for paging). Must be non-negative.</param>
    /// <param name="take">The maximum number of matches to return. Must be non-negative.</param>
    /// <param name="maxPayloadChars">The per-event payload character cap. Must be non-negative.</param>
    /// <returns>The page of matching events, plus the total matched.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A paging or cap argument is negative.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public EventQueryResult Query(
        string path,
        string nameFilter = "",
        int skip = 0,
        int take = 100,
        int maxPayloadChars = DefaultMaxPayloadChars)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegative(take);
        ArgumentOutOfRangeException.ThrowIfNegative(maxPayloadChars);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Trace file not found: {fullPath}", fullPath);
        }

        string etlxPath = Etlx.TraceLog.CreateFromEventPipeDataFile(
            fullPath,
            null,
            new Etlx.TraceLogOptions { ContinueOnError = true });

        using Etlx.TraceLog traceLog = new(etlxPath);

        int matched = 0;
        List<EventRecord> page = [];
        foreach (TraceEvent data in traceLog.Events)
        {
            // Only build the qualified name when there is a filter to test it against;
            // an empty filter matches every event, so the allocation would be wasted.
            if (nameFilter.Length > 0
                && !$"{data.ProviderName}/{data.EventName}".Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Count every match for the total, but only materialize the requested page.
            if (matched >= skip && page.Count < take)
            {
                page.Add(new EventRecord(
                    data.TimeStampRelativeMSec,
                    data.ProviderName,
                    data.EventName,
                    data.ThreadID,
                    RenderPayload(data, maxPayloadChars)));
            }

            matched++;
        }

        // Report the number of matches actually skipped, which is fewer than the
        // requested skip when the query matched fewer events than that.
        return new EventQueryResult(matched, Math.Min(skip, matched), page);
    }

    // Renders an event's named fields as "name=value; ..." truncated to the cap, so
    // a single huge payload cannot blow the output budget.
    private static string RenderPayload(TraceEvent data, int maxPayloadChars)
    {
        if (maxPayloadChars == 0)
        {
            return "";
        }

        string[] names = data.PayloadNames;
        if (names.Length == 0)
        {
            return "";
        }

        // Append at most maxPayloadChars characters in total, so a single very large
        // payload value cannot grow the builder far past the cap before it is
        // truncated (the result is naturally already within the cap).
        System.Text.StringBuilder builder = new();
        for (int i = 0; i < names.Length; i++)
        {
            if (builder.Length >= maxPayloadChars)
            {
                break;
            }

            if (builder.Length > 0)
            {
                AppendCapped(builder, "; ", maxPayloadChars);
            }

            AppendCapped(builder, names[i], maxPayloadChars);
            AppendCapped(builder, "=", maxPayloadChars);

            // Skip materializing the (possibly very large) value when the name has
            // already filled the cap.
            if (builder.Length < maxPayloadChars)
            {
                AppendCapped(builder, data.PayloadString(i, null), maxPayloadChars);
            }
        }

        return builder.ToString();
    }

    // Appends at most (cap - builder.Length) characters of value, so the builder
    // never grows past the cap even when a single value is degenerately large.
    internal static void AppendCapped(System.Text.StringBuilder builder, string value, int cap)
    {
        int remaining = cap - builder.Length;
        if (remaining <= 0)
        {
            return;
        }

        builder.Append(value, 0, Math.Min(value.Length, remaining));
    }
}
