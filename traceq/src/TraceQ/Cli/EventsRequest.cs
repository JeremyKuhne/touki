// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Cli;

/// <summary>
///  The validated inputs to an events query: which trace to read, the name filter,
///  the page (skip / take), the per-event payload cap, and how to render it.
/// </summary>
/// <remarks>
///  <para>
///   This is the boundary between command-line parsing and the execution in
///   <see cref="EventsExecutor"/>; keeping it a plain record lets the executor be
///   exercised directly in tests without driving the parser.
///  </para>
/// </remarks>
/// <param name="Path">The trace file path.</param>
/// <param name="Name">A case-insensitive substring matched against <c>Provider/EventName</c>; empty matches every event.</param>
/// <param name="Skip">The number of matches to skip (for paging).</param>
/// <param name="Take">The maximum number of matches to return on this page.</param>
/// <param name="MaxPayload">The per-event payload character cap.</param>
/// <param name="Format">The render format.</param>
internal sealed record EventsRequest(
    string Path,
    string Name,
    int Skip,
    int Take,
    int MaxPayload,
    OutputFormat Format);
