// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using TraceQ.Tracing.Readers;

namespace TraceQ.Tracing.Providers;

/// <summary>
///  The thread-time stack-source provider: reconstructs each thread's wall-clock
///  timeline from an ETW capture - the time it spent running and the time it spent
///  blocked - into stacks weighted by elapsed milliseconds, so the engine can rank
///  where wall-clock time went, not just where the CPU was busy.
/// </summary>
/// <remarks>
///  <para>
///   CPU sampling only sees threads that are running, so it cannot explain time a
///   thread spends blocked (waiting on a lock, I/O, or a sleep). Thread time fills
///   that gap by simulating each thread's state from the trace's context-switch
///   events: every interval a thread is switched out is credited as
///   <c>BLOCKED_TIME</c> on the stack it blocked at, and every running interval as
///   <c>CPU_TIME</c>. The result is the same {stack, weight} shape as the CPU
///   sampler - only the metric (<see cref="MetricInfo.ThreadTime"/>) differs - so
///   the existing <see cref="FoldingAggregator"/> ranks it unchanged.
///  </para>
///  <para>
///   Context-switch events are an ETW (kernel) capability, so this provider reads
///   an <c>.etl</c> captured with the context-switch keywords. An EventPipe
///   <c>.nettrace</c> samples only running threads and carries no blocked time, so
///   thread time is not available from it. Each stack is rooted at its process and
///   thread, which is also how a scope narrows a machine-wide capture to one
///   workload tree.
///  </para>
/// </remarks>
internal sealed class ThreadTimeProvider
{
    // The thread-time computer roots each stack at a process pseudo-frame formatted
    // "Process<bits> <name> (<pid>) Args: <args>" and a thread pseudo-frame
    // "Thread (<tid>) CPU=<n>ms (<name>)". These extract the ids and a clean label.
    private static readonly Regex s_processFrame =
        new(@"^Process\d*\s+(?<name>.+?)\s+\((?<pid>\d+)\)(?:\s+Args:.*)?$", RegexOptions.Compiled);

    private static readonly Regex s_threadFrame =
        new(@"^Thread\s+\((?<tid>\d+)\)", RegexOptions.Compiled);

    /// <summary>
    ///  Reads the thread-time stack-sample source from the ETW trace at
    ///  <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The <c>.etl</c> file path.</param>
    /// <param name="processScope">
    ///  Optional process-tree scope. When set, only the stacks of the matched
    ///  workload tree are returned, narrowing a machine-wide capture to one scenario.
    /// </param>
    /// <returns>The thread-time source: elapsed-millisecond-weighted stacks.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public StackSampleSource Read(string path, ProcessScope? processScope = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Trace file not found: {fullPath}", fullPath);
        }

        using TraceLog traceLog = TraceLog.OpenOrConvert(
            fullPath,
            new TraceLogOptions { ContinueOnError = true });

        using SymbolReader symbolReader = new(TextWriter.Null, "", null);

        HashSet<int>? scopePids = processScope is null ? null : ProcessTree.ResolvePids(traceLog, processScope);

        MutableTraceEventStackSource stackSource = new(traceLog);

        // The computer is flagged experimental rather than truly obsolete; its
        // GenerateThreadTimeStacks output is the documented thread-time model.
#pragma warning disable CS0618
        ThreadTimeStackComputer computer = new(traceLog, symbolReader)
        {
            // Ready-thread frames describe scheduler latency, not where time went;
            // excluding them keeps the stacks to running and blocked intervals.
            ExcludeReadyThread = true
        };
        computer.GenerateThreadTimeStacks(stackSource);
#pragma warning restore CS0618

        List<SampleStack> samples = [];
        List<string> leafToRoot = [];

        stackSource.ForEach(sample =>
        {
            double weight = sample.Metric;
            if (weight <= 0.0)
            {
                return;
            }

            leafToRoot.Clear();
            for (StackSourceCallStackIndex index = sample.StackIndex;
                index != StackSourceCallStackIndex.Invalid;
                index = stackSource.GetCallerIndex(index))
            {
                StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(index);
                leafToRoot.Add(stackSource.GetFrameName(frameIndex, false));
            }

            if (leafToRoot.Count == 0)
            {
                return;
            }

            // The process pseudo-frame is the outermost (last walked). Resolve it for
            // both the per-process label and the scope filter.
            string rootFrame = leafToRoot[^1];
            string process = NormalizeProcessFrame(rootFrame, out int pid);

            if (scopePids is not null && (pid < 0 || !scopePids.Contains(pid)))
            {
                return;
            }

            string thread = "";
            int count = leafToRoot.Count;
            string[] frames = new string[count];
            for (int i = 0; i < count; i++)
            {
                string frame = leafToRoot[count - 1 - i];

                // Clean the verbose process / thread pseudo-frames into stable labels so
                // a ranking is not dominated by per-thread CPU annotations or a process
                // command line; other frames (the real call stack and the CPU / blocked
                // leaf) pass through untouched.
                if (i == 0)
                {
                    frame = process;
                }
                else if (i == 1 && s_threadFrame.Match(frame) is { Success: true } threadMatch)
                {
                    thread = threadMatch.Groups["tid"].Value;
                    frame = $"Thread ({thread})";
                }

                frames[i] = frame;
            }

            samples.Add(new SampleStack(frames, weight, thread, frameLocations: null, process));
        });

        return new StackSampleSource(MetricInfo.ThreadTime, samples);
    }

    // Turns "Process64 <name> (<pid>) Args: ..." into "<name> (<pid>)" and yields the
    // pid; returns the original frame with pid -1 when it does not match the shape.
    private static string NormalizeProcessFrame(string frame, out int pid)
    {
        Match match = s_processFrame.Match(frame);
        if (!match.Success || !int.TryParse(match.Groups["pid"].Value, out pid))
        {
            pid = -1;
            return frame;
        }

        return $"{match.Groups["name"].Value} ({pid})";
    }
}
