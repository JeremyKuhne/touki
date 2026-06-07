// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  A fully loaded trace: its metadata, the CPU provider's stack-sample source,
///  and the aggregator that ranks it.
/// </summary>
public sealed class LoadedTrace
{
    /// <summary>
    ///  Initializes a new <see cref="LoadedTrace"/>.
    /// </summary>
    public LoadedTrace(TraceInfo info, IReadOnlyList<SampleStack> samples)
    {
        Info = info;
        Source = new StackSampleSource(MetricInfo.Cpu, samples);
        Aggregator = new FoldingAggregator(Source);
    }

    /// <summary>
    ///  Metadata and quality signals describing the trace.
    /// </summary>
    public TraceInfo Info { get; }

    /// <summary>
    ///  The CPU provider's stack-sample source: the sampled stacks paired with the
    ///  CPU metric.
    /// </summary>
    public StackSampleSource Source { get; }

    /// <summary>
    ///  The folding aggregator over <see cref="Source"/>.
    /// </summary>
    public FoldingAggregator Aggregator { get; }
}
