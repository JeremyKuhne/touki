// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing.Providers;
using TraceQ.Tracing.Readers;

namespace TraceQ.Tracing;

/// <summary>
///  Dispatches a trace file to the reader for its format and assembles the
///  <see cref="LoadedTrace"/>, computing the format-agnostic metadata (duration,
///  sample count, per-thread breakdown) from the normalized samples.
/// </summary>
public sealed class TraceLoader
{
    private readonly IReadOnlyList<ITraceReader> _readers =
    [
        new SpeedscopeReader(),
        new NetTraceReader(),
        new EtlReader()
    ];

    /// <summary>
    ///  Loads the CPU view of the trace at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="symbolsDirectory">
    ///  Optional build-output directory whose assemblies' embedded portable PDBs are
    ///  extracted to resolve managed frames to <c>file:line</c> for line-level
    ///  rankings. Ignored for speedscope inputs.
    /// </param>
    /// <param name="processScope">
    ///  Optional process-tree scope. When set, only samples belonging to the matched
    ///  workload process tree are loaded, narrowing a machine-wide capture to one
    ///  scenario losslessly. Ignored for single-process inputs (speedscope).
    /// </param>
    /// <returns>The loaded trace.</returns>
    /// <exception cref="ArgumentException">
    ///  <paramref name="path"/> is <see langword="null"/>, empty, or not a valid file path.
    /// </exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="NotSupportedException">No reader recognizes the file extension.</exception>
    public LoadedTrace Load(string path, string? symbolsDirectory = null, ProcessScope? processScope = null) =>
        Load(path, TraceMetric.Cpu, symbolsDirectory, processScope);

    /// <summary>
    ///  Loads the <paramref name="metric"/> view of the trace at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The trace file path.</param>
    /// <param name="metric">
    ///  Which provider's view to build: the CPU sampler's stacks, the allocation
    ///  sites, and so on. The engine ranks whichever is selected unchanged.
    /// </param>
    /// <param name="symbolsDirectory">
    ///  Optional build-output directory whose assemblies' embedded portable PDBs are
    ///  extracted to resolve managed frames to <c>file:line</c> for line-level
    ///  rankings. Ignored for speedscope inputs and for the metrics whose managed
    ///  frames resolve from the trace's own CLR rundown (allocation, exceptions).
    /// </param>
    /// <param name="processScope">
    ///  Optional process-tree scope. When set, only samples belonging to the matched
    ///  workload process tree are loaded, narrowing a machine-wide capture to one
    ///  scenario losslessly. Ignored for single-process inputs (speedscope) and for
    ///  the single-process EventPipe metrics (allocation, exceptions).
    /// </param>
    /// <returns>The loaded trace.</returns>
    /// <exception cref="ArgumentException">
    ///  <paramref name="path"/> is <see langword="null"/>, empty, or not a valid file path.
    /// </exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="NotSupportedException">
    ///  No reader recognizes the file extension, or the file's format does not carry
    ///  the data the requested <paramref name="metric"/> needs (for example the
    ///  allocation or exceptions metric against an <c>.etl</c>, or the thread-time
    ///  metric against a <c>.nettrace</c>).
    /// </exception>
    public LoadedTrace Load(
        string path,
        TraceMetric metric,
        string? symbolsDirectory = null,
        ProcessScope? processScope = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Trace file not found: {fullPath}", fullPath);
        }

        ITraceReader reader = ResolveReader(fullPath)
            ?? throw new NotSupportedException(
                $"Unrecognized trace format for '{fullPath}'. Supported: .speedscope.json, .nettrace, .etl");

