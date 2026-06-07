// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  Scopes a trace read to a workload process tree: the process(es) whose name
///  contains <see cref="NameSubstring"/> plus, when <see cref="IncludeChildren"/>
///  is set, all of their descendants.
/// </summary>
/// <remarks>
///  <para>
///   This is how an analysis is confined to the work that matters without
///   physically rewriting the trace. A machine-wide ETW capture holds every
///   process on the box; scoping at read time keeps only the samples that belong
///   to the workload, losslessly, because the trace is fully symbol-resolved
///   before any sample is dropped.
///  </para>
///  <para>
///   Following children is the default because the common capture shapes need it.
///   BenchmarkDotNet runs each workload in a child process that the orchestrating
///   host launches, so scoping to the host name without its children would miss
///   the measured code entirely. Profiling an application the same way - launch it
///   under a capture - puts the real work in the launched process and its
///   children. Set <see cref="IncludeChildren"/> to <see langword="false"/> to
///   confine the scope to the named process alone.
///  </para>
/// </remarks>
/// <param name="NameSubstring">
///  A case-insensitive substring matched against process names to find the tree
///  roots.
/// </param>
/// <param name="IncludeChildren">
///  Whether to also include every descendant of a matched process. Defaults to
///  <see langword="true"/>.
/// </param>
internal sealed record ProcessScope(string NameSubstring, bool IncludeChildren = true)
{
    /// <summary>
    ///  A case-insensitive substring matched against process names to find the tree
    ///  roots.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Validated at construction: an empty substring would match every process and
    ///   silently disable scoping, and a <see langword="null"/> one would throw a less
    ///   clear exception later, so a malformed scope fails fast and predictably here.
    ///  </para>
    /// </remarks>
    public string NameSubstring { get; } = string.IsNullOrEmpty(NameSubstring)
        ? throw new ArgumentException("The process-scope name substring must be non-empty.", nameof(NameSubstring))
        : NameSubstring;
}
