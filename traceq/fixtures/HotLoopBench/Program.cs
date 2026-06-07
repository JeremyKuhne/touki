// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Diagnostics.Windows;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

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
///  A loop whose body calls a spread of distinctly named methods, captured under
///  the JIT EventPipe profile so its trace carries the <c>MethodJittingStarted</c>
///  / <c>MethodLoadVerbose</c> events the JIT-stats provider reads.
/// </summary>
/// <remarks>
///  <para>
///   Each method is jitted exactly once - on its first call - regardless of how
///   many times the loop runs, so a single <see cref="RunStrategy.Monitoring"/>
///   invocation captures the complete JIT picture while keeping the committed
///   trace tiny. The methods are named <c>JitMethodNN</c> so the per-method
///   compile rows are easy to recognise in the fixture.
///  </para>
/// </remarks>
[EventPipeProfiler(EventPipeProfile.Jit)]
[SimpleJob(RunStrategy.Monitoring, launchCount: 1, warmupCount: 0, iterationCount: 1, invocationCount: 1)]
public class JitLoop
{
    /// <summary>
    ///  The benchmarked entry point: calls a spread of distinct methods so each is
    ///  jitted once.
    /// </summary>
    /// <returns>An accumulated value, returned so the work is not elided.</returns>
    [Benchmark]
    public long Compile() => JitMethod00(1) + JitMethod01(2) + JitMethod02(3) + JitMethod03(4)
        + JitMethod04(5) + JitMethod05(6) + JitMethod06(7) + JitMethod07(8);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod00(int seed) => (long)Math.Sqrt(seed) + seed;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod01(int seed) => (long)Math.Sqrt(seed) * 3 + seed;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod02(int seed) => (seed * seed) + JitMethod00(seed);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod03(int seed) => (seed << 2) - JitMethod01(seed);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod04(int seed) => (long)Math.Log(seed + 1) + JitMethod02(seed);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod05(int seed) => (seed % 5) + JitMethod03(seed);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod06(int seed) => (seed * 7) + JitMethod04(seed);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static long JitMethod07(int seed) => (seed ^ 0x5A) + JitMethod05(seed) + JitMethod06(seed);
}

/// <summary>
///  The BenchmarkDotNet configuration for the ETW (<c>.etl</c>) capture: a
///  net481 job profiled by <see cref="EtwProfiler"/> with the kernel keywords the
///  ThreadTime view needs.
/// </summary>
/// <remarks>
///  <para>
///   The <c>[EtwProfiler]</c> attribute cannot set kernel keywords, and its
///   default capture is only <c>Profile | ImageLoad</c> (CPU samples). To make a
///   single capture serve both the CPU rankings and the wall-clock / blocked-time
///   ThreadTime view, this adds the <c>Thread</c>, <c>ContextSwitch</c>, and
///   <c>Dispatcher</c> kernel keywords so the trace carries context-switch
///   events, with stacks on the CPU-sample and context-switch events.
///  </para>
///  <para>
///   The job targets net481 (the .NET Framework half of the corpus that the ETW
///   reader exists to serve) and runs a single <see cref="RunStrategy.Monitoring"/>
///   invocation, keeping the committed <c>.etl</c> small. ETW capture is
///   Windows-only and needs an elevated session, so this runs from
///   <c>make-fixtures.ps1</c> on an administrator terminal, never in CI.
///  </para>
/// </remarks>
internal sealed class EtwCaptureConfig : ManualConfig
{
    public EtwCaptureConfig()
    {
        AddJob(Job.Default
            .WithRuntime(ClrRuntime.Net481)
            .WithStrategy(RunStrategy.Monitoring)
            .WithLaunchCount(1)
            .WithWarmupCount(0)
            .WithIterationCount(1)
            .WithInvocationCount(1));

        // CPU samples and image loads for managed stacks, plus thread / context-switch
        // / dispatcher events so the capture carries the blocked-time data ThreadTime reads.
        KernelTraceEventParser.Keywords kernelKeywords =
            KernelTraceEventParser.Keywords.Profile
            | KernelTraceEventParser.Keywords.ImageLoad
            | KernelTraceEventParser.Keywords.Thread
            | KernelTraceEventParser.Keywords.ContextSwitch
            | KernelTraceEventParser.Keywords.Dispatcher;

        // Attach stacks to the CPU-sample and context-switch events; those two carry
        // the call stacks the rankings and the thread-time attribution need.
        KernelTraceEventParser.Keywords stackKeywords =
            KernelTraceEventParser.Keywords.Profile
            | KernelTraceEventParser.Keywords.ContextSwitch;

        AddDiagnoser(new EtwProfiler(new EtwProfilerConfig(
            performExtraBenchmarksRun: false,
            kernelKeywords: kernelKeywords,
            kernelStackKeywords: stackKeywords)));
    }
}

