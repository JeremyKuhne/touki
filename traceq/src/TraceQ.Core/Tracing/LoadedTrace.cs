// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  A fully loaded trace: its metadata, the selected provider's stack-sample
///  source, and the aggregator that ranks it.
/// </summary>
public sealed class LoadedTrace
{
    /// <summary>
    ///  Initializes a new <see cref="LoadedTrace"/> over the CPU provider's stacks.
    /// </summary>
    /// <param name="info">Metadata and quality signals describing the trace.</param>
    /// <param name="samples">The CPU sampler's weighted stacks.</param>
    public LoadedTrace(TraceInfo info, IReadOnlyList<SampleStack> samples)
        : this(info, new StackSampleSource(MetricInfo.Cpu, samples))
    {
    }

    /// <summary>
    ///  Initializes a new <see cref="LoadedTrace"/> over any provider's stack-sample
    ///  source.
    /// </summary>
    /// <param name="info">Metadata and quality signals describing the trace.</param>
    /// <param name="source">
    ///  The provider's stacks paired with the metric they are weighted by. The
    ///  aggregator and every engine verb run on whatever source they are handed, so
    ///  this is the seam by which a non-CPU family (allocation, ...) is ranked by
    ///  the same engine.
    /// </param>
    public LoadedTrace(TraceInfo info, StackSampleSource source)
    {
        Info = info;
        Source = source;
        Aggregator = new FoldingAggregator(source);
    }

    /// <summary>
    ///  Metadata and quality signals describing the trace.
    /// </summary>
    public TraceInfo Info { get; }

    /// <summary>
    ///  The selected provider's stack-sample source: the stacks paired with the
    ///  metric they are weighted by.
    /// </summary>
    public StackSampleSource Source { get; }

    /// <summary>
    ///  The folding aggregator over <see cref="Source"/>.
    /// </summary>
    public FoldingAggregator Aggregator { get; }
}
