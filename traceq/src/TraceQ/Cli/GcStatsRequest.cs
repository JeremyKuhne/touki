// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to a GC-stats run: which trace to read, how many
///  per-collection rows to show, and how to render it.
/// </summary>
/// <remarks>
///  <para>
///   This is the boundary between command-line parsing and the execution in
///   <see cref="GcStatsExecutor"/>; keeping it a plain record lets the executor be
///   exercised directly in tests without driving the parser.
///  </para>
/// </remarks>
/// <param name="Path">The trace file path.</param>
/// <param name="Top">Maximum number of per-collection rows to show, ranked by pause time.</param>
/// <param name="Format">The render format.</param>
internal sealed record GcStatsRequest(
    string Path,
    int Top,
    OutputFormat Format);
