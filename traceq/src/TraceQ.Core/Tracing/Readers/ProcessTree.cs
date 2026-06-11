// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace TraceQ.Tracing.Readers;

/// <summary>
///  Resolves a <see cref="ProcessScope"/> against a trace's process table into the
///  set of process IDs that make up the scoped workload tree.
/// </summary>
/// <remarks>
///  <para>
///   Both the CPU stack reader and the thread-time provider scope a machine-wide
///   capture to one workload, and both need the same rule: the processes whose
///   name matches plus, by default, all of their descendants. Keeping that rule in
///   one place means the two paths cannot drift.
///  </para>
/// </remarks>
internal static class ProcessTree
{
    /// <summary>
    ///  Resolves a high-level <see cref="ScopeRequest"/> against a trace's process
    ///  table into the set of process IDs to keep, applying the automatic
    ///  busiest-process default when neither an explicit name nor the all-processes
    ///  opt-out was given.
    /// </summary>
    /// <param name="traceLog">The opened trace whose process table is queried.</param>
    /// <param name="request">The scope intent to resolve.</param>
    /// <param name="appliedProcessName">
    ///  The name of the process the scope resolved to (the explicit substring, or the
    ///  busiest process chosen automatically), or <see langword="null"/> when no scope
    ///  applies (all-processes, or an automatic scope that found no busy process).
    /// </param>
    /// <returns>
    ///  The process IDs to keep, or <see langword="null"/> when every process is read
    ///  (all-processes, or an automatic scope with nothing to narrow to).
    /// </returns>
    public static HashSet<int>? ResolveScope(
        TraceLog traceLog,
        ScopeRequest request,
        out string? appliedProcessName)
    {
        appliedProcessName = null;

        if (request.IncludeAll)
        {
            return null;
        }

        // An explicit name wins; otherwise the automatic default picks the busiest
        // process so a machine-wide capture narrows to the workload without the caller
        // naming it. A capture with no busy named process leaves the read unscoped.
        bool automatic = request.ProcessName is null;
        string? name = request.ProcessName ?? FindBusiestProcessName(traceLog);
        if (name is null)
        {
            return null;
        }

        HashSet<int> keep = ResolvePids(traceLog, new ProcessScope(name, request.IncludeChildren));

        // An explicit name always reports (the caller asked to scope, even if it
        // happens to match every process). The automatic scope only reports when it
        // actually narrowed - a capture that is already a single tree (a trimmed
        // fixture, say) is not "scoped" in any meaningful sense, so it stays silent
        // rather than emit a notice for a no-op.
        if (!automatic || NarrowsTheCapture(traceLog, keep))
        {
            appliedProcessName = name;
        }

        return keep;
    }

    // Whether the kept set excludes at least one process that carried activity, i.e.
    // scoping actually dropped something rather than matching the whole capture.
    private static bool NarrowsTheCapture(TraceLog traceLog, HashSet<int> keep)
    {
        foreach (TraceProcess process in traceLog.Processes)
        {
            if (process.CPUMSec > 0.0f && process.ProcessID != 0 && !keep.Contains(process.ProcessID))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///  Finds the name of the busiest process in the trace - the one that owns the most
    ///  CPU samples - so an unscoped read can default to that process's tree rather than
    ///  the whole machine-wide capture.
    /// </summary>
    /// <param name="traceLog">The opened trace whose CPU samples are counted.</param>
    /// <returns>
    ///  The busiest named process's name, or <see langword="null"/> when the trace
    ///  carries no CPU samples attributable to a named process (so no automatic scope
    ///  applies).
    /// </returns>
    /// <remarks>
    ///  <para>
    ///   "Busiest" is ranked by <em>CPU sample count</em>, not <see cref="TraceProcess.CPUMSec"/>.
    ///   The rankings this scope feeds are built from CPU samples, so the process that
    ///   owns the most samples is by definition the one the analysis is about. CPUMSec
    ///   can disagree sharply on a machine-wide capture: a long-lived background service
    ///   (antivirus, a VPN client) accumulates more kernel CPU time across the whole
    ///   capture window than a short benchmark, yet carries a tiny fraction of the
    ///   profile's samples - which made a CPUMSec heuristic auto-scope to the wrong
    ///   process. Counting samples is one extra lightweight event pass (process id only,
    ///   no stack walk); it runs only for the automatic scope (no explicit process
    ///   given) and its result is cached with the loaded trace.
    ///  </para>
    ///  <para>
    ///   The matched name still resolves to the whole tree (the process plus its
    ///   descendants) at scope time, so a host that launches the measured work in a
    ///   child is covered.
    ///  </para>
    /// </remarks>
    public static string? FindBusiestProcessName(TraceLog traceLog)
    {
        // Count CPU samples per process id. The predicate mirrors the CPU-sample
        // selection in TraceLogReader exactly (ETW SampledProfileTraceData, EventPipe
        // ClrThreadSampleTraceData excluding error samples) so the busiest process is
        // chosen by the same events the rankings are built from.
        Dictionary<int, int> samplesByProcess = [];
        foreach (TraceEvent data in traceLog.Events)
        {
            if (data is ClrThreadSampleTraceData clrSample)
            {
                if (clrSample.Type == ClrThreadSampleType.Error)
                {
                    continue;
                }
            }
            else if (data is not SampledProfileTraceData)
            {
                continue;
            }

            int pid = data.ProcessID;
            if (pid > 0)
            {
                samplesByProcess[pid] = samplesByProcess.GetValueOrDefault(pid) + 1;
            }
        }

        if (samplesByProcess.Count == 0)
        {
            return null;
        }

        // The Idle process (pid 0) is bookkeeping, not workload, and an unnamed process
        // cannot be matched by a name substring later - skip both.
        string? busiest = null;
        int maxSamples = 0;
        foreach (TraceProcess process in traceLog.Processes)
        {
            if (process.ProcessID == 0 || string.IsNullOrEmpty(process.Name))
            {
                continue;
            }

            int count = samplesByProcess.GetValueOrDefault(process.ProcessID);
            if (count > maxSamples)
            {
                maxSamples = count;
                busiest = process.Name;
            }
        }

        return busiest;
    }

    /// <summary>
    ///  Resolves a <see cref="ProcessScope"/> to the set of process IDs in the matched
    ///  process tree: every process whose name contains the scope substring, plus -
    ///  when the scope includes children - all of their descendants, found by walking
    ///  each process's parent chain.
    /// </summary>
    /// <param name="traceLog">The opened trace whose process table is queried.</param>
    /// <param name="scope">The scope to resolve.</param>
    /// <returns>The process IDs in the scoped tree; empty when nothing matches.</returns>
    public static HashSet<int> ResolvePids(TraceLog traceLog, ProcessScope scope)
    {
        HashSet<int> roots = [];
        foreach (TraceProcess process in traceLog.Processes)
        {
            if (process.Name is not null
                && process.Name.IndexOf(scope.NameSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                roots.Add(process.ProcessID);
            }
        }

        if (!scope.IncludeChildren)
        {
            return roots;
        }

        HashSet<int> keep = [.. roots];
        foreach (TraceProcess process in traceLog.Processes)
        {
            if (keep.Contains(process.ProcessID))
            {
                continue;
            }

            // A process is in scope when any ancestor is a root. The chain is shallow
            // (host -> job), so walking it per process is cheap.
            for (TraceProcess? ancestor = process.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (roots.Contains(ancestor.ProcessID))
                {
                    keep.Add(process.ProcessID);
                    break;
                }
            }
        }

        return keep;
    }
}
