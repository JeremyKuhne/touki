// Copyright (c) 2025 Jeremy W Kuhne
// SPDX-License-Identifier: MIT
// See LICENSE file in the project root for full license information

namespace TraceQ.Output;

/// <summary>
///  The <c>trace_export</c> confirmation payload: what flame-graph file was written,
///  where, and how large it is.
/// </summary>
/// <remarks>
///  <para>
///   Unlike the query tools, export's product is a file on disk, not trace data in
///   the response. This payload is the receipt an agent reads back to confirm the
///   write succeeded and to tell a human where the flame graph is and how to open it
///   (the viewer hint travels in the envelope's hints channel).
///  </para>
/// </remarks>
/// <param name="Format">The flame-graph format written (<c>speedscope</c> or <c>chromium</c>).</param>
/// <param name="OutputPath">The absolute path the flame graph was written to.</param>
/// <param name="ByteCount">The size of the written file, in bytes.</param>
/// <param name="Name">The profile name embedded in the flame graph, shown in the viewer.</param>
public sealed record ExportResult(
    string Format,
    string OutputPath,
    long ByteCount,
    string Name);
