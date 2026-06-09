// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  How a trace read should be scoped to processes: the agent-facing intent the
///  loader resolves into a concrete <see cref="ProcessScope"/> (or none).
/// </summary>
/// <remarks>
///  <para>
///   A machine-wide capture holds every process on the box, and an unscoped
///   ranking is the most common way an agent burns its token budget on irrelevant
///   processes. So scenario scope is the default: when neither a name nor the
///   all-processes opt-out is given, the loader scopes a multi-process capture to
///   the busiest process and its tree automatically. The two explicit modes are an
///   override (<see cref="ForProcess"/>) and an opt-out (<see cref="AllProcesses"/>).
///  </para>
///  <para>
///   Scoping only applies to a multi-process capture (an ETW <c>.etl</c>); the
///   single-process EventPipe and speedscope formats carry one process, so every
///   mode is a no-op there.
///  </para>
/// </remarks>
public sealed class ScopeRequest
{
    private ScopeRequest(bool includeAll, string? processName, bool includeChildren)
    {
        IncludeAll = includeAll;
        ProcessName = processName;
        IncludeChildren = includeChildren;
    }

    /// <summary>
    ///  The default: let the loader scope a multi-process capture to the busiest
    ///  process tree automatically.
    /// </summary>
    public static ScopeRequest Auto { get; } = new(includeAll: false, processName: null, includeChildren: true);

    /// <summary>
    ///  Read every process - the opt-out from automatic scenario scoping.
    /// </summary>
    public static ScopeRequest AllProcesses { get; } = new(includeAll: true, processName: null, includeChildren: true);

    /// <summary>
    ///  Scope to the process(es) whose name contains <paramref name="processName"/>,
    ///  optionally including their descendants.
    /// </summary>
    /// <param name="processName">A case-insensitive process-name substring.</param>
    /// <param name="includeChildren">
    ///  Whether to also include every descendant of a matched process. Defaults to
    ///  <see langword="true"/>, matching the capture shapes (a host that launches the
    ///  measured work in a child).
    /// </param>
    /// <returns>The scope request.</returns>
    /// <exception cref="ArgumentException"><paramref name="processName"/> is <see langword="null"/> or empty.</exception>
    public static ScopeRequest ForProcess(string processName, bool includeChildren = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(processName);
        return new ScopeRequest(includeAll: false, processName: processName, includeChildren: includeChildren);
    }

    /// <summary>
    ///  Whether every process is read (the all-processes opt-out).
    /// </summary>
    public bool IncludeAll { get; }

    /// <summary>
    ///  The explicit process-name substring to scope to, or <see langword="null"/>
    ///  when none was given (automatic or all-processes).
    /// </summary>
    public string? ProcessName { get; }

    /// <summary>
    ///  Whether a matched process's descendants are included in the scope. Applies to
    ///  an explicit <see cref="ForProcess"/> request and to the automatic scope.
    /// </summary>
    public bool IncludeChildren { get; }
}

