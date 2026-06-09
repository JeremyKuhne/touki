// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

using TraceQ.Tracing;

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a ranking run: which trace to load, how to scope and
///  fold it, which measure to report, and how to render it.
/// </summary>
/// <remarks>
///  <para>
///   This is the boundary between command-line parsing (owned by
///   ConsoleAppFramework, which binds and type-checks the verb parameters) and the
///   provider-agnostic execution in <see cref="RankingExecutor"/>. Keeping the
///   executor's input a plain record lets it be exercised directly in tests without
///   driving the parser.
///  </para>
/// </remarks>
/// <param name="Path">The trace file path.</param>
/// <param name="Metric">Which provider view to rank (CPU, allocations, ...).</param>
/// <param name="Root">Substring scoping the ranking to a subtree, or empty for the whole trace.</param>
/// <param name="Top">Maximum number of ranked rows to return.</param>
/// <param name="Fold">The leaf-frame fold patterns.</param>
/// <param name="Measure">Which measure to report.</param>
/// <param name="Format">The render format.</param>
/// <param name="Symbols">Optional build-output directory whose embedded PDBs resolve managed frames.</param>
/// <param name="Strict">Whether to trip the strict symbol-resolution exit gate.</param>
internal sealed record RankRequest(
    string Path,
    TraceMetric Metric,
    string Root,
    int Top,
    IReadOnlyList<string> Fold,
    Measure Measure,
    OutputFormat Format,
    string? Symbols,
    bool Strict);