/// <summary>
///  A mixed CPU-and-blocking loop captured under the ETW profiler on net481, so
///  its <c>.etl</c> carries managed CPU-sample stacks (for the ETW reader and the
///  cross-machine ETLX spike) and context-switch events (for the ThreadTime view).
/// </summary>
/// <remarks>
///  <para>
///   The body interleaves the same hot string building the EventPipe fixtures use
///   with brief blocking sleeps, so the capture has both on-CPU time to rank and
///   off-CPU (blocked) time for ThreadTime to attribute. It is the net481 half of
///   the corpus; its rankings are not compared against the frozen EventPipe oracle
///   (a different runtime and capture), only used to exercise the ETW read path.
///  </para>
/// </remarks>
[Config(typeof(EtwCaptureConfig))]
public class EtwLoop
{
    /// <summary>
    ///  The benchmarked entry point: sums the lengths of many built labels with
    ///  brief blocking interludes.
    /// </summary>
    /// <returns>The accumulated label length, returned so the work is not elided.</returns>
    [Benchmark]
    public long Run() => MixedWorkload(600);

    private static long MixedWorkload(int count)
    {
        long total = 0;
        for (int i = 0; i < count; i++)
        {
            total += BuildLabel(i).Length;

            // A brief block every so often, so the capture carries off-CPU intervals
            // for the ThreadTime view to attribute (CPU-only work has none to show).
            if (i % 100 == 0)
            {
                System.Threading.Thread.Sleep(1);
            }
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

        // `convert <etl> <outEtlx>` converts an ETW trace to ETLX, the cross-machine
        // hand-off format the O1 spike commits and reads off Windows.
        if (args.Length >= 3 && string.Equals(args[0], "convert", StringComparison.OrdinalIgnoreCase))
        {
            return Convert(args[1], args[2]);
        }

        // `trim <inEtl> <outEtl> <processNameSubstring> [--no-children]` relogs an ETW
        // trace keeping only the events belonging to the matching process tree (the
        // matched process plus all its descendants, unless --no-children is given), so
        // a machine-wide capture shrinks to a committable per-scenario smoke.
        if (args.Length >= 4 && string.Equals(args[0], "trim", StringComparison.OrdinalIgnoreCase))
        {
            bool includeChildren = !args.Contains("--no-children", StringComparer.OrdinalIgnoreCase);
            return Trim(args[1], args[2], args[3], includeChildren);
        }

        IEnumerable<Summary> summaries = BenchmarkSwitcher.FromAssembly(typeof(HotLoop).Assembly).Run(args);

        // Propagate a non-zero exit code when any benchmark failed to build/run or was
        // invalid, so make-fixtures' $LASTEXITCODE check fails fast on a bad capture.
        bool anyFailure = summaries.Any(s => s.HasCriticalValidationErrors || s.Reports.Any(r => !r.Success));
        return anyFailure ? 1 : 0;
    }

    // Relogs an ETW trace keeping only the events of a process tree - the process(es)
    // whose name contains the given substring plus, by default, all their descendants
    // - so a machine-wide capture shrinks to a small per-scenario file. Following
    // children is essential for BenchmarkDotNet, which runs each workload in a child
    // process the orchestrating host launches; it is equally the right default for
    // "profile my app", whose work often runs in children too. Runs unelevated:
    // relogging an existing file is not a live session.
    //
    // KNOWN LIMITATION: the relogged file resolves native modules but NOT JITted
    // managed methods, even with the full CLR method/module rundown preserved - the
    // raw ETWRelogger's event re-injection does not rebuild the managed-method address
    // map that TraceLog's full conversion does. So this `trim` is currently a
    // native-only file shrink, not a substitute for analysing the full trace. The
    // lossless path is to scope by process tree at analysis time over the full trace
    // (see the EtlReader). The full state of this investigation, and why physical
    // trimming is still wanted (avoiding repeated filtering, transporting a smaller
    // trace), is captured in docs/traceq-etl-trimming.md.
    private static int Trim(string inEtlPath, string outEtlPath, string processNameSubstring, bool includeChildren)
    {
        if (!File.Exists(inEtlPath))
        {
            Console.Error.WriteLine($"Trace not found: {inEtlPath}");
            return 1;
        }

        // Resolve the scenario to the set of process IDs to keep, and their thread IDs,
        // with a TraceLog pass (which reuses the sibling .etlx conversion cache). The
        // raw relogger cannot resolve process names or the parent / child tree itself,
        // so the keep-set is computed here and matched by ID during the relog.
        HashSet<int> keepPids = [];
        HashSet<int> keepThreadIds = [];
        List<string> keptNames = [];
        using (TraceLog traceLog = OpenAnyTrace(inEtlPath))
        {
            // The roots are every process whose name contains the substring. For
            // BenchmarkDotNet both the host ("HotLoopBench") and each job child
            // ("HotLoopBench-Job-...") match, but the descendant walk below also picks
            // up children with unrelated names (the "profile my app" case).
            HashSet<int> roots = [];
            foreach (TraceProcess process in traceLog.Processes)
            {
                if (process.Name is not null
                    && process.Name.IndexOf(processNameSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    roots.Add(process.ProcessID);
                }
            }

            foreach (TraceProcess process in traceLog.Processes)
            {
                bool keep = roots.Contains(process.ProcessID);

                // Keep a process when it descends from a root: walk its parent chain and
                // see whether any ancestor is a root (or is itself kept). The chain is
                // shallow (host -> job), so this is cheap.
                if (!keep && includeChildren)
                {
                    for (TraceProcess? ancestor = process.Parent; ancestor is not null; ancestor = ancestor.Parent)
                    {
                        if (roots.Contains(ancestor.ProcessID))
                        {
                            keep = true;
                            break;
                        }
                    }
                }

                if (keep)
                {
                    keepPids.Add(process.ProcessID);
                    keptNames.Add($"{process.Name}({process.ProcessID})");
                    foreach (TraceThread thread in process.Threads)
                    {
                        keepThreadIds.Add(thread.ThreadID);
                    }
                }
            }
        }

        if (keepPids.Count == 0)
        {
            Console.Error.WriteLine($"No process matching '{processNameSubstring}' found in {inEtlPath}.");
            return 1;
        }

        int kept = 0;
        int total = 0;
        using (ETWReloggerTraceEventSource relogger = new(inEtlPath, outEtlPath))
        {
            // Register the kernel parser so the CPU-sample, stack, and context-switch
            // events arrive typed (the raw relogger otherwise passes them through
            // unparsed, with no resolvable thread). CPU-sample and context-switch
            // events are attributed by thread, so keep the ones belonging to the target
            // process's threads, and record each kept event's timestamp so its stack
            // can be matched to it.
            KernelTraceEventParser kernel = new(relogger);
            HashSet<double> keptEventTimestamps = [];

            kernel.PerfInfoSample += data =>
            {
                if (keepThreadIds.Contains(data.ThreadID))
                {
                    relogger.WriteEvent(data);
                    keptEventTimestamps.Add(data.TimeStampRelativeMSec);
                    kept++;
                }
            };

            kernel.ThreadCSwitch += data =>
            {
                if (keepThreadIds.Contains(data.NewThreadID) || keepThreadIds.Contains(data.OldThreadID))
                {
                    relogger.WriteEvent(data);
                    keptEventTimestamps.Add(data.TimeStampRelativeMSec);
                    kept++;
                }
            };

            // A kernel stack-walk event does not carry a reliable owning thread (its
            // header thread is often the logging CPU's, and the payload thread is not
            // public), so match it to its target event by timestamp instead - exactly
            // how TraceLog folds stacks onto events. Both timestamps convert the same
            // QPC tick through the same function, so they compare bit-identically. The
            // stack always follows its target in the stream, so the target's timestamp
            // is already recorded by the time the stack arrives.
            kernel.StackWalkStack += data =>
            {
                if (keptEventTimestamps.Contains(data.EventTimeStampRelativeMSec))
                {
                    relogger.WriteEvent(data);
                    kept++;
                }
            };

            // Deep / repeated stacks (the managed hot path) are emitted compressed: the
            // sample carries a StackWalkStackKey* reference to a StackKey, and the frames
            // live in a separate StackWalkKeyRundown definition at session end. Keep the
            // references for our kept events (by timestamp) and record the keys they
            // point at, then keep the matching definitions below - otherwise the managed
            // stacks resolve to nothing.
            HashSet<ulong> referencedStackKeys = [];

            void KeepStackKeyRef(StackWalkRefTraceData data)
            {
                if (keptEventTimestamps.Contains(data.EventTimeStampRelativeMSec))
                {
                    relogger.WriteEvent(data);
                    referencedStackKeys.Add((ulong)data.StackKey);
                    kept++;
                }
            }

            kernel.StackWalkStackKeyKernel += KeepStackKeyRef;
            kernel.StackWalkStackKeyUser += KeepStackKeyRef;

            kernel.StackWalkKeyRundown += data =>
            {
                if (referencedStackKeys.Contains((ulong)data.StackKey))
                {
                    relogger.WriteEvent(data);
                    kept++;
                }
            };

            relogger.AllEvents += data =>
            {
                total++;

                // The kernel CPU-sample / stack / context-switch events are written by
                // the typed handlers above; skip exactly those event types here so they
                // are not written twice. Skipping by event type (rather than by the
                // kernel provider GUID) is essential: the Windows kernel provider uses
                // several GUIDs, so a GUID check would wrongly drop the kernel image
                // events that map instruction pointers to modules. Everything else - the
                // process's image, thread, and CLR-method records - is kept when it
                // belongs to the process.
                if (data is SampledProfileTraceData
                    or CSwitchTraceData
                    or StackWalkStackTraceData
                    or StackWalkRefTraceData
                    or StackWalkDefTraceData)
                {
                    return;
                }

                if (keepPids.Contains(data.ProcessID))
                {
                    relogger.WriteEvent(data);
                    kept++;
                }
            };

            relogger.Process();
        }

        long bytes = new FileInfo(outEtlPath).Length;
        Console.WriteLine(
            $"Trimmed {inEtlPath} -> {outEtlPath}: kept {keepPids.Count} process(es) [{string.Join(", ", keptNames)}], "
            + $"{keepThreadIds.Count} threads, {kept:N0} of {total:N0} events ({bytes / 1024:N0} KB)");
        return 0;
    }

    // Opens a trace of either format as a TraceLog: ETW (.etl) via OpenOrConvert,
    // EventPipe (.nettrace) via CreateFromEventPipeDataFile.
    private static TraceLog OpenAnyTrace(string tracePath)
    {
        if (tracePath.EndsWith(".etl", StringComparison.OrdinalIgnoreCase))
        {
            return TraceLog.OpenOrConvert(tracePath, new TraceLogOptions { ContinueOnError = true });
        }

        string etlxPath = TraceLog.CreateFromEventPipeDataFile(
            tracePath,
            null,
            new TraceLogOptions { ContinueOnError = true });

        return new TraceLog(etlxPath);
    }

    private static int Convert(string etlPath, string outEtlxPath)
    {
        if (!File.Exists(etlPath))
        {
            Console.Error.WriteLine($"Trace not found: {etlPath}");
            return 1;
        }

        // CreateFromEventTraceLogFile writes the ETLX next to the ETL by default;
        // direct it to the requested output path so make-fixtures controls the name.
        string etlxPath = TraceLog.CreateFromEventTraceLogFile(
            etlPath,
            outEtlxPath,
            new TraceLogOptions { ContinueOnError = true });

        Console.WriteLine($"Converted {etlPath} -> {etlxPath}");
        return 0;
    }

    private static int Inspect(string tracePath)
    {
        if (!File.Exists(tracePath))
        {
            Console.Error.WriteLine($"Trace not found: {tracePath}");
            return 1;
        }

        using TraceLog traceLog = OpenAnyTrace(tracePath);

        Dictionary<string, int> byType = new(StringComparer.Ordinal);
        int allocTicks = 0;
        int allocTicksWithStack = 0;
        int cpuSamples = 0;
        int cpuSamplesWithStack = 0;
        int cpuSamplesWithResolvedFrame = 0;
        int benchmarkJitMethods = 0;
        string[]? firstResolvedStack = null;
        string[]? firstManagedStack = null;
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

            // Report CPU-sample stack resolution: how many samples carry a folded call
            // stack, how many resolve to at least one named frame, and the first stack
            // that reaches a managed benchmark frame.
            if (data is SampledProfileTraceData)
            {
                cpuSamples++;
                TraceCallStack? stack = data.CallStack();
                if (stack is not null)
                {
                    cpuSamplesWithStack++;
                    string[] frames = DescribeStack(stack);
                    if (Array.Exists(frames, static f => f.Length > 1 && !f.StartsWith("?!", StringComparison.Ordinal)))
                    {
                        cpuSamplesWithResolvedFrame++;
                        firstResolvedStack ??= frames;
                    }

                    if (firstManagedStack is null
                        && Array.Exists(frames, static f =>
                            f.IndexOf("EtwLoop", StringComparison.OrdinalIgnoreCase) >= 0
                            || f.IndexOf("BuildLabel", StringComparison.OrdinalIgnoreCase) >= 0
                            || f.IndexOf("MixedWorkload", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        firstManagedStack = frames;
                    }
                }
            }

            // Count the JIT rundown for the benchmark's own methods: these map the
            // managed instruction pointers to method names, so their survival is what
            // lets a trimmed stack resolve to a managed frame.
            if (data is MethodLoadUnloadVerboseTraceData method
                && method.MethodNamespace is not null
                && method.MethodNamespace.Contains("HotLoopBench", StringComparison.Ordinal))
            {
                benchmarkJitMethods++;
            }
        }

        Console.WriteLine($"Events: {traceLog.EventCount:N0} across {byType.Count} types");
        Console.WriteLine($"AllocationTick: {allocTicks:N0} total, {allocTicksWithStack:N0} with a call stack");
        Console.WriteLine($"CpuSample: {cpuSamples:N0} total, {cpuSamplesWithStack:N0} with a call stack, {cpuSamplesWithResolvedFrame:N0} with a resolved frame");
        Console.WriteLine($"Benchmark JIT methods in rundown: {benchmarkJitMethods:N0}");
        foreach (KeyValuePair<string, int> pair in byType.OrderByDescending(static p => p.Value))
        {
            Console.WriteLine($"  {pair.Value,9:N0}  {pair.Key}");
        }

        if (firstResolvedStack is not null)
        {
            Console.WriteLine("First resolved CPU-sample stack (leaf first):");
            foreach (string frame in firstResolvedStack)
            {
                Console.WriteLine($"    {frame}");
            }
        }

        if (firstManagedStack is not null)
        {
            string joined = string.Join(" <- ", firstManagedStack);
            Console.WriteLine($"First managed CPU-sample stack: {joined}");
        }
        else if (cpuSamplesWithStack > 0)
        {
            Console.WriteLine("No CPU-sample stack reached a HotLoopBench managed frame.");
        }

        return 0;
    }

    // Walks a call stack leaf-to-root into its "module!method" frame names, capped so
    // a deep stack does not flood the diagnostic output.
    private static string[] DescribeStack(TraceCallStack stack)
    {
        List<string> frames = [];
        TraceCallStack? current = stack;
        while (current is not null && frames.Count < 40)
        {
            string module = current.CodeAddress.ModuleName ?? "?";
            frames.Add($"{module}!{current.CodeAddress.FullMethodName}");
            current = current.Caller;
        }

        return [.. frames];
    }
}
