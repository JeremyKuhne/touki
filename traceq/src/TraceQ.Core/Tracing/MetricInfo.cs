// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  Describes the metric a <see cref="StackSampleSource"/> carries: its display
///  name and the unit its sample weights are measured in.
/// </summary>
/// <remarks>
///  <para>
///   Each investigation family weights its stacks by a different metric - CPU
///   time in milliseconds today, allocation bytes or event counts as later
///   providers land. Threading the metric through the source lets the engine and
///   its renderers stay provider-agnostic instead of assuming milliseconds.
///  </para>
/// </remarks>
/// <param name="Name">The metric's display name (for example <c>CPU</c>).</param>
/// <param name="Unit">The unit the sample weights are measured in (for example <c>ms</c>).</param>
internal sealed record MetricInfo(string Name, string Unit)
{
    /// <summary>
    ///  The CPU-time metric: wall-clock milliseconds per sampled call stack. This
    ///  is the metric of the CPU provider.
    /// </summary>
    public static MetricInfo Cpu { get; } = new("CPU", "ms");

    /// <summary>
    ///  The thread-time metric: wall-clock milliseconds per stack, including the
    ///  time a thread spent blocked (not running). This is the metric of the
    ///  thread-time provider, which - unlike CPU sampling - accounts for off-CPU
    ///  time, so a stack's weight reflects elapsed time rather than busy time.
    /// </summary>
    public static MetricInfo ThreadTime { get; } = new("ThreadTime", "ms");

    /// <summary>
    ///  The allocation metric: bytes allocated per <c>GCAllocationTick</c> call
    ///  stack. This is the metric of the allocation provider.
    /// </summary>
    public static MetricInfo Allocations { get; } = new("Allocations", "bytes");
}
