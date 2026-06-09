// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  Selects which provider's view of a trace the loader builds and the engine
///  ranks: the CPU sampler's stacks, the allocation sites, and so on.
/// </summary>
/// <remarks>
///  <para>
///   Each value names a stack-source family the loader can produce from a trace.
///   The engine that ranks, drills, and folds the result is provider-agnostic, so
///   the only thing that changes per family is which provider builds the
///   <see cref="StackSampleSource"/> and what its <see cref="MetricInfo"/> measures.
///  </para>
///  <para>
///   The families land one at a time as their providers are wired into the loader;
///   the values present here are the selectable ones.
///  </para>
/// </remarks>
public enum TraceMetric
{
    /// <summary>
    ///  The CPU sampler's stacks, weighted by sampled milliseconds
    ///  (<see cref="MetricInfo.Cpu"/>). Read from any supported trace format.
    /// </summary>
    Cpu,

    /// <summary>
    ///  Each thread's wall-clock timeline - running and blocked - weighted by
    ///  elapsed milliseconds (<see cref="MetricInfo.ThreadTime"/>). Read from an
    ///  <c>.etl</c> ETW capture carrying context-switch events.
    /// </summary>
    ThreadTime,

    /// <summary>
    ///  The allocation sites, weighted by bytes allocated
    ///  (<see cref="MetricInfo.Allocations"/>). Read from a <c>.nettrace</c>
    ///  EventPipe trace carrying <c>GCAllocationTick</c> events.
    /// </summary>
    Allocations,

    /// <summary>
    ///  The exception throw sites, weighted by one count per throw
    ///  (<see cref="MetricInfo.Exceptions"/>). Read from a <c>.nettrace</c>
    ///  EventPipe trace carrying <c>Exception/Start</c> events.
    /// </summary>
    Exceptions
}
