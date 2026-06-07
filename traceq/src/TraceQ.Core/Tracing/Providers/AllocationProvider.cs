// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace TraceQ.Tracing.Providers;

/// <summary>
///  The allocation stack-source provider: reads the <c>GCAllocationTick</c>
///  events from a .NET EventPipe trace into stacks weighted by bytes allocated,
///  so the engine can rank allocation by call site exactly as it ranks CPU time.
/// </summary>
/// <remarks>
///  <para>
///   The runtime emits a <c>GCAllocationTick</c> roughly every 100 KB allocated,
///   carrying the allocating call stack and the byte amount since the previous
///   tick. Weighting each stack by that amount yields an allocation profile in
///   the same {stack, weight} shape as the CPU sampler, so the existing
///   <see cref="FoldingAggregator"/> ranks it without change - only the metric
///   (<see cref="MetricInfo.Allocations"/>, measured in bytes) differs.
///  </para>
///  <para>
///   This is a provider, not a format reader: it is a different view of the same
///   <c>.nettrace</c> the CPU reader consumes, so it does not implement
///   <c>ITraceReader</c> (which dispatches by file extension).
///  </para>
/// </remarks>
public sealed class AllocationProvider
{
    /// <summary>
    ///  Reads the allocation stack-sample source from the EventPipe trace at
    ///  <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The <c>.nettrace</c> file path.</param>
    /// <returns>The allocation source: byte-weighted allocation-site stacks.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public StackSampleSource Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Trace file not found: {fullPath}", fullPath);
        }

        string etlxPath = TraceLog.CreateFromEventPipeDataFile(
            fullPath,
            null,
            new TraceLogOptions { ContinueOnError = true });

        using TraceLog traceLog = new(etlxPath);

        List<SampleStack> samples = [];
        List<string> leafToRoot = [];

        foreach (TraceEvent data in traceLog.Events)
        {
            if (data is not GCAllocationTickTraceData alloc)
            {
                continue;
            }

            long bytes = alloc.AllocationAmount64;
            if (bytes <= 0)
            {
                continue;
            }

            TraceCallStack? callStack = data.CallStack();
            if (callStack is null)
            {
                continue;
            }

            leafToRoot.Clear();
            for (TraceCallStack? frame = callStack; frame is not null; frame = frame.Caller)
            {
                leafToRoot.Add(QualifyFrame(frame.CodeAddress));
            }

            if (leafToRoot.Count == 0)
            {
                continue;
            }

            int count = leafToRoot.Count;
            string[] frames = new string[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = leafToRoot[count - 1 - i];
            }

            samples.Add(new SampleStack(frames, bytes, data.ThreadID.ToString()));
        }

        return new StackSampleSource(MetricInfo.Allocations, samples);
    }

    // Builds the "module!Method(sig)" frame name the aggregator and FrameNames.Short
    // expect, matching how the CPU reader names frames so folding stays consistent.
    private static string QualifyFrame(TraceCodeAddress address)
    {
        string method = address.FullMethodName;
        string module = address.ModuleName;
        if (string.IsNullOrEmpty(method))
        {
            return $"{(string.IsNullOrEmpty(module) ? "?" : module)}!?";
        }

        return string.IsNullOrEmpty(module) ? method : $"{module}!{method}";
    }
}
