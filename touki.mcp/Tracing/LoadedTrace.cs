// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace Touki.Mcp.Tracing;

/// <summary>
///  A fully loaded trace: its metadata, the normalized samples, and the
///  aggregator that ranks them.
/// </summary>
internal sealed class LoadedTrace
{
    /// <summary>
    ///  Initializes a new <see cref="LoadedTrace"/>.
    /// </summary>
    public LoadedTrace(TraceInfo info, IReadOnlyList<SampleStack> samples)
    {
        Info = info;
        Samples = samples;
        Aggregator = new FoldingAggregator(samples);
    }

    /// <summary>
    ///  Metadata and quality signals describing the trace.
    /// </summary>
    public TraceInfo Info { get; }

    /// <summary>
    ///  The normalized weighted samples.
    /// </summary>
    public IReadOnlyList<SampleStack> Samples { get; }

    /// <summary>
    ///  The folding aggregator over <see cref="Samples"/>.
    /// </summary>
    public FoldingAggregator Aggregator { get; }
}
