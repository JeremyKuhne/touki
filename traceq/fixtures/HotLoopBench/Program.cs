// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace TraceQ.Fixtures.HotLoopBench;

/// <summary>
///  A deliberately hot, stable string-building loop whose EventPipe CPU profile
///  is the seed of the traceq fixture corpus. The call tree is intentionally
///  shallow and named so its self / inclusive / callers rankings are easy to
///  reason about and to compare against the frozen oracle.
/// </summary>
/// <remarks>
///  <para>
///   The string concatenation in <see cref="BuildLabel"/> forces the runtime
///   helper frames (the buffer <c>Memmove</c> and the GC write barrier) that the
///   fold patterns exist to credit back to their real caller, so the captured
///   trace exercises the folding aggregator end to end.
///  </para>
/// </remarks>
[EventPipeProfiler(EventPipeProfile.CpuSampling)]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 1, iterationCount: 3, launchCount: 1)]
public class HotLoop
{
    /// <summary>
    ///  The benchmarked entry point: sums the lengths of many built labels.
    /// </summary>
    /// <returns>The accumulated label length, returned so the work is not elided.</returns>
    [Benchmark]
    public int StringWork() => SumLabelLengths(1500);

    private static int SumLabelLengths(int count)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += BuildLabel(i).Length;
        }

        return total;
    }

    private static string BuildLabel(int value)
    {
        string label = "";
        for (int segment = 0; segment < 24; segment++)
        {
            label += value.ToString() + "-";
        }

        return label;
    }
}

/// <summary>
///  An allocation-heavy loop captured under the GC-verbose EventPipe profile, so
///  its trace carries the <c>GCAllocationTick</c> and GC events the allocation and
///  GC-stats provider families read. The allocation sites are named so the
///  allocation ranking is easy to reason about.
/// </summary>
/// <remarks>
///  <para>
///   Captured with <see cref="RunStrategy.Monitoring"/> and a single invocation so
///   the workload runs exactly once - BenchmarkDotNet's default pilot stage
///   auto-scales the invocation count to fill an iteration, which inflates a
///   GC-verbose trace to tens of megabytes. One invocation that allocates a
///   bounded amount keeps the committed smoke trace small while still emitting a
///   few hundred <c>GCAllocationTick</c> events.
///  </para>
/// </remarks>
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 0, iterationCount: 1, invocationCount: 1)]
public class AllocLoop
{
    /// <summary>
    ///  The benchmarked entry point: allocates many short-lived buffers and labels.
    /// </summary>
    /// <returns>An accumulated value, returned so the work is not elided.</returns>
    [Benchmark]
    public long Allocate() => AllocateBuffers(60_000);

    private static long AllocateBuffers(int count)
    {
        long total = 0;
        for (int i = 0; i < count; i++)
        {
            total += RentByteBuffer(i) + RentLabel(i);
        }

        return total;
    }

    private static int RentByteBuffer(int seed)
    {
        byte[] buffer = new byte[256];
        buffer[seed % buffer.Length] = (byte)seed;
        return buffer[seed % buffer.Length];
    }

    private static int RentLabel(int seed)
    {
        string label = new('x', 64);
        return label.Length + seed % 7;
    }
}

/// <summary>
///  Runs the fixture benchmarks under the EventPipe profiler, or inspects a
///  captured trace's event types.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // `inspect <trace>` reports the distinct event types and their counts, so
        // make-fixtures can confirm a capture carries the events a provider needs.
        if (args.Length >= 2 && string.Equals(args[0], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            return Inspect(args[1]);
        }

        IEnumerable<Summary> summaries = BenchmarkSwitcher.FromAssembly(typeof(HotLoop).Assembly).Run(args);

        // Propagate a non-zero exit code when any benchmark failed to build/run or was
        // invalid, so make-fixtures' $LASTEXITCODE check fails fast on a bad capture.
        bool anyFailure = summaries.Any(s => s.HasCriticalValidationErrors || s.Reports.Any(r => !r.Success));
        return anyFailure ? 1 : 0;
    }

    private static int Inspect(string tracePath)
    {
        if (!File.Exists(tracePath))
        {
            Console.Error.WriteLine($"Trace not found: {tracePath}");
            return 1;
        }

        string etlxPath = TraceLog.CreateFromEventPipeDataFile(
            tracePath,
            null,
            new TraceLogOptions { ContinueOnError = true });

        using TraceLog traceLog = new(etlxPath);

        Dictionary<string, int> byType = new(StringComparer.Ordinal);
        int allocTicks = 0;
        int allocTicksWithStack = 0;
        foreach (TraceEvent data in traceLog.Events)
        {
            string name = $"{data.ProviderName}/{data.EventName}";
            byType.TryGetValue(name, out int count);
            byType[name] = count + 1;

            // Use the strongly-typed event so the count is robust to any event-name
            // formatting differences across parsers.
            if (data is GCAllocationTickTraceData)
            {
                allocTicks++;
                if (data.CallStack() is not null)
                {
                    allocTicksWithStack++;
                }
            }
        }

        Console.WriteLine($"Events: {traceLog.EventCount:N0} across {byType.Count} types");
        Console.WriteLine($"AllocationTick: {allocTicks:N0} total, {allocTicksWithStack:N0} with a call stack");
        foreach (KeyValuePair<string, int> pair in byType.OrderByDescending(static p => p.Value))
        {
            Console.WriteLine($"  {pair.Value,9:N0}  {pair.Key}");
        }

        return 0;
    }
}
