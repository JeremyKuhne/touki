// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;

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
///  Runs the hot-loop benchmark under the EventPipe profiler to produce the
///  fixture trace.
/// </summary>
internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(HotLoop).Assembly).Run(args);
}
