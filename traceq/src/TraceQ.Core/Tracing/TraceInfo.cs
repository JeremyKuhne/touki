// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  Metadata and quality signals describing a loaded trace, returned up front so
///  a caller can pick its next query from real signals rather than empty results.
/// </summary>
public sealed class TraceInfo
{
    /// <summary>
    ///  Initializes a new <see cref="TraceInfo"/>.
    /// </summary>
    public TraceInfo(
        string path,
        TraceFormat format,
        double durationMs,
        int sampleCount,
        double symbolResolutionRate,
        IReadOnlyList<ThreadSampleInfo> threads,
        IReadOnlyList<string> warnings)
    {
        Path = path;
        Format = format;
        DurationMs = durationMs;
        SampleCount = sampleCount;
        SymbolResolutionRate = symbolResolutionRate;
        Threads = threads;
        Warnings = warnings;
    }

    /// <summary>
    ///  The absolute path the trace was loaded from.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///  The on-disk format the trace was read from.
    /// </summary>
    public TraceFormat Format { get; }

    /// <summary>
    ///  Sum of the per-sample weights across all samples, in the source metric's
    ///  unit - milliseconds of CPU time for a CPU trace, bytes for an allocation
    ///  trace. For CPU this is busy time, not wall-clock: because every thread's
    ///  samples are included, the value can exceed the trace's wall-clock span when
    ///  multiple threads ran concurrently.
    /// </summary>
    public double DurationMs { get; }

    /// <summary>
    ///  Number of weighted samples in the normalized model.
    /// </summary>
    public int SampleCount { get; }

    /// <summary>
    ///  Fraction in <c>[0, 1]</c> of stack frames whose symbol resolved to a
    ///  managed method name. A value below <c>0.8</c> usually means symbols are
    ///  missing and the rankings should not be trusted.
    /// </summary>
    public double SymbolResolutionRate { get; }

    /// <summary>
    ///  Per-thread sample counts, useful for picking a root frame or spotting
    ///  idle thread-pool noise.
    /// </summary>
    public IReadOnlyList<ThreadSampleInfo> Threads { get; }

    /// <summary>
    ///  Human-readable quality warnings (low symbol resolution, no samples, etc.).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }
}

/// <summary>
///  Per-thread sample count within a loaded trace.
/// </summary>
public sealed class ThreadSampleInfo
{
    /// <summary>
    ///  Initializes a new <see cref="ThreadSampleInfo"/>.
    /// </summary>
    public ThreadSampleInfo(string thread, int sampleCount)
    {
        Thread = thread;
        SampleCount = sampleCount;
    }

    /// <summary>
    ///  A label identifying the thread (OS thread id, or a synthetic id for
    ///  speedscope profiles).
    /// </summary>
    public string Thread { get; }

    /// <summary>
    ///  Number of samples attributed to the thread.
    /// </summary>
    public int SampleCount { get; }
}
