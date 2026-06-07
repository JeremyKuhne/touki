// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing.Providers;

/// <summary>
///  One garbage collection's structured record in a <see cref="GcStatsResult"/>.
/// </summary>
/// <param name="Number">The collection's sequence number within the trace.</param>
/// <param name="Generation">The generation condemned (0, 1, or 2).</param>
/// <param name="Kind">The collection kind (for example <c>Blocking</c> or <c>Background</c>).</param>
/// <param name="Reason">Why the collection was triggered.</param>
/// <param name="PauseMs">How long the managed threads were paused, in milliseconds.</param>
/// <param name="HeapSizeAfterMB">The managed heap size after the collection, in megabytes.</param>
/// <param name="PromotedMB">Memory promoted to an older generation by the collection, in megabytes.</param>
internal sealed record GcRecord(
    int Number,
    int Generation,
    string Kind,
    string Reason,
    double PauseMs,
    double HeapSizeAfterMB,
    double PromotedMB);

/// <summary>
///  The garbage-collection report for a trace: the per-collection records plus
///  the aggregate counts and pause-time summary an agent reads to judge GC
///  pressure.
/// </summary>
/// <remarks>
///  <para>
///   Unlike the stack-source families (CPU, allocation), GC behavior is a series
///   of structured per-collection records rather than weighted call stacks, so it
///   does not flow through the folding aggregator; this is its own result shape.
///  </para>
/// </remarks>
/// <param name="GcCount">The total number of collections.</param>
/// <param name="Gen0Count">The number of generation-0 collections.</param>
/// <param name="Gen1Count">The number of generation-1 collections.</param>
/// <param name="Gen2Count">The number of generation-2 collections.</param>
/// <param name="TotalPauseMs">The summed pause time across all collections, in milliseconds.</param>
/// <param name="MaxPauseMs">The longest single pause, in milliseconds.</param>
/// <param name="MeanPauseMs">The mean pause time, in milliseconds.</param>
/// <param name="PeakHeapSizeMB">The largest post-collection heap size observed, in megabytes.</param>
/// <param name="TotalPromotedMB">The summed promoted bytes across all collections, in megabytes.</param>
/// <param name="Gcs">The per-collection records, in trace order.</param>
internal sealed record GcStatsResult(
    int GcCount,
    int Gen0Count,
    int Gen1Count,
    int Gen2Count,
    double TotalPauseMs,
    double MaxPauseMs,
    double MeanPauseMs,
    double PeakHeapSizeMB,
    double TotalPromotedMB,
    IReadOnlyList<GcRecord> Gcs);