        return metric switch
        {
            TraceMetric.Allocations => LoadAllocations(fullPath, reader),
            TraceMetric.Exceptions => LoadExceptions(fullPath, reader),
            TraceMetric.ThreadTime => LoadThreadTime(fullPath, reader, processScope),
            _ => LoadCpu(fullPath, reader, symbolsDirectory, processScope)
        };
    }

    private static LoadedTrace LoadCpu(
        string fullPath,
        ITraceReader reader,
        string? symbolsDirectory,
        ProcessScope? processScope)
    {
        TraceReadResult result = reader.Read(fullPath, symbolsDirectory, processScope);
        TraceInfo info = BuildInfo(
            fullPath,
            reader.Format,
            result.Samples,
            result.SymbolResolutionRate,
            result.Warnings);

        return new LoadedTrace(info, result.Samples);
    }

    private static LoadedTrace LoadAllocations(string fullPath, ITraceReader reader)
    {
        // The allocation metric is the GCAllocationTick view of an EventPipe trace; an
        // .etl or speedscope export carries no such events, so reject a wrong-format
        // input cleanly here rather than let the provider fail deep in the reader.
        if (reader.Format != TraceFormat.NetTrace)
        {
            throw new NotSupportedException(
                $"The allocation metric requires a .nettrace EventPipe trace; '{fullPath}' is {reader.Format}.");
        }

        StackSampleSource source = new AllocationProvider().Read(fullPath);

        List<string> warnings = [];
        if (source.Samples.Count == 0)
        {
            warnings.Add(
                "No allocation events were found. Was the trace captured with allocation sampling (GcVerbose)?");
        }

        // Managed allocation frames resolve from the CLR rundown embedded in the
        // trace, and this provider carries no separate symbol-reader signal, so
        // resolution is reported as complete: the low-resolution warning and the
        // --strict gate are CPU-reader concerns that do not apply to this family.
        TraceInfo info = BuildInfo(fullPath, TraceFormat.NetTrace, source.Samples, symbolResolutionRate: 1.0, warnings);
        return new LoadedTrace(info, source);
    }

    private static LoadedTrace LoadExceptions(string fullPath, ITraceReader reader)
    {
        // The exceptions metric is the Exception/Start view of an EventPipe trace; an
        // .etl or speedscope export carries no such events, so reject a wrong-format
        // input cleanly here rather than let the provider fail deep in the reader.
        if (reader.Format != TraceFormat.NetTrace)
        {
            throw new NotSupportedException(
                $"The exceptions metric requires a .nettrace EventPipe trace; '{fullPath}' is {reader.Format}.");
        }

        StackSampleSource source = new ExceptionsProvider().Read(fullPath);

        List<string> warnings = [];
        if (source.Samples.Count == 0)
        {
            warnings.Add("No exception-throw events were found. Did the workload throw any exceptions?");
        }

        // Throw-site frames resolve from the trace's CLR rundown, so - as with the
        // allocation family - resolution is reported complete and the --strict gate
        // does not apply.
        TraceInfo info = BuildInfo(fullPath, TraceFormat.NetTrace, source.Samples, symbolResolutionRate: 1.0, warnings);
        return new LoadedTrace(info, source);
    }

    private static LoadedTrace LoadThreadTime(string fullPath, ITraceReader reader, ProcessScope? processScope)
    {
        // Thread time is reconstructed from ETW context-switch events, which only an
        // .etl capture carries; an EventPipe .nettrace samples only running threads and
        // a speedscope export has no thread state, so reject either cleanly here.
        if (reader.Format != TraceFormat.Etl)
        {
            throw new NotSupportedException(
                $"The thread-time metric requires an .etl ETW capture; '{fullPath}' is {reader.Format}.");
        }

        StackSampleSource source = new ThreadTimeProvider().Read(fullPath, processScope);

        List<string> warnings = [];
        if (source.Samples.Count == 0)
        {
            warnings.Add(
                "No thread-time samples were found. Was the capture taken with the context-switch keywords?");
        }

        // The thread-time computer resolves frames from the ETW capture itself and
        // exposes no separate resolution signal, so - as with the other stack-source
        // families - resolution is reported complete and the --strict gate does not
        // apply. The total weight BuildInfo sums is elapsed milliseconds.
        TraceInfo info = BuildInfo(fullPath, TraceFormat.Etl, source.Samples, symbolResolutionRate: 1.0, warnings);
        return new LoadedTrace(info, source);
    }

    // Computes the format-agnostic metadata (total weight, sample count, per-thread
    // breakdown) shared by every provider from its normalized samples. The total is
    // the sum of the sample weights in the source metric's unit - milliseconds of CPU
    // time for the CPU provider, bytes for the allocation provider.
    private static TraceInfo BuildInfo(
        string fullPath,
        TraceFormat format,
        IReadOnlyList<SampleStack> samples,
        double symbolResolutionRate,
        IReadOnlyList<string> warnings)
    {
        double totalWeight = 0.0;
        Dictionary<string, int> threadCounts = new(StringComparer.Ordinal);
        foreach (SampleStack sample in samples)
        {
            totalWeight += sample.Weight;
            threadCounts.TryGetValue(sample.Thread, out int count);
            threadCounts[sample.Thread] = count + 1;
        }

        List<ThreadSampleInfo> threads = [];
        foreach (KeyValuePair<string, int> pair in threadCounts)
        {
            threads.Add(new ThreadSampleInfo(pair.Key, pair.Value));
        }

        threads.Sort(static (a, b) => b.SampleCount.CompareTo(a.SampleCount));

        return new TraceInfo(
            fullPath,
            format,
            totalWeight,
            samples.Count,
            symbolResolutionRate,
            threads,
            warnings);
    }

    private ITraceReader? ResolveReader(string path)
    {
        foreach (ITraceReader reader in _readers)
        {
            if (reader.CanRead(path))
            {
                return reader;
            }
        }

        return null;
    }
}
