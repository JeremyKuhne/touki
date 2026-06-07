// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using Microsoft.Diagnostics.Tracing.Etlx;

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
