// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

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
    ///  Loads the trace at <paramref name="path"/>.
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
    public LoadedTrace Load(string path, string? symbolsDirectory = null, ProcessScope? processScope = null)
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

        TraceReadResult result = reader.Read(fullPath, symbolsDirectory, processScope);

        double durationMs = 0.0;
        Dictionary<string, int> threadCounts = new(StringComparer.Ordinal);
        foreach (SampleStack sample in result.Samples)
        {
            durationMs += sample.Weight;
            threadCounts.TryGetValue(sample.Thread, out int count);
            threadCounts[sample.Thread] = count + 1;
        }

        List<ThreadSampleInfo> threads = [];
        foreach (KeyValuePair<string, int> pair in threadCounts)
        {
            threads.Add(new ThreadSampleInfo(pair.Key, pair.Value));
        }

        threads.Sort(static (a, b) => b.SampleCount.CompareTo(a.SampleCount));

        TraceInfo info = new(
            fullPath,
            reader.Format,
            durationMs,
            result.Samples.Count,
            result.SymbolResolutionRate,
            threads,
            result.Warnings);

        return new LoadedTrace(info, result.Samples);
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
