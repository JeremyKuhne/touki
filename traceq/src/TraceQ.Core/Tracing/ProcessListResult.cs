// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  One process in a trace's CPU-sample inventory: its label, how many samples it
///  owns, the weight those samples carry, and that weight's share of the whole
///  capture.
/// </summary>
/// <param name="Process">
///  The process label (<c>name(pid)</c> for a multi-process capture), or empty for a
///  single-process trace format.
/// </param>
/// <param name="SampleCount">The number of CPU samples attributed to the process.</param>
/// <param name="Weight">The summed sample weight, in the metric's unit (milliseconds for CPU time).</param>
/// <param name="PercentOfScope">The process's share of the whole capture's weight, in percent.</param>
public sealed record ProcessSummary(
    string Process,
    int SampleCount,
    double Weight,
    double PercentOfScope);

/// <summary>
///  A trace's process inventory: every process that owns CPU samples, ranked by
///  weight, so a multi-process ETW capture can be scoped to the right one before
///  ranking.
/// </summary>
/// <remarks>
///  <para>
///   This is the answer to "who is in this capture?" - the first move on a
///   machine-wide <c>.etl</c> before scoping a ranking with <c>--process</c>. The
///   automatic scope already narrows to the busiest process by sample count, but a
///   capture can hold several meaningful processes; this view lists them so the
///   choice is explicit. Single-process EventPipe and speedscope traces list one
///   process with an empty label.
///  </para>
/// </remarks>
/// <param name="TotalWeight">The whole capture's weight, in the metric's unit (the percent denominator).</param>
/// <param name="TotalSamples">The total number of CPU samples across every process.</param>
/// <param name="Processes">The processes, highest weight first.</param>
public sealed record ProcessListResult(
    double TotalWeight,
    int TotalSamples,
    IReadOnlyList<ProcessSummary> Processes);
