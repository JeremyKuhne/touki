// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Mcp;

/// <summary>
///  The <c>trace_info</c> payload: a loaded trace's identity and quality signals.
/// </summary>
/// <remarks>
///  <para>
///   The trace's quality warnings are not repeated here; they travel in the
///   <see cref="TraceQ.Output.AnalysisResult{T}.Warnings"/> channel of the
///   envelope this view is wrapped in, the same uniform channel every other tool
///   reports them through.
///  </para>
/// </remarks>
/// <param name="Path">The absolute path the trace was loaded from.</param>
/// <param name="Format">The on-disk format the trace was read from.</param>
/// <param name="TotalWeight">
///  Sum of the per-sample weights, in the metric's unit (CPU milliseconds, bytes
///  allocated, or one count per event).
/// </param>
/// <param name="SampleCount">Number of weighted samples in the normalized model.</param>
/// <param name="SymbolResolutionRate">
///  Fraction in <c>[0, 1]</c> of frames that resolved to a managed method name; below
///  <c>0.8</c> usually means symbols are missing and the rankings are unreliable.
/// </param>
/// <param name="Threads">Per-thread sample counts, highest first.</param>
public sealed record TraceInfoView(
    string Path,
    string Format,
    double TotalWeight,
    int SampleCount,
    double SymbolResolutionRate,
    IReadOnlyList<ThreadSampleInfo> Threads);
