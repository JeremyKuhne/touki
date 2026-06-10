// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Tracing;

/// <summary>
///  Resolves the user-facing <c>metric</c> selector string - the same one the
///  <c>rank</c> verb and the <c>trace_rank</c> MCP tool accept - to the
///  <see cref="TraceMetric"/> the loader builds.
/// </summary>
/// <remarks>
///  <para>
///   This is the single source of truth for the selector vocabulary so the CLI and
///   MCP heads cannot drift: both resolve <c>cpu</c>, <c>threadtime</c>,
///   <c>alloc</c> (or its <c>allocations</c> alias), and <c>exceptions</c> through
///   here, and both report an unknown selector against the one
///   <see cref="Selectors"/> list.
///  </para>
/// </remarks>
public static class TraceMetricSelector
{
    /// <summary>
    ///  The canonical selector names, lowest-level first, for help and error text.
    ///  The <c>alloc</c> selector also accepts the <c>allocations</c> alias.
    /// </summary>
    public static IReadOnlyList<string> Selectors { get; } = ["cpu", "threadtime", "alloc", "exceptions"];

    /// <summary>
    ///  Resolves a <c>metric</c> selector string to the provider view it names.
    /// </summary>
    /// <param name="selector">The requested provider metric (case-insensitive).</param>
    /// <param name="metric">The resolved provider view when recognized; otherwise <see cref="TraceMetric.Cpu"/>.</param>
    /// <returns>
    ///  <see langword="true"/> when <paramref name="selector"/> names a wired provider;
    ///  otherwise <see langword="false"/>, and the caller should report a usage error.
    /// </returns>
    public static bool TryResolve(string selector, out TraceMetric metric)
    {
        switch (selector.ToLowerInvariant())
        {
            case "cpu":
                metric = TraceMetric.Cpu;
                return true;
            case "threadtime":
                metric = TraceMetric.ThreadTime;
                return true;
            case "alloc":
            case "allocations":
                metric = TraceMetric.Allocations;
                return true;
            case "exceptions":
                metric = TraceMetric.Exceptions;
                return true;
            default:
                metric = TraceMetric.Cpu;
                return false;
        }
    }
}
